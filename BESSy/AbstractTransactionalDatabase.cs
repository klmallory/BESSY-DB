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
    public interface IConfigureDatabase
    {
        void AddIndex(string command);
        void RemoveIndex(string name);

        void WithPublisher(string command);
        void WithoutPublisher(string name);
        void WithSubscriber(string command);
        void WithoutSubscriber(string name);
    }

    public interface IFluentlyConfigure<IdType, EntityType>
    {
        ITransactionalDatabase<IdType, EntityType> WithIndex<IndexType>(string name, string indexProperty, IBinConverter<IndexType> indexConverter);
        ITransactionalDatabase<IdType, EntityType> WithoutIndex(string name);
        ITransactionalDatabase<IdType, EntityType> WithoutPublishing(string name);
        ITransactionalDatabase<IdType, EntityType> WithoutSubscription(string name);
        ITransactionalDatabase<IdType, EntityType> WithPublishing(string name, IReplicationPublisher<IdType, EntityType> replication);
        ITransactionalDatabase<IdType, EntityType> WithSubscription(string name, IReplicationSubscriber<IdType, EntityType> replication);
    }

    public interface IIndexedRepository<T, I> : IRepository<T, I>, IIndexedReadOnlyRepository<T, I>
    {

    }

    public interface IIndexedReadOnlyRepository<T, I> : IReadOnlyRepository<T, I>, IFlush, ILoadAndDispose, IConfigureDatabase
    {
        IDictionary<I, T> GetCache();
        IList<T> FetchFromIndex<IndexType>(string name, IndexType indexProperty);
        IList<T> FetchRangeFromIndexInclusive<IndexType>(string name, IndexType startIndex, IndexType endIndex);
    }

    public interface IStreamingRepository: ILoadAndDispose
    {
        IEnumerable<Stream> AsStreaming();
    }

    public interface ITransactionalDatabase<IdType, EntityType> : 
        IIndexedRepository<EntityType, IdType>, 
        IServerRepository<IdType, EntityType>, 
        IFluentlyConfigure<IdType, EntityType>, 
        IQueryableRepository<EntityType>, 
        IQueryableServerRepository<EntityType>, 
        IStreamingRepository 
    {
        IQueryableFormatter Formatter { get; }
        Guid TransactionSource { get; }
        ITransaction BeginTransaction();
        void FlushAll();
        void Reorganize();

        //IList<EntityType> FetchRangeFromIndexInclusive<IndexType>(string indexName, IndexType startIndex, IndexType endINdex);
    }

    /// <summary>
    /// Abstract implementation for a fully transactional and queryable database.
    /// </summary>
    public partial class AbstractTransactionalDatabase<IdType, EntityType> : ITransactionalDatabase<IdType, EntityType>
    {
        protected AbstractTransactionalDatabase(string fileName)
        {
            _fileName = fileName;
        }

        /// <summary>
        /// Opens an existing database with the specified paramters.
        /// </summary>
        /// <param property="fileName"></param>
        /// <param property="transactionManager"></param>
        /// <param property="databaseFileFactory"></param>
        /// <param property="cacheFactory"></param>
        /// <param property="cacheFactory"></param>
        /// <param property="cacheFactory"></param>
        /// <param property="rowSynchronizer"></param>
        public AbstractTransactionalDatabase(
            string fileName
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, EntityType> transactionManager
            , IAtomicFileManagerFactory databaseFileFactory
            , IDatabaseCacheFactory cacheFactory
            , IIndexFileFactory indexFileFactory
            , IIndexFactory indexFactory
            , IRowSynchronizer<long> rowSynchronizer)
        {
            _fileName = fileName;
            Formatter = formatter;
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
        /// <param property="fileName"></param>
        /// <param property="idToken"></param>
        /// <param property="segmentSeed"></param>
        /// <param property="IdConverter"></param>
        /// <param property="formatter"></param>
        /// <param property="transactionManager"></param>
        /// <param property="databaseFileFactory"></param>
        /// <param property="cacheFactory"></param>
        /// <param property="cacheFactory"></param>
        /// <param property="cacheFactory"></param>
        /// <param property="rowSynchronizer"></param>
        public AbstractTransactionalDatabase(
            string fileName
            , string idToken
            , IFileCore<IdType, long> core
            , IBinConverter<IdType> idConverter
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, EntityType> transactionManager
            , IAtomicFileManagerFactory databaseFileFactory
            , IDatabaseCacheFactory cacheFactory
            , IIndexFileFactory indexFileFactory
            , IIndexFactory indexFactory
            , IRowSynchronizer<long> rowSynchronizer)
        {
            _createNew = true;

            _fileName = fileName;
            _idToken = idToken;
            _core = core;
            _idConverter = idConverter;
            Formatter = formatter;
            _transactionManager = transactionManager;
            _indexFactory = indexFactory;
            _cacheFactory = cacheFactory;
            _fileFactory = databaseFileFactory;
            _indexFileFactory = indexFileFactory;
            _rowSynchronizer = rowSynchronizer;

            _core.IdConverter = idConverter;
            _core.IdProperty = idToken;

            _transactionManager.TransactionCommitted += new TransactionCommit<IdType, EntityType>(OnTransactionCommitted);
        }

        protected object _syncTrans = new object();
        protected object _syncOperations = new object();
       
        protected Stack<int> _operations = new Stack<int>();

        protected bool _disposed;
        protected bool _publish;
        protected bool _createNew; 

        protected string _idToken;
        protected IBinConverter<IdType> _idConverter;
        protected IFileCore<IdType, long> _core;
        protected ITransaction<IdType, EntityType> _activeTransaction;
        protected ITransactionManager<IdType, EntityType> _transactionManager;
        protected IAtomicFileManager<EntityType> _fileManager;
        
        protected IDatabaseCache<IdType, EntityType> _databaseCache;
        protected IDatabaseCache<Guid, IDictionary<IdType, JObject>> _stagingCache;
        protected IRowSynchronizer<long> _rowSynchronizer;

        protected IIndex<IdType, EntityType, long> _primaryIndex;
        
        protected IDatabaseCacheFactory _cacheFactory;
        protected IAtomicFileManagerFactory _fileFactory;
        protected IIndexFileFactory _indexFileFactory;
        
        protected Func<EntityType, IdType> _idGet;
        protected Action<EntityType, IdType> _idSet;

        protected internal string _fileName;
        protected internal object _syncRoot = new object();
        protected internal object _syncIndex = new object();
        protected internal IIndexFactory _indexFactory;
        protected internal IDictionary<string, object> _indexes = new Dictionary<string, object>();
        protected internal IDictionary<string, IReplicationPublisher<IdType, EntityType>> _publishers = new Dictionary<string, IReplicationPublisher<IdType, EntityType>>();
        protected internal IDictionary<string, IReplicationSubscriber<IdType, EntityType>> _subscribers = new Dictionary<string, IReplicationSubscriber<IdType, EntityType>>();

        protected internal virtual string GetIndexName(string fileName)
        {
            return fileName + ".index";
        }

        protected virtual void OnTransactionCommitted(ITransaction<IdType, EntityType> transaction)
        {
            Trace.TraceInformation("Transaction {0} commit detected", transaction.Id);

            while (_operations.Contains(1))
                Thread.Sleep(100);

            lock (_syncOperations)
                _operations.Push(1);

            try
            {
                var segs = new Dictionary<IdType, long>();

                var staging = transaction.GetEnlistedActions();

                if (staging.Count <= 0)
                    return;

                foreach (var a in staging)
                    if (a.Value.Action != Action.Create)
                    {
                        var seg = _primaryIndex.FetchSegment(a.Key);
                        if (seg > 0)
                            segs.Add(a.Key, seg);
                    }
                    else
                        segs.Add(a.Key, 0);

                Trace.TraceInformation("Transaction {0} committing with {1} segments", transaction.Id, segs.Count);

                var commitState = new Tuple<ITransaction<IdType, EntityType>, IDictionary<IdType, long>>(transaction, segs);

                Parallel.Invoke(new System.Action[] 
                { 
                    new System.Action(delegate() { 
                        UpdateStaging(transaction, staging); 
                    }), 
                    new System.Action(delegate() { 
                        CommitTransactionToFile(transaction, segs); 
                    })
                });

                if (_publishers.Count > 0)
                    Parallel.ForEach(_publishers, new Action<KeyValuePair<string, IReplicationPublisher<IdType, EntityType>>>(delegate(KeyValuePair<string, IReplicationPublisher<IdType, EntityType>> pair)
                    {
                        pair.Value.Publish(transaction);
                    }));
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

        protected void UpdateStaging(ITransaction<IdType, EntityType> transaction, IDictionary<IdType, EnlistedAction<EntityType>> staging)
        {
            object syncLocal = new object();

            var c = new Dictionary<IdType, JObject>();

            lock (syncLocal)
                foreach (var s in staging.Where(s => s.Value.Action != Action.Delete))
                    c.Add(s.Key, JObject.FromObject(s.Value.Entity, Formatter.Serializer));

            lock (syncLocal)
                foreach (var s in staging.Where(s => s.Value.Action == Action.Delete))
                    c.Add(s.Key, null);

            lock (_stagingCache)
                _stagingCache.UpdateCache(transaction.Id, c, true, false);

            SyncCache(staging);

            Trace.TraceInformation("Transaction {0} update staging thread complete", transaction.Id);
        }

        protected virtual void CommitTransactionToFile(ITransaction<IdType, EntityType> transaction, IDictionary<IdType, long> segments)
        {
            if (transaction == null)
                return;

            _fileManager.CommitTransaction(transaction, segments);

            RecentTransactions.UpdateCache(transaction.Id, 0, true, false);

            Trace.TraceInformation("Transaction {0} commit thread complete", transaction.Id);
        }

        protected virtual void SyncCache(IDictionary<IdType, EnlistedAction<EntityType>> actions)
        {

            foreach (var a in actions)
            {
                switch (a.Value.Action)
                {
                    case Action.Update:
                    case Action.Delete:
                        {
                            lock (_stagingCache)
                                _stagingCache.GetCache().Where(d => d.Value.Any(v => _idConverter.Compare(v.Key, a.Key) == 0)).ToList().ForEach(n => _stagingCache.Detach(n.Key));
                            break;
                        }
                }
            }
        }

        protected virtual void AfterTransactionCommitted(ITransaction<EntityType> transaction)
        {
            _fileManager.SaveCore<IdType>();

            Trace.TraceInformation("Database trans post commit complete.");
        }

        protected internal virtual void OnReplicateReceived(ITransaction<IdType, EntityType> transaction, long timestamp)
        {
            Trace.TraceInformation("Transaction {0} replicate detected", transaction.Id);

            while (_operations.Contains(1))
                Thread.Sleep(100);

            lock (_syncOperations)
                _operations.Push(1);

            try
            {
                var segs = new Dictionary<IdType, long>();

                var staging = transaction.GetEnlistedActions();

                foreach (var a in staging)
                    if (a.Value.Action == Action.Delete)
                    {
                        var seg = _primaryIndex.FetchSegment(a.Key);
                        if (seg > 0)
                            segs.Add(a.Key, seg);
                        else
                            segs.Add(a.Key, -1);
                    }
                    else
                    {
                        if (_idConverter.Compare(a.Key, default(IdType)) != 0)
                            segs.Add(a.Key, _primaryIndex.FetchSegment(a.Key));
                        else
                            segs.Add(a.Key, 0);
                    }

                Trace.TraceInformation("Transaction {0} replciating with {1} segs", transaction.Id, segs.Count);

                var commitState = new Tuple<ITransaction<IdType, EntityType>, IDictionary<IdType, long>>(transaction, segs);

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

            _core.LastReplicatedTimeStamp = timestamp;
        }

        protected virtual void OnRebuilt(Guid transactionId, int newStride, long newLength, int newSeedStride)
        {
            _fileManager.SaveCore<IdType>();

            if (newLength > _primaryIndex.Length)
                _primaryIndex.Rebuild(newLength);

            //_primaryIndex.SaveCore<IdType>();
        }

        protected bool IsPresent(IdType id)
        {
            if (_transactionManager.HasActiveTransactions)
            {
                using (var t = _transactionManager.GetActiveTransaction(false))
                {
                    if (t.Transaction != null && t.Transaction.EnlistCount > 0)
                    {
                        var i = t.Transaction.GetEnlistedActions().LastOrDefault(a => _idConverter.Compare(id, a.Key) == 0);

                        if (_idConverter.Compare(default(IdType), i.Key) != 0)
                            if (i.Value.Action != Action.Delete)
                                return true;
                            else
                                return false;
                    }
                }
            }
            lock (_stagingCache)
            {
                if (_stagingCache.Count > 0 && _stagingCache.GetCache().Any(s => s.Value.ContainsKey(id)))
                {
                    var jo = _stagingCache.GetCache().Where(s => s.Value.ContainsKey(id)).Last().Value[id];

                    if (jo != null)
                        return true;

                    return false;
                }
            }

            return _primaryIndex.FetchSegment(id) > 0;
        }
        
        public virtual long LastReplicatedTimeStamp { get { return _core.LastReplicatedTimeStamp; } }
        public virtual bool FileFlushQueueActive { get { return _fileManager.FileFlushQueueActive || _primaryIndex.FileFlushQueueActive || _indexes.Values.Cast<IFlush>().Any(i => i.FileFlushQueueActive); } }
        public virtual long Length { get { return _fileManager.Length; } }
        public IDatabaseCache<Guid, int> RecentTransactions { get; protected set; }
        public IQueryableFormatter Formatter { get; protected set; }
        public Guid TransactionSource { get { return _core.Source; } }
        public virtual bool AutoCommit { get; set; }
        public ISeed<IdType> Seed { get { return _core.IdSeed; } }

        public virtual ITransaction BeginTransaction()
        {
            var tLock = _transactionManager.BeginTransaction();

            return tLock.Transaction;
        }

        public virtual long Load()
        {
            Trace.TraceInformation("Database loading");

            lock (_syncRoot)
            {
                lock (_syncTrans)
                {
                    if (_createNew)
                        _fileManager = _fileFactory.Create<IdType, EntityType>(_fileName, Environment.SystemPageSize, (int)_core.InitialDbSize, Caching.DetermineOptimumCacheSize(_core.Stride), _core,  Formatter, _rowSynchronizer);
                    else
                        _fileManager = _fileFactory.Create<IdType, EntityType>(_fileName, Environment.SystemPageSize, 10240, 0, Formatter, _rowSynchronizer);

                    _fileManager.Load<IdType>();

                    _core = ((IFileCore<IdType, long>)_fileManager.Core);
                    _idToken = _core.IdProperty;
                    _idConverter = (IBinConverter<IdType>)_core.IdConverter;

                    InitIdMethods();

                    InitializePrimaryIndex();

                    //_fileManager.SaveFailed += new SaveFailed<EntityType>(OnSaveFailed);
                    _fileManager.TransactionCommitted += new Committed<EntityType>(AfterTransactionCommitted);
                    _fileManager.Rebuilt += new Rebuild<EntityType>(OnRebuilt);
                    //_fileManager.Reorganized += new Reorganized<EntityType>(OnReorganized);

                    _databaseCache = _cacheFactory.Create<IdType, EntityType>(true, Parallelization.TaskGrouping.ArrayLimit, _idConverter);
                    _stagingCache = _cacheFactory.Create<Guid, IDictionary<IdType, JObject>>(true, Parallelization.TaskGrouping.ArrayLimit, new BinConverterGuid());

                    //is this segmentSeed a passthrough? jObj.e. string?
                    // _passthrough = IdConverter.Compare(_core.Peek(), default(IdType)) == 0;

                    _transactionManager.Source = _core.Source;

                    RecentTransactions = _cacheFactory.Create<Guid, int>(true, TaskGrouping.ArrayLimit, new BinConverterGuid());

                    //Load secondary segments.
                    foreach (var index in _indexes.Values.Cast<ILoadAndRegister<EntityType>>())
                    {
                        index.Load();
                        index.Register(_fileManager);
                    }

                    return _fileManager.Length;
                }
            }
        }

        protected virtual void InitializePrimaryIndex()
        {
            _primaryIndex = _indexFactory.Create<IdType, EntityType, long>
                (GetIndexName(_fileName), _idToken, true, 1024, _idConverter, new BinConverter64(), _rowSynchronizer, new RowSynchronizer<int>(new BinConverter32()));

            _primaryIndex.Load();

            _primaryIndex.Register(_fileManager);
        }

        protected virtual void InitIdMethods()
        {
            _idGet = (Func<EntityType, IdType>)Delegate.CreateDelegate(typeof(Func<EntityType, IdType>), typeof(EntityType).GetProperty(_idToken).GetGetMethod());
            _idSet = (Action<EntityType, IdType>)Delegate.CreateDelegate(typeof(Action<EntityType, IdType>), typeof(EntityType).GetProperty(_idToken).GetSetMethod());
        }

        public  virtual void Reorganize()
        {
            lock (_stagingCache)
                _stagingCache.ClearCache();

            _fileManager.Reorganize<IdType>(this._idConverter, jObject => jObject.Value<IdType>("Id"));
        }

        public virtual IdType Add(EntityType item)
        {
            lock (_syncOperations)
                _operations.Push(2);

            try
            {
                var id = GetSeededId(item);

                using (var tLock = _transactionManager.GetActiveTransaction(false))
                {
                    tLock.Transaction.Enlist(Action.Create, id, item);

                    return id;
                }
            }
            finally { lock (_syncOperations) _operations.Pop(); }
        }

        protected virtual IdType GetSeededId(EntityType item)
        {
            var id = Seed.Increment();

            if (!Seed.Passive)
            {
                if (_idConverter.Compare(_idGet(item), default(IdType)) != 0)
                    throw new DuplicateKeyException("Id was already set on this object, you can only set the primary id of a new object with a passive core");

                _idSet(item, id);
            }
            else
                id = _idGet(item);

            return id;
        }

        public virtual IdType AddOrUpdate(EntityType item)
        {
            if (_idConverter.Compare(_idGet(item), default(IdType)) == 0)
                return Add(item);
            else
            { Update(item); return _idGet(item); }
        }

        public virtual IdType AddOrUpdate(EntityType item, IdType id)
        {
            if (_idConverter.Compare(id, default(IdType)) == 0)
                return Add(item);
            else
            { Update(item, id); return id; }
        }

        public virtual void Update(EntityType item)
        {
            if (object.Equals(item, default(EntityType)))
                throw new ArgumentException("item to be updated was null or empty. Use delete command if this was intended.");

            var id = _idGet(item);

            lock (_syncOperations)
                _operations.Push(3);

            try
            {
                using (var tLock = _transactionManager.GetActiveTransaction(false))
                {
                    if (!IsPresent(id))
                        throw new KeyNotFoundException(string.Format("Could not find entity with id {0}", id));

                    tLock.Transaction.Enlist(Action.Update, id, item);
                }
            }
            finally { lock (_syncOperations) _operations.Pop(); }
        }

        public virtual void Update(EntityType item, IdType id)
        {
            if (_idConverter.Compare(id, default(IdType)) == 0)
                throw new ArgumentException("id was null or empty.");

            var newId = _idGet(item);
            var deleteFirst = (_idConverter.Compare(newId, id) != 0);

            lock (_syncOperations)
                _operations.Push(3);

            try
            {
                using (var tLock = _transactionManager.GetActiveTransaction(false))
                {
                    if (!IsPresent(id))
                        throw new KeyNotFoundException(string.Format("Could not find entity with id {0}", id));

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
            if (_idConverter.Compare(id, default(IdType)) == 0)
                throw new ArgumentException("id was null or empty.");

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

        public virtual void Delete(IEnumerable<IdType> ids)
        {
            if (ids == null)
                throw new ArgumentNullException();

            lock (_syncOperations)
                _operations.Push(4);

            try
            {
                lock (_syncTrans)
                {
                    using (var tLock = _transactionManager.GetActiveTransaction(false))
                    {
                        foreach(var id in ids)
                            tLock.Transaction.Enlist(Action.Delete, id, default(EntityType));
                    }
                }
            }
            finally { lock (_syncOperations) _operations.Pop(); }
        }

        public virtual EntityType Fetch(IdType id)
        {
            if (_idConverter.Compare(id, default(IdType)) == 0)
                throw new ArgumentException("id was null or empty.");

            if (_transactionManager.HasActiveTransactions)
            {
                using (var t = _transactionManager.GetActiveTransaction(false))
                {
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
            }
            lock (_stagingCache)
            {
                if (_stagingCache.Count > 0 &&_stagingCache.GetCache().Any(s => s.Value.ContainsKey(id)))
                {
                    var jo = _stagingCache.GetCache().Where(s => s.Value.ContainsKey(id)).Last().Value[id];

                    if (jo != null)
                        return jo.ToObject<EntityType>(Formatter.Serializer);

                    return default(EntityType);
                }
            }

            var seg = _primaryIndex.FetchSegment(id);

            if (seg > 0)
            {
                var entity = LoadFromFile(seg);

                return entity;
            }

            return default(EntityType);
        }

        protected virtual EntityType LoadFromFile(long seg)
        {
            return  _fileManager.LoadSegmentFrom(seg);
        }

        public virtual IList<EntityType> FetchFromIndex<IndexType>(string name, IndexType indexProperty)
        {
            if (!_indexes.ContainsKey(name))
                throw new IndexNotFoundException(string.Format("indexUpdate not found '{0}'", name));

            var index = _indexes[name] as IIndex<IndexType, EntityType, long>;

            if (index == null)
                throw new IndexNotFoundException(string.Format("indexUpdate not found '{0}'", name));

            var segs = index.FetchSegments(indexProperty);

            var entities = new List<EntityType>();

            if (segs.Length < 0)
                return entities;

            foreach (var s in segs)
                entities.Add(_fileManager.LoadSegmentFrom(s));

            return entities;
        }

        public IList<EntityType> FetchRangeFromIndexInclusive<IndexType>(string name, IndexType startIndex, IndexType endIndex)
        {
            if (!_indexes.ContainsKey(name))
                throw new IndexNotFoundException(string.Format("indexUpdate not found '{0}'", name));

            var index = _indexes[name] as IIndex<IndexType, EntityType, long>;

            if (index == null)
                throw new IndexNotFoundException(string.Format("indexUpdate not found '{0}'", name));

            var segs = index.FetchSegments(startIndex, endIndex);

            var entities = new List<EntityType>();

            if (segs.Length < 0)
                return entities;

            foreach (var s in segs)
                entities.Add(_fileManager.LoadSegmentFrom(s));

            return entities;
        }

        public virtual void Clear()
        {
            lock (_syncTrans)
                _transactionManager.RollBackAll(true);

            lock (_syncRoot)
                _databaseCache.ClearCache();

            lock (_stagingCache)
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
                    if (_transactionManager.CurrentTransaction != null)
                        using (var tLock = _transactionManager.GetActiveTransaction(false))
                            if (tLock.Transaction.EnlistCount > 0)
                                foreach (var jObj in tLock.Transaction.GetEnlistedItems()
                                    .Select(s => Formatter.AsQueryableObj(s))
                                        .Where(s => s != null)
                                        .Where(selector)
                                        .ToList())
                                {
                                    tLock.Transaction.Enlist(Action.Delete, jObj.SelectToken(_idToken, true).Value<IdType>(), default(EntityType));
                                    count++;
                                }

                    using (var tLock = _transactionManager.GetActiveTransaction(false))
                    {
                        foreach (var page in _fileManager.AsEnumerable())
                            foreach (var obj in page.Where(o => selector(o)))
                            {
                                count++;
                                tLock.Transaction.Enlist(Action.Delete, obj.SelectToken(_idToken, true).Value<IdType>(), obj.ToObject<EntityType>(Formatter.Serializer));
                            }
                    }

                    return count;
                }
            }
            catch (Exception ex) { throw new QueryExecuteException("Delete Query Execution Failed.", ex); }
        }

        public virtual int DeleteFirst(Func<JObject, bool> selector, int max)
        {
            try
            {
                int count = 0;

                lock (_syncTrans)
                {
                    if (_transactionManager.CurrentTransaction != null)
                        using (var tLock = _transactionManager.GetActiveTransaction(false))
                            if (tLock.Transaction.EnlistCount > 0)
                                foreach (var jObj in tLock.Transaction.GetEnlistedItems()
                                     .Where(s => s != null)
                                    .Select(s => Formatter.AsQueryableObj(s))
                                    .Where(selector)
                                    .ToList())
                                {
                                    if (count >= max)
                                        break;

                                    tLock.Transaction.Enlist(Action.Delete, jObj.SelectToken(_idToken, true).Value<IdType>(), default(EntityType));
                                    count++;
                                }

                    using (var tLock = _transactionManager.GetActiveTransaction(false))
                    {
                        foreach (var page in _fileManager.AsEnumerable())
                            foreach (var obj in page.Where(o => selector(o)))
                            {
                                if (count >= max)
                                    return count;

                                count++;
                                tLock.Transaction.Enlist(Action.Delete, obj.SelectToken(_idToken, true).Value<IdType>(), obj.ToObject<EntityType>(Formatter.Serializer));
                            }
                    }

                    return count;
                }
            }
            catch (Exception ex) { throw new QueryExecuteException("Delete Query Execution Failed.", ex); }
        }

        public virtual int DeleteLast(Func<JObject, bool> selector, int max)
        {
            try
            {
                int count = 0;

                lock (_syncTrans)
                {
                    if (_transactionManager.CurrentTransaction != null)
                        using (var tLock = _transactionManager.GetActiveTransaction(false))
                            if (tLock.Transaction.EnlistCount > 0)
                                foreach (var jObj in tLock.Transaction.GetEnlistedItems()
                                    .Where(s => s != null)
                                    .Select(s => Formatter.AsQueryableObj(s))
                                    .Where(selector)
                                    .Reverse().ToList())
                                {
                                    if (count >= max)
                                        break;

                                    tLock.Transaction.Enlist(Action.Delete, jObj.SelectToken(_idToken, true).Value<IdType>(), default(EntityType));
                                    count++;
                                }

                    using (var tLock = _transactionManager.GetActiveTransaction(false))
                    {
                        foreach (var page in _fileManager.AsReverseEnumerable())
                            foreach (var obj in page.Where(o => selector(o)))
                            {
                                if (count >= max)
                                    return count;

                                count++;
                                tLock.Transaction.Enlist(Action.Delete, obj.SelectToken(_idToken, true).Value<IdType>(), obj.ToObject<EntityType>(Formatter.Serializer));
                            }
                    }

                    return count;
                }
            }
            catch (Exception ex) { throw new QueryExecuteException("Delete Query Execution Failed.", ex); }
        }

        public int Update<UpdateEntityType>(UpdateEntityType entity, Func<JObject, bool> selector, params Action<UpdateEntityType>[] updates) where UpdateEntityType : EntityType
        {
            return Update<UpdateEntityType>(selector, updates);
        }

        public int Update<UpdateEntityType>(Func<JObject, bool> selector, params Action<UpdateEntityType>[] updates) where UpdateEntityType : EntityType
        {
            try
            {
                var count = 0;
                lock (_syncTrans)
                {
                    if (_transactionManager.CurrentTransaction != null)
                        using (var tLock = _transactionManager.GetActiveTransaction(false))
                            if (tLock.Transaction.EnlistCount > 0)
                                foreach (var jObj in tLock.Transaction.GetEnlistedItems()
                                    .Where(s => s != null)
                                    .Select(s => Formatter.AsQueryableObj(s))
                                    .Where(selector)
                                    .ToList())
                                {
                                    var entity = jObj.ToObject<UpdateEntityType>();

                                    foreach (var action in updates)
                                        action.Invoke(entity);

                                    tLock.Transaction.Enlist(Action.Update, jObj.SelectToken(_idToken, true).Value<IdType>(), entity);
                                    count++;
                                }
                }
                using (var tLock = _transactionManager.GetActiveTransaction(false))
                {
                    foreach (var page in _fileManager.AsEnumerable())
                    {
                        foreach (var obj in page.Where(o => selector(o)))
                        {
                            var entity = obj.ToObject<UpdateEntityType>(Formatter.Serializer);

                            foreach (var action in updates)
                                action.Invoke(entity);

                            tLock.Transaction.Enlist(Action.Update, obj.SelectToken(_idToken, true).Value<IdType>(), entity);
                            count++;
                        }
                    }
                }

                return count;
            }
            catch (Exception ex) { throw new QueryExecuteException("Update Query Execution Failed.", ex); }
        }

        public virtual IList<EntityType> Select(Func<JObject, bool> selector)
        {
            try
            {
                return SelectJObj(selector).Select(o => o.ToObject<EntityType>(Formatter.Serializer)).ToList();
            }
            catch (Exception ex) { throw new QueryExecuteException("Select Query Execution Failed.", ex); }
        }

        public virtual IList<EntityType> SelectFirst(Func<JObject, bool> selector, int max)
        {      
            try
            {
                return SelectJObjFirst(selector, max).Select(o => o.ToObject<EntityType>(Formatter.Serializer)).ToList();
            }
            catch (Exception ex) { throw new QueryExecuteException("SelectFirst Query Execution Failed.", ex); }
        }

        public virtual IList<EntityType> SelectLast(Func<JObject, bool> selector, int max)
        {
            try
            {
                return SelectJObjLast(selector, max).Select(o => o.ToObject<EntityType>(Formatter.Serializer)).ToList();
            }
            catch (Exception ex) { throw new QueryExecuteException("SelectLast Query Execution Failed.", ex); }
        }

        public virtual IList<JObject> SelectScalar(Func<JObject, bool> selector, params string[] tokens)
        {
            var tnks = tokens.ToList();

            var values = new Dictionary<IdType, JObject>();

            try
            {
                if (_transactionManager.CurrentTransaction != null)
                    using (var tLock = _transactionManager.GetActiveTransaction(false))
                        if (tLock.Transaction.EnlistCount > 0)
                            foreach (var i in tLock.Transaction.GetEnlistedItems()
                                .Where(s => s != null)
                                .Select(s => Formatter.AsQueryableObj(s))
                                .Where(selector))
                            {
                                var id = i.SelectToken(_idToken).Value<IdType>();
                                if (!values.ContainsKey(id))
                                {
                                    var jObj = new JObject();
                                    tnks.ForEach(m => jObj.Add(m, i.SelectToken(m)));
                                    values.Add(id, jObj);
                                }
                            }

                lock (_stagingCache)
                    if (_stagingCache.Count > 0)
                        foreach (var i in _stagingCache.GetCache()
                            .Where(s => s.Value != null)
                            .SelectMany(s => s.Value.Values)
                                .Where(v => v != null)
                                .Where(selector))
                        {
                            var id = i.SelectToken(_idToken).Value<IdType>();
                            if (!values.ContainsKey(id))
                            {
                                var jObj = new JObject();
                                tnks.ForEach(m => jObj.Add(m, i.SelectToken(m)));
                                values.Add(id, jObj);
                            }
                        }


                foreach (var page in _fileManager.AsEnumerable())
                {
                    foreach (var i in page.Where(p => selector(p)))
                    {
                        var id = i.SelectToken(_idToken).Value<IdType>();

                        if (!values.ContainsKey(id))
                        {
                            var jObj = new JObject();
                            tnks.ForEach(m => jObj.Add(m, i.SelectToken(m)));
                            values.Add(id, jObj);
                        }
                    }
                }

                return values.Values.ToList();
            }
            catch (Exception ex) { throw new QueryExecuteException("Select Scalar Query Execution Failed.", ex); }
        }

        public virtual IList<JObject> SelectScalarFirst(Func<JObject, bool> selector, int max, params string[] tokens)
        {
            var values = new Dictionary<IdType, JObject>();
            var tnks = tokens.ToList();

            try
            {
                if (_transactionManager.CurrentTransaction != null)
                    using (var tLock = _transactionManager.GetActiveTransaction(false))
                        if (values.Count < max && tLock.Transaction.EnlistCount > 0)
                            foreach (var i in tLock.Transaction.GetEnlistedItems()
                                .Where(s => s != null)
                                .Select(s => Formatter.AsQueryableObj(s))
                                .Where(selector))
                            {
                                if (values.Count >= max)
                                    break;

                                var id = i.SelectToken(_idToken).Value<IdType>();
                                if (!values.ContainsKey(id))
                                {
                                    var jObj = new JObject();
                                    tnks.ForEach(m => jObj.Add(m, i.SelectToken(m)));
                                    values.Add(id, jObj);
                                }
                            }

                lock (_stagingCache)
                    if (values.Count < max && _stagingCache.Count > 0)
                        foreach (var i in _stagingCache.GetCache()
                            .Where(s => s.Value != null)
                            .SelectMany(s => s.Value.Values)
                                .Where(v => v != null)
                                .Where(selector))
                        {
                            if (values.Count >= max)
                                break;

                            var id = i.SelectToken(_idToken).Value<IdType>();
                            if (!values.ContainsKey(id))
                            {
                                var jObj = new JObject();
                                tnks.ForEach(m => jObj.Add(m, i.SelectToken(m)));
                                values.Add(id, jObj);
                            }
                        }


                if (values.Count < max)
                {
                    foreach (var page in _fileManager.AsEnumerable())
                    {
                        foreach (var i in page.Where(p => selector(p)))
                        {
                            if (values.Count >= max)
                                break;

                            var id = i.SelectToken(_idToken).Value<IdType>();

                            if (!values.ContainsKey(id))
                            {
                                var jObj = new JObject();
                                tnks.ForEach(m => jObj.Add(m, i.SelectToken(m)));
                                values.Add(id, jObj);
                            }
                        }
                    }
                }

                return values.Values.ToList();
            }
            catch (Exception ex) { throw new QueryExecuteException("Select Scalar First Query Execution Failed.", ex); }
        }

        public virtual IList<JObject> SelectScalarLast(Func<JObject, bool> selector, int max, params string[] tokens)
        {
            var values = new Dictionary<IdType, JObject>();
            var tnks = tokens.ToList();

            try
            {
                if (_transactionManager.CurrentTransaction != null)
                    using (var tLock = _transactionManager.GetActiveTransaction(false))
                        if (values.Count < max && tLock.Transaction.EnlistCount > 0)
                            foreach (var i in tLock.Transaction.GetEnlistedItems()
                                .Where(s => s != null)
                                .Select(s => Formatter.AsQueryableObj(s))
                                .Where(selector).Reverse())
                            {
                                if (values.Count >= max)
                                    break;

                                var id = i.SelectToken(_idToken).Value<IdType>();
                                if (!values.ContainsKey(id))
                                {
                                    var jObj = new JObject();
                                    tnks.ForEach(m => jObj.Add(m, i.SelectToken(m)));
                                    values.Add(id, jObj);
                                }
                            }

                lock (_stagingCache)
                    if (values.Count < max && _stagingCache.Count > 0)
                        foreach (var i in _stagingCache.GetCache()
                            .Where(s => s.Value != null)
                            .SelectMany(s => s.Value.Values)
                                .Where(s => s != null)
                                .Where(selector).Reverse())
                        {
                            if (values.Count >= max)
                                break;

                            var id = i.SelectToken(_idToken).Value<IdType>();
                            if (!values.ContainsKey(id))
                            {
                                var jObj = new JObject();
                                tnks.ForEach(m => jObj.Add(m, i.SelectToken(m)));
                                values.Add(id, jObj);
                            }
                        }

                if (values.Count < max)
                {
                    foreach (var page in _fileManager.AsReverseEnumerable())
                    {
                        foreach (var i in page.Where(p => selector(p)))
                        {
                            if (values.Count >= max)
                                break;

                            var id = i.SelectToken(_idToken).Value<IdType>();

                            if (!values.ContainsKey(id))
                            {
                                var jObj = new JObject();
                                tnks.ForEach(m => jObj.Add(m, i.SelectToken(m)));
                                values.Add(id, jObj);
                            }
                        }
                    }
                }

                return values.Values.ToList();
            }
            catch (Exception ex) { throw new QueryExecuteException("Select Scalar Last Query Execution Failed.", ex); }
        }

        public IDictionary<IdType, EntityType> GetCache()
        {
            lock (_syncRoot)
            {
                lock (_stagingCache)
                {
                    var u = 
                        _stagingCache.GetCache()
                        .Where(s => s.Value != null)
                        .SelectMany(s => s.Value.Select
                            (v => new KeyValuePair<IdType, EntityType>(v.Key, v.Value.ToObject<EntityType>(Formatter.Serializer))));

                    return u.GroupBy(k => k.Key).ToDictionary(k => k.Key, v => v.FirstOrDefault().Value);
                }
            }
        }

        public IEnumerable<Stream> AsStreaming()
        {
            return _fileManager.AsStreaming();
        }

        public virtual void Dispose()
        {
            if (_disposed)
                return;

            Trace.TraceInformation("Database disposing");

            if (_transactionManager != null)
                _transactionManager.CommitAmbientTransactions();

            Trace.TraceInformation("Waiting for ambient trans to commit.");

            while (_operations.Count > 0)
            {
                Thread.Sleep(10);
                lock (_syncRoot) { }
            }

            Trace.TraceInformation("All ambient transactions complete.");

            lock (_syncRoot)
            {
                _publish = false;

                Trace.TraceInformation("Waiting for all threads to exit.");

                while (_operations.Count > 0)
                    Thread.Sleep(100);

                Trace.TraceInformation("All threads have exited.");

                Trace.TraceInformation("Stopping all replication.");

                if (_publishers.Count > 0)
                    _publishers.Where(k => k.Value != null).ToList().ForEach(a => a.Value.Dispose());

                if (_subscribers.Count > 0)
                    _subscribers.Where(k => k.Value != null).ToList().ForEach(a => a.Value.Dispose());

                Trace.TraceInformation("Replication stopped.");

                lock (_syncTrans)
                {
                    if (_transactionManager != null)
                    {
                        _transactionManager.TransactionCommitted -= new TransactionCommit<IdType, EntityType>(OnTransactionCommitted);

                        _transactionManager.RollBackAll(true);

                        Trace.TraceInformation("Waiting for trans manager to complete operations.");

                        while (_transactionManager.HasActiveTransactions)
                            Thread.Sleep(100);

                        Trace.TraceInformation("Transaction manager operations complete.");

                        _transactionManager.Dispose();
                    }
                }

                if (_primaryIndex != null)
                {
                    Trace.TraceInformation("Waiting for primary indexUpdate to complete operations");

                    while (_primaryIndex.FileFlushQueueActive)
                        Thread.Sleep(100);

                    Trace.TraceInformation("Primary indexUpdate operations complete.");

                    _primaryIndex.Dispose();
                }

                if (_indexes != null && _indexes.Count > 0)
                {
                    foreach (var index in _indexes.Values)
                    {
                        Trace.TraceInformation("Waiting for secondary indexUpdate to complete operations");

                        var disposable = index as IDisposable;

                        var flushable = index as IFlush;

                        while (flushable.FileFlushQueueActive)
                            Thread.Sleep(100);

                        if (disposable != null)
                            disposable.Dispose();

                        Trace.TraceInformation("Secondary indexUpdate operations complete.");
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

