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
using System.Threading;
using System.Threading.Tasks;
using BESSy.Cache;
using BESSy.Extensions;
using BESSy.Factories;
using BESSy.Files;
using BESSy.Indexes;
using BESSy.Parallelization;
using BESSy.Queries;
using BESSy.Replication;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using BESSy.Transactions;
using BESSy.Json;
using BESSy.Json.Linq;

namespace BESSy
{

    public interface ITransactionalDatabase<IdType, EntityType> : IIndexedRepository<EntityType, IdType>, IQueryableRepository<EntityType>
    {
        Guid TransactionSource { get; }
        ITransaction<IdType, EntityType> BeginTransaction();
        void FlushAll();
        void Reorganize();

        ITransactionalDatabase<IdType, EntityType> WithPublishing(string replicationFolder);
        ITransactionalDatabase<IdType, EntityType> WithoutPublishing();
        ITransactionalDatabase<IdType, EntityType> WithSubscription(string replicationFolder, TimeSpan interval);
        ITransactionalDatabase<IdType, EntityType> WithoutSubscription();

        ITransactionalDatabase<IdType, EntityType> WithIndex<IndexType>(string name, string indexProperty, IBinConverter<IndexType> indexConverter);
        ITransactionalDatabase<IdType, EntityType> WithoutIndex<IndexType>(string name);
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
        /// <param name="segmentSeed"></param>
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
        protected object _syncIndex = new object();

        protected Stack<int> _operations = new Stack<int>();

        protected bool _disposed;
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
        protected IDictionary<string, object> _indexes = new Dictionary<string, object>();

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

            while (_operations.Contains(1))
                Thread.Sleep(100);

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
                    {
                        var seg = _primaryIndex.Fetch(a.Key);
                        if (seg > 0)
                            segs.Add(a.Key, seg);
                    }
                    else
                        segs.Add(a.Key, 0);

                Trace.TraceInformation("Transaction {0} committing with {1} segs", transaction.Id, segs.Count);

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
            object syncLocal = new object();

            var c = new Dictionary<IdType, JObject>();

            Parallel.Invoke(new System.Action[] { 
                new System.Action(delegate() {

                    foreach (var s in staging.Where(s => s.Value.Action != Action.Delete))
                        c.Add(s.Value.Id, JObject.FromObject(s.Value.Entity, _formatter.Serializer));

                    foreach (var s in staging.Where(s => s.Value.Action == Action.Delete))
                        c.Add(s.Value.Id, null);

                    lock (_syncStaging)
                        _stagingCache.UpdateCache(transaction.Id, c, true, false);
                }),
                new System.Action(delegate() {
                    SyncCache(staging);                
                })
             });

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
            {
                foreach (var a in actions)
                {
                    switch (a.Value.Action)
                    {
                        case Action.Update:
                        case Action.Delete:
                            {
                                lock (_syncStaging)
                                    _stagingCache.GetCache().Where(d => d.Value.Any(v => _idConverter.Compare(v.Key, a.Value.Id) == 0)).ToList().ForEach(n => _stagingCache.Detach(n.Key));
                                break;
                            }
                    }
                }
            }
        }

        protected virtual void AfterTransactionCommitted(IList<TransactionResult<EntityType>> results, IDisposable transaction)
        {
            _fileManager.SaveSeed<IdType>();

            Trace.TraceInformation("Database transaction post commit complete.");
        }

