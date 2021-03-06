﻿/*
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

namespace BESSy.Relational
{
    public interface IPocoRelationalDatabase<IdType, EntityType> : ITransactionalDatabase<IdType, JObject>
    {
        string IdToken { get; }
        IBinConverter<IdType> IdConverter { get; }
        void UpdateCascade(Tuple<string, IEnumerable<IdType>, IEnumerable<IdType>> cascade);
        void Update(EntityType item, IdType id);
        IdType Add(EntityType item);
        IdType AddOrUpdate(EntityType item, IdType id);
        new EntityType Fetch(IdType id);
        IList<EntityType> FetchPocoFromIndex<IndexType>(string name, IndexType indexProperty);
        IList<EntityType> SelectPoco(Func<JObject, bool> selector);
        IList<EntityType> SelectPocoFirst(Func<JObject, bool> selector, int max);
        IList<EntityType> SelectPocoLast(Func<JObject, bool> selector, int max);
    }

    public class PocoRelationalDatabase<IdType, EntityType> : JObjectDatabase<IdType>, IPocoRelationalDatabase<IdType, EntityType>
    {
        public PocoRelationalDatabase(string fileName)
            : this(fileName, new BSONFormatter())
        {

        }


        public PocoRelationalDatabase(string fileName, IQueryableFormatter formatter)
            : this(fileName, formatter
            , new TransactionManager<IdType, JObject>())
        {

        }


        public PocoRelationalDatabase(string fileName
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, JObject> transactionManager)
            : this(fileName, formatter, transactionManager
            , new AtomicFileManagerFactory())
        {

        }


        public PocoRelationalDatabase(string fileName
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, JObject> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory)
            : this(fileName, formatter, transactionManager, fileManagerFactory
            , new DatabaseCacheFactory())
        {

        }


        public PocoRelationalDatabase(string fileName
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, JObject> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory
            , IDatabaseCacheFactory cacheFactory)
            : this(fileName, formatter, transactionManager, fileManagerFactory, cacheFactory
            , new IndexFileFactory()
            , new IndexFactory())
        {

        }

        public PocoRelationalDatabase(string fileName
        , IQueryableFormatter formatter
        , ITransactionManager<IdType, JObject> transactionManager
        , IAtomicFileManagerFactory fileManagerFactory
        , IDatabaseCacheFactory cacheFactory
        , IIndexFileFactory indexFileFactory
        , IIndexFactory indexFactory)
            : this(fileName, formatter, transactionManager, fileManagerFactory, cacheFactory, indexFileFactory, indexFactory, new PocoProxyFactory<IdType, EntityType>())
        {

        }

        public PocoRelationalDatabase(string fileName
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, JObject> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory
            , IDatabaseCacheFactory cacheFactory
            , IIndexFileFactory indexFileFactory
            , IIndexFactory indexFactory
            , IProxyFactory<IdType, EntityType> proxyFactory)
            : base(fileName, formatter, transactionManager, fileManagerFactory, cacheFactory, indexFileFactory, indexFactory)
        {
            _proxyFactory = proxyFactory;
        }

        public PocoRelationalDatabase(string fileName, string idToken)
            : this(fileName, idToken
            , TypeFactory.GetFileCoreFor<IdType, long>())
        {

        }

        public PocoRelationalDatabase(string fileName
            , string idToken
            , IFileCore<IdType, long> core)
            : this(fileName, idToken, core
             , TypeFactory.GetBinConverterFor<IdType>())
        {

        }


        public PocoRelationalDatabase(string fileName
            , string idToken
            , IFileCore<IdType, long> core
            , IBinConverter<IdType> converter)
            : this(fileName, idToken, core, converter,
            new BSONFormatter())
        {

        }

        public PocoRelationalDatabase(string fileName
            , string idToken
            , IFileCore<IdType, long> core
                        , IBinConverter<IdType> converter
            , IQueryableFormatter formatter)
            : this(fileName, idToken, core, converter, formatter,
            new TransactionManager<IdType, JObject>())
        {

        }


        public PocoRelationalDatabase(string fileName
            , string idToken
            , IFileCore<IdType, long> core
                        , IBinConverter<IdType> converter
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, JObject> transactionManager)
            : this(fileName, idToken, core, converter, formatter, transactionManager
            , new AtomicFileManagerFactory())
        {

        }


        public PocoRelationalDatabase(string fileName
            , string idToken
            , IFileCore<IdType, long> core
            , IBinConverter<IdType> converter
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, JObject> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory)
            : this(fileName, idToken, core, converter, formatter, transactionManager, fileManagerFactory
            , new DatabaseCacheFactory())
        {

        }


        public PocoRelationalDatabase(string fileName
            , string idToken
            , IFileCore<IdType, long> core
            , IBinConverter<IdType> converter
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, JObject> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory
            , IDatabaseCacheFactory cacheFactory)
            : this(fileName, idToken, core, converter, formatter, transactionManager, fileManagerFactory, cacheFactory
            , new IndexFileFactory()
            , new IndexFactory())
        {

        }


        public PocoRelationalDatabase(string fileName
            , string idToken
            , IFileCore<IdType, long> core
            , IBinConverter<IdType> converter
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, JObject> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory
            , IDatabaseCacheFactory cacheFactory
            , IIndexFileFactory indexFileFactory
            , IIndexFactory indexFactory)
            : this(fileName, idToken, core, converter, formatter, transactionManager, fileManagerFactory, cacheFactory, indexFileFactory, indexFactory, new PocoProxyFactory<IdType, EntityType>())
        {
        }


        public PocoRelationalDatabase(string fileName
            , string idToken
            , IFileCore<IdType, long> core
            , IBinConverter<IdType> converter
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, JObject> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory
            , IDatabaseCacheFactory cacheFactory
            , IIndexFileFactory indexFileFactory
            , IIndexFactory indexFactory
            , IProxyFactory<IdType, EntityType> proxyFactory)
            : base(fileName, idToken, core, converter, formatter, transactionManager, fileManagerFactory, cacheFactory, indexFileFactory, indexFactory)
        {
            _proxyFactory = proxyFactory;
        }

        protected object _hashRoot = new object();

        protected Dictionary<Guid, Dictionary<int, IdType>> _transactionHashMash = new Dictionary<Guid, Dictionary<int, IdType>>();

        protected IProxyFactory<IdType, EntityType> _proxyFactory;
        protected IIndex<string, EntityType, IdType> _cascadeIndex = null;
        protected IIndex<string, EntityType, IdType> _externalIndex = null;
        protected Func<EntityType, IdType> _pocoIdGet;
        protected Action<EntityType, IdType> _pocoIdSet;
        protected Func<EntityType, String> _externalIdGet;
        protected Action<EntityType, String> _externalIdSet;
        protected BinConverterString _externalIdConverter;

        public IBinConverter<IdType> IdConverter { get { return _idConverter; } }

        public string IdToken { get { return _idToken; } }

        public override long Load()
        {
            _externalIdConverter = new BinConverterString(255);

            var length = base.Load();

            _proxyFactory.IdGet = this._pocoIdGet;
            _proxyFactory.IdSet = this._pocoIdSet;
            _proxyFactory.IdToken = this._idToken;

            if (!_core.ExternalIdProperty.IsNullOrEmpty())
            {
                _proxyFactory.ExternalIdGet = this._externalIdGet;
                _proxyFactory.ExternalIdSet = this._externalIdSet;
                _proxyFactory.ExternalIdToken = this._core.ExternalIdProperty;
            }

            _cascadeIndex = new Index<string, EntityType, IdType>(_fileName + ".cascade" + ".index", null, false);
            _cascadeIndex.Load();

            _proxyFactory.GetProxyTypeFor(typeof(EntityType));

            return length;
        }

        protected override void InitIdMethods()
        {
            _pocoIdGet = (Func<EntityType, IdType>)Delegate.CreateDelegate(typeof(Func<EntityType, IdType>), typeof(EntityType).GetProperty(_idToken).GetGetMethod());
            _pocoIdSet = (Action<EntityType, IdType>)Delegate.CreateDelegate(typeof(Action<EntityType, IdType>), typeof(EntityType).GetProperty(_idToken).GetSetMethod());

            if (!_core.ExternalIdProperty.IsNullOrEmpty())
            {
                var get = typeof(EntityType).GetProperty(_core.ExternalIdProperty).GetGetMethod();
                if (get.ReturnType != typeof(String))
                    throw new ProxyCreationException("ExternalIdToken must refer to a property of type 'System.String'");

                var set = typeof(EntityType).GetProperty(_core.ExternalIdProperty).GetSetMethod();

                _externalIdGet = (Func<EntityType, String>)Delegate.CreateDelegate(typeof(Func<EntityType, String>), get);
                _externalIdSet = (Action<EntityType, String>)Delegate.CreateDelegate(typeof(Action<EntityType, String>), set);
            }

            base.InitIdMethods();
        }

        protected override void OnTransactionCommitted(ITransaction<IdType, JObject> transaction)
        {
            Trace.TraceInformation("Updating cascades for transaction {0} commit", transaction.Id);

            foreach (var c in transaction.GetCascades())
            {
                try
                {
                    var indexUpdate = _cascadeIndex as IIndexUpdate<string, IdType>;

                    indexUpdate.PopIndexes(new string[] { c.Item1 });
                    indexUpdate.PushIndexes(c.Item2.Select(s => new NTreeItem<string, IdType>(c.Item1, s)));

                    long tmp;
                    foreach (var id in c.Item3)
                        if (_cascadeIndex.FetchIndex(id, out tmp) == null && tmp == 0)
                            transaction.Enlist(Action.Delete, id, null);
                }
                catch (Exception ex) { Trace.TraceError("Error cascading index for {0}: {1}", c.Item1, ex); }
            }

            base.OnTransactionCommitted(transaction);

            lock (_hashRoot)
                if (_transactionHashMash.ContainsKey(transaction.Id))
                    _transactionHashMash.Remove(transaction.Id);
        }

        public void UpdateCascade(Tuple<string, IEnumerable<IdType>, IEnumerable<IdType>> cascade)
        {
            using (var tLock = _transactionManager.GetActiveTransaction(false))
            {
                tLock.Transaction.Cascade(cascade);
            }
        }

        protected IdType GetSeededId(EntityType item)
        {
            var id = Seed.Increment();

            if (!Seed.Passive)
                _pocoIdSet(item, id);

            else
                id = _pocoIdGet(item);

            return id;
        }

        public override ITransaction BeginTransaction()
        {
            var tLock = base.BeginTransaction();

            if (!_transactionHashMash.ContainsKey(tLock.Id))
                _transactionHashMash.Add(tLock.Id, new Dictionary<int, IdType>());

            return tLock;
        }

        public IdType Add(EntityType item)
        {
            lock (_syncOperations)
                _operations.Push(2);

            if (item == null)
                return default(IdType);

            try
            {
                var hash = item.GetHashCode();

                using (var tLock = _transactionManager.GetActiveTransaction(false))
                {
                    lock (_hashRoot)
                    {
                        if (!_transactionHashMash.ContainsKey(tLock.TransactionId))
                            _transactionHashMash.Add(tLock.TransactionId, new Dictionary<int, IdType>());
                        else if (_transactionHashMash[tLock.TransactionId].ContainsKey(hash))
                            return _transactionHashMash[tLock.TransactionId][hash];
                    }

                    EntityType proxy = default(EntityType);

                    if (!(item is IBESSyProxy<IdType, EntityType>))
                        proxy = _proxyFactory.GetInstanceFor(this, item);

                    var proxyItem = (proxy as IBESSyProxy<IdType, EntityType>);

                    proxyItem.Bessy_Proxy_Repository = this;
                    proxyItem.Bessy_Proxy_Factory = _proxyFactory;
                    proxyItem.Bessy_Proxy_OldIdHash = item.GetType().FullName + _proxyFactory.IdGet(item).ToString();

                    proxyItem.Bessy_Proxy_Shallow_Copy_From(item);

                    var id = GetSeededId(proxy);

                    tLock.Transaction.Enlist(Action.Create, id, Formatter.AsQueryableObj(proxy));

                    lock (_hashRoot)
                        _transactionHashMash[tLock.TransactionId].Add(item.GetHashCode(), id);

                    proxyItem.Bessy_Proxy_Deep_Copy_From(item);

                    tLock.Transaction.Enlist(Action.Update, id, Formatter.AsQueryableObj(proxy));

                    _pocoIdSet(item, id);

                    return id;
                }
            }
            finally { lock (_syncOperations) _operations.Pop(); }
        }

        public void Update(EntityType item, IdType id)
        {
            var newId = _pocoIdGet(item);
            var deleteFirst = (_idConverter.Compare(newId, id) != 0);

            lock (_syncOperations)
                _operations.Push(3);

            var hash = item.GetHashCode();

            var oldSeg = _primaryIndex.FetchSegment(id);

            try
            {
                using (var tLock = _transactionManager.GetActiveTransaction(false))
                {
                    EntityType proxy = default(EntityType);
                    IBESSyProxy<IdType, EntityType> proxyItem = null;

                    lock (_hashRoot)
                    {
                        if (!_transactionHashMash.ContainsKey(tLock.TransactionId))
                            _transactionHashMash.Add(tLock.TransactionId, new Dictionary<int, IdType>());

                    }

                    if (!(item is IBESSyProxy<IdType, EntityType>))
                    {
                        proxy = _proxyFactory.GetInstanceFor(this, item);

                        proxyItem = (proxy as IBESSyProxy<IdType, EntityType>);
                        proxyItem.Bessy_Proxy_Shallow_Copy_From(item);

                        lock (_hashRoot)
                            if (!_transactionHashMash.ContainsKey(tLock.TransactionId))
                                _transactionHashMash[tLock.TransactionId].Add(item.GetHashCode(), id);

                        if (deleteFirst)
                        {
                            tLock.Transaction.Enlist(Action.Delete, id, null);
                            tLock.Transaction.Enlist(Action.Create, newId, Formatter.AsQueryableObj(proxy));
                        }
                        else
                            tLock.Transaction.Enlist(Action.Update, newId, Formatter.AsQueryableObj(proxy));

                        proxyItem.Bessy_Proxy_Deep_Copy_From(item);

                        tLock.Transaction.Enlist(Action.Update, newId, Formatter.AsQueryableObj(proxy));
                    }
                    else
                    {
                        if (deleteFirst)
                        {
                            tLock.Transaction.Enlist(Action.Delete, id, null);
                            tLock.Transaction.Enlist(Action.Create, newId, Formatter.AsQueryableObj(item));
                        }
                        else
                            tLock.Transaction.Enlist(Action.Update, newId, Formatter.AsQueryableObj(item));
                    }
                }
            }
            finally { lock (_syncOperations) _operations.Pop(); }
        }

        public new EntityType Fetch(IdType id)
        {
            var item = base.Fetch(id);

            if (item != null)
            {
                var entity = _proxyFactory.GetInstanceFor(this, item);

                return entity;
            }

            return default(EntityType);
        }

        public IList<EntityType> SelectPoco(Func<JObject, bool> selector)
        {
            var selects = base.Select(selector)
                .Select(s => _proxyFactory.GetInstanceFor(this, s)).ToList();

            return selects;
        }

        public IList<EntityType> SelectPocoFirst(Func<JObject, bool> selector, int max)
        {
            var first = base.SelectFirst(selector, max)
                                .Select(s => _proxyFactory.GetInstanceFor(this, s)).ToList();

            return first;
        }

        public IList<EntityType> SelectPocoLast(Func<JObject, bool> selector, int max)
        {
            var last = base.SelectLast(selector, max)
                .Select(s => _proxyFactory.GetInstanceFor(this, s)).ToList(); 

            return last;
        }

        public IList<EntityType> FetchPocoFromIndex<IndexType>(string name, IndexType indexProperty)
        {
            var list = base.FetchFromIndex<IndexType>(name, indexProperty)
                .Select(s => _proxyFactory.GetInstanceFor(this, s)).ToList();

            return list;
        }

        public IdType AddOrUpdate(EntityType item, IdType id)
        {
            if (_idConverter.Compare(id, default(IdType)) == 0)
                return Add(item);
            else
            { Update(item, id); return id; }
        }

        public override void Dispose()
        {
            base.Dispose();

            if (_cascadeIndex != null)
                _cascadeIndex.Dispose();
        }
    }
}
