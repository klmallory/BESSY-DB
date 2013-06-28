/*
Copyright (c) 2011,2012,2013 Kristen Mallory dba Klink

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading.Tasks;
using BESSy.Cache;
using BESSy.Extensions;
using BESSy.Factories;
using BESSy.Files;
using BESSy.Indexes;
using BESSy.Parallelization;
using BESSy.Queries;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using BESSy.Transactions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using BESSy.Replication;

namespace BESSy
{
    public interface ITransactionalDatabase<IdType, EntityType> :  IIndexedRepository<EntityType, IdType>, IQueryableRepository<EntityType>
    {
        ITransaction<IdType, EntityType> BeginTransaction();
        void FlushAll();
    }

    /// <summary>
    /// Abstract implementation for a fully transactional and queryable database.
    /// </summary>
    public class AbstractTransactionalDatabase<IdType, EntityType> : ITransactionalDatabase<IdType, EntityType>
    {
        protected AbstractTransactionalDatabase(string fileName)
        {
            _fileName = fileName;
        }

        /// <summary>
        /// Opens an existing database with the specified paramters.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="transactionManager"></param>
        /// <param name="databaseFileFactory"></param>
        /// <param name="cacheFactory"></param>
        /// <param name="indexFileFactory"></param>
        /// <param name="indexFactory"></param>
        /// <param name="rowSynchronizer"></param>
        public AbstractTransactionalDatabase(
            string fileName
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, EntityType> transactionManager
            , IAtomicFileManagerFactory databaseFileFactory
            , IRepositoryCacheFactory cacheFactory
            , IIndexFileFactory indexFileFactory
            , IIndexFactory indexFactory
            , IRowSynchronizer<int> rowSynchronizer)
        {
            _fileName = fileName;
            _formatter = formatter;
            _transactionManager = transactionManager;
            _fileFactory = databaseFileFactory;
            _cacheFactory = cacheFactory;
            _indexFileFactory = indexFileFactory;
            _indexFactory = indexFactory;
            _rowSynchronizer = rowSynchronizer;

            _transactionManager.TransactionCommitted += new TransactionCommit<IdType, EntityType>(OnTransactionCommitted);
        }

        /// <summary>
        /// Open a new or existing database with the specified paramters.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="idToken"></param>
        /// <param name="seed"></param>
        /// <param name="idConverter"></param>
        /// <param name="formatter"></param>
        /// <param name="transactionManager"></param>
        /// <param name="databaseFileFactory"></param>
        /// <param name="cacheFactory"></param>
        /// <param name="indexFileFactory"></param>
        /// <param name="indexFactory"></param>
        /// <param name="rowSynchronizer"></param>
        public AbstractTransactionalDatabase(
            string fileName
            , string idToken
            , ISeed<IdType> seed
            , IBinConverter<IdType> idConverter
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, EntityType> transactionManager
            , IAtomicFileManagerFactory databaseFileFactory
            , IRepositoryCacheFactory cacheFactory
            , IIndexFileFactory indexFileFactory
            , IIndexFactory indexFactory
            , IRowSynchronizer<int> rowSynchronizer)
        {
            _createNew = true;

            _fileName = fileName;
            _idToken = idToken;
            _seed = seed;
            _idConverter = idConverter;
            _formatter = formatter;
            _transactionManager = transactionManager;
            _indexFactory = indexFactory;
            _cacheFactory = cacheFactory;
            _fileFactory = databaseFileFactory;
            _indexFileFactory = indexFileFactory;
            _rowSynchronizer = rowSynchronizer;

            _seed.IdConverter = idConverter;
            _seed.IdProperty = idToken;

            _transactionManager.TransactionCommitted += new TransactionCommit<IdType, EntityType>(OnTransactionCommitted);
        }

        protected object _syncRoot = new object();
        protected object _syncTrans = new object();
        protected object _syncStaging = new object();
        protected object _syncOperations = new object();

        protected Stack<int> _operations = new Stack<int>();

        protected bool _publish;
        protected bool _passthrough;
        protected bool _createNew; 
        protected string _fileName;
        protected string _idToken;
        protected IBinConverter<IdType> _idConverter;
        protected ISeed<IdType> _seed;
        protected ITransaction<IdType, EntityType> _activeTransaction;
        protected ITransactionManager<IdType, EntityType> _transactionManager;
        protected IAtomicFileManager<EntityType> _fileManager;
        
        protected IRepositoryCache<IdType, EntityType> _databaseCache;
        protected IRepositoryCache<Guid, IDictionary<IdType, JObject>> _stagingCache;
        protected IRowSynchronizer<int> _rowSynchronizer;

        protected IAtomicFileManager<IndexPropertyPair<IdType, int>> _indexFileManager;
        protected IPrimaryIndex<IdType, EntityType> _primaryIndex;
        protected IQueryableFormatter _formatter;
        protected IRepositoryCacheFactory _cacheFactory;
        protected IAtomicFileManagerFactory _fileFactory;
        protected IIndexFileFactory _indexFileFactory;
        protected IIndexFactory _indexFactory;

        protected IReplicationSubscriber<IdType, EntityType> _subscriber;
        protected IReplicationPublisher<IdType, EntityType> _publisher;

        protected Func<EntityType, IdType> _idGet;
        protected Action<EntityType, IdType> _idSet;

        protected virtual string GetIndexName(string fileName)
        {
            return fileName + ".index";
        }

        protected void OnTransactionCommitted(ITransaction<IdType, EntityType> transaction)
        {
            Trace.TraceInformation("Transaction {0} commit detected", transaction.Id);

            lock (_syncOperations)
                _operations.Push(1);

            try
            {
                var segs = new Dictionary<IdType, int>();

                var staging = transaction.GetEnlistedActions();

                if (staging.Count <= 0)
                    return;

                foreach (var a in staging)
                    if (a.Value.Action != Action.Create)
                        segs.Add(a.Key, _primaryIndex.Fetch(a.Key));
                    else
                        segs.Add(a.Key, 0);

                Trace.TraceInformation("Transaction {0} committing with {1} segments", transaction.Id, segs.Count);

                var commitState = new Tuple<ITransaction<IdType, EntityType>, IDictionary<IdType, int>>(transaction, segs);

                Parallel.Invoke(new System.Action[] 
                { 
                    new System.Action(delegate() { 
                        UpdateStaging(transaction, staging); 
                    }), 
                    new System.Action(delegate() { 
                        CommitTransactionToFile(transaction, segs); 
                    })
                });

                if (_publish)
                    _publisher.Publish(transaction);
            }
            finally
            {
                lock (_syncTrans)
                    if (transaction != null)
                        transaction.MarkComplete();

                lock (_syncOperations)
                    _operations.Pop();
            }
        }

        protected void UpdateStaging(ITransaction<IdType, EntityType> transaction, IDictionary<IdType, EnlistedAction<IdType, EntityType>> staging)
        {
            var c = new Dictionary<IdType, JObject>();

            foreach (var s in staging.Where(s => s.Value.Action != Action.Delete))
                c.Add(s.Value.Id, JObject.FromObject(s.Value.Entity));
            foreach (var s in staging.Where(s => s.Value.Action == Action.Delete))
                c.Add(s.Value.Id, null);

            lock (_syncStaging)
                _stagingCache.UpdateCache(transaction.Id, c, true, false);

            SyncCache(staging);

            Trace.TraceInformation("Transaction {0} update staging thread complete", transaction.Id);
        }

        protected void CommitTransactionToFile(ITransaction<IdType, EntityType> transaction, IDictionary<IdType, int> segments)
        {
            if (transaction == null)
                return;

            _fileManager.CommitTransaction(transaction, segments);

            RecentTransactions.UpdateCache(transaction.Id, 0, true, false);

            Trace.TraceInformation("Transaction {0} commit thread complete", transaction.Id);
        }

        protected virtual void SyncCache(IDictionary<IdType, EnlistedAction<IdType, EntityType>> actions)
        {
            lock (_syncRoot)
                foreach (var a in actions)
                    _databaseCache.Detach(a.Key);
        }

        protected virtual void AfterTransactionCommitted(IList<TransactionResult<EntityType>> results, IDisposable transaction)
        {
            Trace.TraceInformation("Database transaction post commit complete.");
        }

        protected virtual void OnSaveFailed(SaveFailureInfo<EntityType> saveFailInfo)
        {
            Trace.TraceInformation("Database save failure detected... rebuilding");

            _fileManager.Rebuild(saveFailInfo.NewRowSize, saveFailInfo.NewDatabaseSize, _fileManager.SeedPosition);

            if (saveFailInfo.Segment > 0)
                _fileManager.SaveSegment(saveFailInfo.Entity, saveFailInfo.Segment);
            else
                _fileManager.SaveSegment(saveFailInfo.Entity);
        }

        protected virtual void OnReplicateReceived(ITransaction<IdType, EntityType> transaction, long timestamp)
        {
            Trace.TraceInformation("Transaction {0} replicate detected", transaction.Id);

            lock (_syncOperations)
                _operations.Push(1);

            try
            {
                var segs = new Dictionary<IdType, int>();

                var staging = transaction.GetEnlistedActions();

                foreach (var a in staging)
                    if (a.Value.Action == Action.Delete)
                    {
                        var seg = _primaryIndex.Fetch(a.Key);
                        if (seg > 0)
                            segs.Add(a.Key, seg);
                        else
                            segs.Add(a.Key, -1);
                    }
                    else
                    {
                        if (_idConverter.Compare(a.Key, default(IdType)) != 0)
                            segs.Add(a.Key, _primaryIndex.Fetch(a.Key));
                        else
                            segs.Add(a.Key, 0);
                    }

                Trace.TraceInformation("Transaction {0} replciating with {1} segments", transaction.Id, segs.Count);

                var commitState = new Tuple<ITransaction<IdType, EntityType>, IDictionary<IdType, int>>(transaction, segs);

                Parallel.Invoke(new System.Action[] 
                { 
                    new System.Action(delegate() { 
                        UpdateStaging(transaction, staging); 
                    }), 
                    new System.Action(delegate() { 
                        CommitTransactionToFile(transaction, segs); 
                    })
                });
            }
            finally
            {
                lock (_syncTrans)
                    if (transaction != null)
                        transaction.MarkComplete();

                lock (_syncOperations)
                    _operations.Pop();
            }

            _seed.LastReplicatedTimeStamp = timestamp;
        }

        internal virtual void InitilizeReplicationSubscription(IReplicationSubscriber<IdType, EntityType> subscriber)
        {
            lock (_syncRoot)
            {
                if (subscriber == null)
                {
                    if (_subscriber != null)
                        try { _subscriber.OnReplicate -= new ReplicateTransaction<IdType, EntityType>(OnReplicateReceived); }
                        catch (Exception) { }

                    _subscriber = null;

                    return;
                }

                _subscriber = subscriber;
                _subscriber.OnReplicate += new ReplicateTransaction<IdType, EntityType>(OnReplicateReceived);
            }
        }

        internal virtual void InitilizeReplicationPublishing(IReplicationPublisher<IdType, EntityType> publisher)
        {
            lock (_syncRoot)
            {
                if (publisher == null)
                {
                    _publish = false;
                    _publisher = null;
                    return;
                }

                _publish = true;
                _publisher = publisher;
            }
        }

        public virtual long LastReplicatedTimeStamp { get { return _seed.LastReplicatedTimeStamp; } }
        public virtual bool FileFlushQueueActive { get { return _fileManager.FileFlushQueueActive; } }
        public virtual int Length { get { return _fileManager.Length; } }
        public IRepositoryCache<Guid, int> RecentTransactions { get; protected set; }
        public Guid TransactionSource { get { return _seed.Source; } }
        public virtual bool AutoCommit { get; set; }

        public virtual ITransaction<IdType, EntityType> BeginTransaction()
        {
            var tLock = _transactionManager.BeginTransaction();

            return tLock.Transaction;
        }

        public virtual int Load()
        {
            Trace.TraceInformation("Database loading");

            lock (_syncRoot)
            {
                lock (_syncTrans)
                {
                    lock (_syncStaging)
                    {
                        if (_createNew)
                            _primaryIndex = _indexFactory.CreatePrimary<IdType, EntityType>
                                (GetIndexName(_fileName), _seed, _idConverter, _cacheFactory, _formatter, _indexFileFactory, _rowSynchronizer);
                        else
                            _primaryIndex = _indexFactory.CreatePrimary<IdType, EntityType>
                                (GetIndexName(_fileName), _formatter, _cacheFactory, _indexFileFactory, _rowSynchronizer);

                        _primaryIndex.Load();

                        _seed = _primaryIndex.Seed;
                        _idToken = _seed.IdProperty;
                        _idConverter = (IBinConverter<IdType>)_seed.IdConverter;

                        _fileManager = _fileFactory.Create<IdType, EntityType>(_fileName, Environment.SystemPageSize, 10000, Caching.DetermineOptimumCacheSize(_seed.Stride), _formatter, _rowSynchronizer);

                        _fileManager.Load();

                        _primaryIndex.Register(_fileManager);

                        _fileManager.SaveFailed += new SaveFailed<EntityType>(OnSaveFailed);
                        _fileManager.TransactionCommitted += new Committed<EntityType>(AfterTransactionCommitted);

                        _databaseCache = _cacheFactory.Create<IdType, EntityType>(true, Parallelization.TaskGrouping.ArrayLimit, _idConverter);
                        _stagingCache = _cacheFactory.Create<Guid, IDictionary<IdType, JObject>>(true, Parallelization.TaskGrouping.ArrayLimit, new BinConverterGuid());

                        _idGet = (Func<EntityType, IdType>)Delegate.CreateDelegate(typeof(Func<EntityType, IdType>), typeof(EntityType).GetProperty(_idToken).GetGetMethod());
                        _idSet = (Action<EntityType, IdType>)Delegate.CreateDelegate(typeof(Action<EntityType, IdType>), typeof(EntityType).GetProperty(_idToken).GetSetMethod());

                        //is this seed a passthrough? i.e. string?
                        _passthrough = _idConverter.Compare(_seed.Peek(), default(IdType)) == 0;

                        _transactionManager.Source = _primaryIndex.Seed.Source;

                        RecentTransactions = _cacheFactory.Create<Guid, int>(true, TaskGrouping.ArrayLimit, new BinConverterGuid());

                        return _fileManager.Length;
                    }
                }
            }
        }

        public virtual IdType Add(EntityType item)
        {
            lock (_syncOperations)
                _operations.Push(1);

            try
            {
                var id = _seed.Increment();

                if (!_passthrough)
                    _idSet(item, id);
                else
                    id = _idGet(item);

                using (var tLock = _transactionManager.GetActiveTransaction(false))
                {
                    tLock.Transaction.Enlist(Action.Create, id, item);

                    return id;
                }
            }
            finally { lock (_syncOperations) _operations.Pop(); }
        }

        public virtual void AddOrUpdate(EntityType item, IdType id)
        {
            if (_idConverter.Compare(id, default(IdType)) == 0)
                Add(item);
            else
                Update(item, id);
        }

        public virtual void Update(EntityType item, IdType id)
        {
            var newId = _idGet(item);
            var deleteFirst = (_idConverter.Compare(newId, id) != 0);

            lock (_syncOperations)
                _operations.Push(1);

            try
            {
                using (var tLock = _transactionManager.GetActiveTransaction(false))
                {
                    var oldSeg = _primaryIndex.Fetch(id);

                    if (deleteFirst)
                    {
                        tLock.Transaction.Enlist(Action.Delete, id, item);
                        tLock.Transaction.Enlist(Action.Create, newId, item);
                    }
                    else
                        tLock.Transaction.Enlist(Action.Update, newId, item);
                }
            }
            finally { lock (_syncOperations) _operations.Pop(); }
        }

        public virtual void Delete(IdType id)
        {
            lock (_syncOperations)
                _operations.Push(1);

            try
            {
                lock (_syncTrans)
                {
                    using (var tLock = _transactionManager.GetActiveTransaction(false))
                    {
                        _databaseCache.UpdateCache(id, default(EntityType), true, false);

                        tLock.Transaction.Enlist(Action.Delete, id, Fetch(id));
                    }
                }
            }
            finally { lock (_syncOperations) _operations.Pop(); }
        }

        public virtual EntityType Fetch(IdType id)
        {
            lock (_syncRoot)
                if (_databaseCache.Contains(id))
                    return _databaseCache.GetFromCache(id);

            lock (_syncStaging)
            {
                if (_stagingCache.GetCache().Any(s => s.Value.ContainsKey(id)))
                {
                    var jo = _stagingCache.GetCache().Where(s => s.Value.ContainsKey(id)).Last().Value[id];

                    if (jo != null)
                        return jo.ToObject<EntityType>();

                    return default(EntityType);
                }
            }

            if (_transactionManager.HasActiveTransaction)
            {
                var tmItems = _transactionManager.GetActiveItems();

                if (tmItems.ContainsKey(id))
                    return tmItems[id];
            }

            var seg = _primaryIndex.Fetch(id);

            if (seg > 0)
            {
                var entity = _fileManager.LoadSegmentFrom(seg);

                _databaseCache.UpdateCache(id, entity, false, false);

                return entity;
            }

            return default(EntityType);
        }

        public virtual void Clear()
        {
            lock (_syncTrans)
                _transactionManager.RollBackAll(true);

            lock (_syncRoot)
                _databaseCache.ClearCache();

            lock (_syncStaging)
                _stagingCache.ClearCache();
        }

        public virtual void FlushAll()
        {
            FlushTransactions(true);
        }

        public virtual void Flush()
        {
            FlushTransactions(false);
        }

        protected virtual void FlushTransactions(bool allThreads)
        {
            Trace.TraceInformation("Database flushing");

            lock (_syncTrans)
                _transactionManager.CommitAll(allThreads);

            int ops = 0;

            lock (_syncOperations)
                ops = _operations.Count;

            while (ops > 0)
            {
                Thread.Sleep(100);

                lock (_syncOperations)
                    ops = _operations.Count;
            }
        }

        public virtual int Delete(Func<JObject, bool> selector)
        {
            
            try
            {
                int count = 0;

                lock (_syncTrans)
                {
                    using (var tLock = _transactionManager.GetActiveTransaction(false))
                    {
                        foreach (var page in _fileManager.AsEnumerable())
                            foreach (var obj in page.Where(o => selector(o)))
                            {
                                count++;
                                tLock.Transaction.Enlist(Action.Delete, obj.SelectToken(_idToken, true).Value<IdType>(), obj.ToObject<EntityType>());
                            }
                    }

                    return count;
                }
            }
            catch (Exception ex) { throw new QueryExecuteException("Update Query Execution Failed.", ex); }
        }

        public int Update(Func<JObject, bool> selector, params Action<EntityType>[] updates)
        {
            try
            {
                lock (_syncTrans)
                {
                    var count = 0;

                    using (var tLock = _transactionManager.GetActiveTransaction(false))
                    {
                        foreach (var page in _fileManager.AsEnumerable())
                        {
                            foreach (var obj in page.Where(o => selector(o)))
                            {
                                var entity = obj.ToObject<EntityType>();

                                foreach (var action in updates)
                                    action.Invoke(entity);

                                tLock.Transaction.Enlist(Action.Update, obj.SelectToken(_idToken, true).Value<IdType>(), entity);
                                count++;
                            }
                        }
                    }

                    return count;
                }
            }
            catch (Exception ex) { throw new QueryExecuteException("Update Query Execution Failed.", ex); }
        }

        public IList<EntityType> Select(Func<JObject, bool> selector)
        {
            try
            {
                var list = new List<EntityType>();

                if (_stagingCache.Count > 0)
                    list.AddRange(_stagingCache.GetCache()
                        .SelectMany(s => s.Value.Values)
                            .Where(s => s != null)
                            .Where(selector)
                            .Select(o => o.ToObject<EntityType>()));

                foreach (var page in _fileManager.AsEnumerable())
                    foreach (var obj in page.Where(o => selector(o)))
                        list.Add(obj.ToObject<EntityType>());

                return list.GroupBy(k => _idGet(k)).Select(f => f.First()).ToList();
            }
            catch (Exception ex) { throw new QueryExecuteException("Select Query Execution Failed.", ex); }
        }

        public IList<EntityType> SelectFirst(Func<JObject, bool> selector, int max)
        {
            try
            {
                var list = new List<EntityType>();

                if (_databaseCache.Count > 0)
                    list.AddRange(_databaseCache.GetCache()
                        .Select(s => JObject.FromObject(s.Value))
                        .Where(selector)
                            .Select(o => o.ToObject<EntityType>()));

                if (list.Count < max && _stagingCache.Count > 0)
                        list.AddRange(_stagingCache.GetCache()
                            .SelectMany(s => s.Value.Values)
                                .Where(s => s != null)
                                .Where(selector)
                                .Select(o => o.ToObject<EntityType>()));

                if (list.Count < max)
                {
                    foreach (var page in _fileManager.AsEnumerable())
                    {
                        list.AddRange(page.Where(o => selector(o)).Select(e => e.ToObject<EntityType>()));

                        if (list.Count >= max)
                            break;
                    }
                }

                return list.GroupBy(k => _idGet(k)).Select(f => f.First()).Take(Math.Min(list.Count, max)).ToList();
            }
            catch (Exception ex) { throw new QueryExecuteException("SelectFirst Query Execution Failed.", ex); }
        }

        public IList<EntityType> SelectLast(Func<JObject, bool> selector, int max)
        {
            try
            {
                var list = new List<EntityType>();


                if (_databaseCache.Count > 0)
                    list.AddRange(_databaseCache.GetCache().Reverse()
                        .Select(s => JObject.FromObject(s.Value))
                        .Where(selector)
                            .Select(o => o.ToObject<EntityType>()));


                if (list.Count < max && _stagingCache.Count > 0)
                    list.AddRange(_stagingCache.GetCache().Reverse()
                        .SelectMany(s => s.Value.Values.Reverse())
                            .Where (s => s != null)
                            .Where(selector)
                            .Select(o => o.ToObject<EntityType>()));

                if (list.Count < max)
                {
                    foreach (var page in _fileManager.AsReverseEnumerable())
                    {
                        list.AddRange(page.Where(o => selector(o)).Select(e => e.ToObject<EntityType>()));

                        if (list.Count >= max)
                            break;
                    }
                }

                return list.GroupBy(k => _idGet(k)).Select(f => f.First()).Take(Math.Min(list.Count, max)).ToList();
            }
            catch (Exception ex) { throw new QueryExecuteException("SelectLast Query Execution Failed.", ex); }
        }

        public IDictionary<IdType, EntityType> GetCache()
        {
            lock (_syncRoot)
            {
                lock (_syncStaging)
                {
                    var u = _databaseCache.GetCache()
                        .Union(_stagingCache.GetCache()
                        .Where(s => s.Value != null)
                        .SelectMany(s => s.Value.Select
                            (v => new KeyValuePair<IdType, EntityType>(v.Key, v.Value.ToObject<EntityType>()))));

                    return u.GroupBy(k => k.Key).ToDictionary(k => k.Key, v => v.FirstOrDefault().Value);
                }
            }
        }

        public void Dispose()
        {
            Trace.TraceInformation("Database disposing");

            if (_transactionManager != null)
                _transactionManager.CommitAmbientTransactions();

            Trace.TraceInformation("Waiting for ambient transaction to commit.");

            while (_operations.Count > 0)
            {
                Thread.Sleep(10);
                lock (_syncRoot) { }
            }

            Trace.TraceInformation("All threads have exited.");

            lock (_syncRoot)
            {
                _publish = false;

                Trace.TraceInformation("Waiting for all threads to exit.");

                while (_operations.Count > 0)
                    Thread.Sleep(100);

                Trace.TraceInformation("All threads have exited.");

                if (_publisher != null)
                    _publisher.Dispose();

                if (_subscriber != null)
                    _subscriber.Dispose();

                lock (_syncTrans)
                {
                    if (_transactionManager != null)
                    {
                        _transactionManager.TransactionCommitted -= new TransactionCommit<IdType, EntityType>(OnTransactionCommitted);

                        _transactionManager.RollBackAll(true);

                        Trace.TraceInformation("Waiting for transaction manager to complete operations.");

                        while (_transactionManager.HasActiveTransactions)
                            Thread.Sleep(100);

                        Trace.TraceInformation("Transaction manager operations complete.");

                        _transactionManager.Dispose();
                    }
                }

                if (_primaryIndex != null)
                {
                    Trace.TraceInformation("Waiting for primary index to complete operations");

                    while (_primaryIndex.FileFlushQueueActive)
                        Thread.Sleep(100);

                    Trace.TraceInformation("Primary index operations complete.");

                    _primaryIndex.Dispose();
                }

                if (_stagingCache != null)
                    lock (_stagingCache)
                        _stagingCache.Dispose();

                if (_databaseCache != null)
                    _databaseCache.Dispose();

                if (_fileManager != null)
                    lock (_syncRoot)
                        _fileManager.Dispose();
            }
        }
    }
}