        protected virtual void OnReplicateReceived(ITransaction<IdType, EntityType> transaction, long timestamp)
        {
            Trace.TraceInformation("Transaction {0} replicate detected", transaction.Id);

            while (_operations.Contains(1))
                Thread.Sleep(100);

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

                Trace.TraceInformation("Transaction {0} replciating with {1} segs", transaction.Id, segs.Count);

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

        protected virtual void OnRebuilt(Guid transactionId, int newStride, int newLength, int newSeedStride)
        {
            _fileManager.SaveSeed<IdType>();

            if (newLength > _primaryIndex.Length)
                _primaryIndex.Rebuild(_primaryIndex.SegmentSeed.Stride, newLength);

            //_primaryIndex.SaveSeed<IdType>();
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
        public virtual bool FileFlushQueueActive { get { return _fileManager.FileFlushQueueActive || _primaryIndex.FileFlushQueueActive || _indexes.Values.Cast<IFlush>().Any(i => i.FileFlushQueueActive); } }
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
                                (GetIndexName(_fileName), _idToken, _idConverter, _cacheFactory, _formatter, _indexFileFactory, _rowSynchronizer);
                        else
                            _primaryIndex = _indexFactory.CreatePrimary<IdType, EntityType>
                                (GetIndexName(_fileName), _formatter, _cacheFactory, _indexFileFactory, _rowSynchronizer);

                        _primaryIndex.Load();

                        if (_createNew)
                            _fileManager = _fileFactory.Create<IdType, EntityType>(_fileName, Environment.SystemPageSize, 10240, Caching.DetermineOptimumCacheSize(_seed.Stride), _seed, _primaryIndex.SegmentSeed, _formatter, _rowSynchronizer);
                        else
                            _fileManager = _fileFactory.Create<IdType, EntityType>(_fileName, Environment.SystemPageSize, 10240, 0, _primaryIndex.SegmentSeed, _formatter, _rowSynchronizer);
                        
                        _fileManager.Load<IdType>();

                        _seed = ((ISeed<IdType>)_fileManager.Seed);
                        _idToken = _seed.IdProperty;
                        _idConverter = (IBinConverter<IdType>)_seed.IdConverter;

                        _primaryIndex.Register(_fileManager);

                        //_fileManager.SaveFailed += new SaveFailed<EntityType>(OnSaveFailed);
                        _fileManager.TransactionCommitted += new Committed<EntityType>(AfterTransactionCommitted);
                        _fileManager.Rebuilt += new Rebuild<EntityType>(OnRebuilt);
                        //_fileManager.Reorganized += new Reorganized<EntityType>(OnReorganized);

                        _databaseCache = _cacheFactory.Create<IdType, EntityType>(true, Parallelization.TaskGrouping.ArrayLimit, _idConverter);
                        _stagingCache = _cacheFactory.Create<Guid, IDictionary<IdType, JObject>>(true, Parallelization.TaskGrouping.ArrayLimit, new BinConverterGuid());

                        _idGet = (Func<EntityType, IdType>)Delegate.CreateDelegate(typeof(Func<EntityType, IdType>), typeof(EntityType).GetProperty(_idToken).GetGetMethod());
                        _idSet = (Action<EntityType, IdType>)Delegate.CreateDelegate(typeof(Action<EntityType, IdType>), typeof(EntityType).GetProperty(_idToken).GetSetMethod());

                        //is this segmentSeed a passthrough? i.e. string?
                        _passthrough = _idConverter.Compare(_seed.Peek(), default(IdType)) == 0;

                        _transactionManager.Source = _seed.Source;

                        RecentTransactions = _cacheFactory.Create<Guid, int>(true, TaskGrouping.ArrayLimit, new BinConverterGuid());

                        //Load secondary indexes.
                        foreach (var index in _indexes.Values.Cast<ILoadAndRegister<EntityType>>())
                        {
                            index.Load();
                            index.Register(_fileManager);
                        }

                        return _fileManager.Length;
                    }
                }
            }
        }

        public  virtual void Reorganize()
        {
            lock (_syncStaging)
                _stagingCache.ClearCache();

            _fileManager.Reorganize<IdType>(this._idConverter, jObject => jObject.Value<IdType>("Id"));
        }

        public virtual IdType Add(EntityType item)
        {
            lock (_syncOperations)
                _operations.Push(2);

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

        public virtual IdType AddOrUpdate(EntityType item, IdType id)
        {
            if (_idConverter.Compare(id, default(IdType)) == 0)
                return Add(item);
            else
            { Update(item, id); return id; }
        }

        public virtual void Update(EntityType item, IdType id)
        {
            var newId = _idGet(item);
            var deleteFirst = (_idConverter.Compare(newId, id) != 0);

            lock (_syncOperations)
                _operations.Push(3);

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
                _operations.Push(4);

            try
            {
                lock (_syncTrans)
                {
                    using (var tLock = _transactionManager.GetActiveTransaction(false))
                    {
                        tLock.Transaction.Enlist(Action.Delete, id, default(EntityType));
                    }
                }
            }
            finally { lock (_syncOperations) _operations.Pop(); }
        }

        public virtual EntityType Fetch(IdType id)
        {
            if (_transactionManager.HasActiveTransactions)
            {
                var t = _transactionManager.GetActiveTransaction(false);

                if (t.Transaction != null && t.Transaction.EnlistCount > 0)
                {
                    var i = t.Transaction.GetEnlistedActions().LastOrDefault(a => _idConverter.Compare(id, a.Key) == 0);

                    if (_idConverter.Compare(default(IdType), i.Key) != 0)
                        if (i.Value.Action != Action.Delete)
                            return i.Value.Entity;
                        else
                            return default(EntityType);
                }
            }
            lock (_syncStaging)
            {
                if (_stagingCache.Count > 0 &&_stagingCache.GetCache().Any(s => s.Value.ContainsKey(id)))
                {
                    var jo = _stagingCache.GetCache().Where(s => s.Value.ContainsKey(id)).Last().Value[id];

                    if (jo != null)
                        return jo.ToObject<EntityType>(_formatter.Serializer);

                    return default(EntityType);
                }
            }

            var seg = _primaryIndex.Fetch(id);

            if (seg > 0)
            {
                var entity = _fileManager.LoadSegmentFrom(seg);

                return entity;
            }

            return default(EntityType);
        }

        public virtual IList<EntityType> FetchFromIndex<IndexType>(string name, IndexType indexProperty)
        {
            if (!_indexes.ContainsKey(name))
                throw new IndexNotFoundException(string.Format("index not found '{0}'", name));

            var index = _indexes[name] as ISecondaryIndex<IndexType, EntityType, int>;

            if (index == null)
                throw new IndexNotFoundException(string.Format("index not found '{0}'", name));

            var segs = index.Fetch(indexProperty);

            var entities = new List<EntityType>();

            if (segs.Length < 0)
                return entities;

            foreach (var s in segs)
                entities.Add(_fileManager.LoadSegmentFrom(s));

            return entities;
        }

        public virtual IList<EntityType> FetchRangeFromIndexInclusive<IndexType>(string name, IndexType startProperty, IndexType endProperty)
        {
            if (!_indexes.ContainsKey(name))
                throw new IndexNotFoundException(string.Format("index not found '{0}'", name));

            var index = _indexes[name] as ISecondaryIndex<IndexType, EntityType, int>;

            if (index == null)
                throw new IndexNotFoundException(string.Format("index not found '{0}'", name));

            var segs = index.FetchBetween(startProperty, endProperty);

            var entities = new List<EntityType>();

            if (segs.Length <= 0)
                return entities;

            foreach (var seg in segs)
                entities.Add(_fileManager.LoadSegmentFrom(seg));

            return entities;
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
                                tLock.Transaction.Enlist(Action.Delete, obj.SelectToken(_idToken, true).Value<IdType>(), obj.ToObject<EntityType>(_formatter.Serializer));
                            }
                    }

                    return count;
                }
            }
            catch (Exception ex) { throw new QueryExecuteException("Delete Query Execution Failed.", ex); }
        }

        public int Update<UpdateEntityType>(Func<JObject, bool> selector, params Action<UpdateEntityType>[] updates) where UpdateEntityType : EntityType
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
                                var entity = obj.ToObject<UpdateEntityType>(_formatter.Serializer);

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

        public virtual IList<EntityType> Select(Func<JObject, bool> selector)
        {
            try
            {
                var list = new List<EntityType>();

                lock (_syncStaging)
                    if (_stagingCache.Count > 0)
                        list.AddRange(_stagingCache.GetCache()
                            .SelectMany(s => s.Value.Values)
                                .Where(s => s != null)
                                .Where(selector)
                                .Select(o => o.ToObject<EntityType>(_formatter.Serializer)));

                foreach (var page in _fileManager.AsEnumerable())
                    foreach (var obj in page.Where(o => selector(o)))
                        list.Add(obj.ToObject<EntityType>(_formatter.Serializer));

                return list.GroupBy(k => _idGet(k)).Select(f => f.First()).ToList();
            }
            catch (Exception ex) { throw new QueryExecuteException("Select Query Execution Failed.", ex); }
        }

        public virtual IList<EntityType> SelectFirst(Func<JObject, bool> selector, int max)
        {
            try
            {
                var list = new List<EntityType>();

                lock (_syncStaging)
                    if (list.Count < max && _stagingCache.Count > 0)
                            list.AddRange(_stagingCache.GetCache()
                                .SelectMany(s => s.Value.Values)
                                    .Where(s => s != null)
                                    .Where(selector)
                                    .Select(o => o.ToObject<EntityType>(_formatter.Serializer)));

                if (list.Count < max)
                {
                    foreach (var page in _fileManager.AsEnumerable())
                    {
                        list.AddRange(page.Where(o => selector(o)).Select(e => e.ToObject<EntityType>(_formatter.Serializer)));

                        if (list.Count >= max)
                            break;
                    }
                }

                return list.GroupBy(k => _idGet(k)).Select(f => f.First()).Take(Math.Min(list.Count, max)).ToList();
            }
            catch (Exception ex) { throw new QueryExecuteException("SelectFirst Query Execution Failed.", ex); }
        }

        public virtual IList<EntityType> SelectLast(Func<JObject, bool> selector, int max)
        {
            try
            {
                var list = new List<EntityType>();

                lock (_syncStaging)
                    if (list.Count < max && _stagingCache.Count > 0)
                        list.AddRange(_stagingCache.GetCache().Reverse()
                            .SelectMany(s => s.Value.Values.Reverse())
                                .Where (s => s != null)
                                .Where(selector)
                                .Select(o => o.ToObject<EntityType>(_formatter.Serializer)));

                if (list.Count < max)
                {
                    foreach (var page in _fileManager.AsReverseEnumerable())
                    {
                        list.AddRange(page.Where(o => selector(o)).Select(e => e.ToObject<EntityType>(_formatter.Serializer)));

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
                    var u = 
                        _stagingCache.GetCache()
                        .Where(s => s.Value != null)
                        .SelectMany(s => s.Value.Select
                            (v => new KeyValuePair<IdType, EntityType>(v.Key, v.Value.ToObject<EntityType>(_formatter.Serializer))));
                    

                    return u.GroupBy(k => k.Key).ToDictionary(k => k.Key, v => v.FirstOrDefault().Value);
                }
            }
        }

        public ITransactionalDatabase<IdType, EntityType> WithPublishing(string replicationFolder)
        {
            var replication = new Publisher<IdType, EntityType>(this, replicationFolder);

            this.InitilizeReplicationPublishing(replication);

            return this;
        }

        public ITransactionalDatabase<IdType, EntityType> WithoutPublishing()
        {
            this.InitilizeReplicationPublishing(null);

            return this;
        }

        public ITransactionalDatabase<IdType, EntityType> WithSubscription(string replicationFolder, TimeSpan interval)
        {
            var replication = new Subscriber<IdType, EntityType>(this, replicationFolder, interval);

            this.InitilizeReplicationSubscription(replication);

            return this;
        }

        public ITransactionalDatabase<IdType, EntityType>  WithoutSubscription()
        {
            this.InitilizeReplicationSubscription(null);

            return this;
        }

        public ITransactionalDatabase<IdType, EntityType> WithIndex<IndexType>(string name, string indexProperty, IBinConverter<IndexType> indexConverter)
        {
            try
            {
                if (_indexes.ContainsKey(name))
                    throw new DuplicateKeyException("Index with this name already exists.");

                var index = _indexFactory.CreateSecondary<IndexType, EntityType>
                    (GetIndexName(_fileName + "." + name)
                    , indexProperty
                    , _formatter
                    , indexConverter
                    , _cacheFactory
                    , _indexFileFactory
                    , new RowSynchronizer<int>(new BinConverter32()));

                _indexes.Add(name, index);

            }
            catch (Exception ex)
            {
                Trace.TraceError("Error initializing secondary index: {0}, property: {1}", name, indexProperty);
                Trace.TraceError(ex.ToString());

                throw;
            }
            return this;
        }

        public ITransactionalDatabase<IdType, EntityType> WithoutIndex<IndexType>(string name)
        {
            try
            {
                if (!_indexes.ContainsKey(name))
                    return this;

                lock (_syncIndex)
                {
                    var index = _indexes[name] as IIndex<IndexType, EntityType, int>;

                    if (index != null)
                        index.Dispose();

                    _indexes.Remove(name);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error unloading index: {0}", name);
                Trace.TraceError(ex.ToString());
            }
            return this;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

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

                if (_indexes != null && _indexes.Count > 0)
                {
                    foreach (var index in _indexes.Values)
                    {
                        Trace.TraceInformation("Waiting for secondary index to complete operations");

                        var disposable = index as IDisposable;

                        var flushable = index as IFlush;

                        while (flushable.FileFlushQueueActive)
                            Thread.Sleep(100);

                        if (disposable != null)
                            disposable.Dispose();

                        Trace.TraceInformation("Secondary index operations complete.");
                    }
                }

                if (_stagingCache != null)
                    lock (_stagingCache)
                        _stagingCache.Dispose();

                if (_databaseCache != null)
                    _databaseCache.Dispose();

                if (_fileManager != null)
                    lock (_syncRoot)
                        _fileManager.Dispose();

                _disposed = true;
            }
        }
    }
}

