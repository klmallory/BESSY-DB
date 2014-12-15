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

namespace BESSy.Relational
{
    public interface IPocoRelationalDatabase<IdType, EntityType> : ITransactionalDatabase<IdType, EntityType>
    {
        string IdToken { get; }
        IBinConverter<IdType> IdConverter { get; }
        void UpdateCascade(Tuple<string, IEnumerable<IdType>, IEnumerable<IdType>> cascade);
    }

    public class PocoRelationalDatabase<IdType, EntityType> : AbstractTransactionalDatabase<IdType, EntityType>, IPocoRelationalDatabase<IdType, EntityType>
    {
        public PocoRelationalDatabase(string fileName)
            : this(fileName, new BSONFormatter()
            , new PocoProxyFactory<IdType, EntityType>())
        {

        }

        public PocoRelationalDatabase(string fileName, IQueryableFormatter formatter)
            : this(fileName, formatter
            , new PocoProxyFactory<IdType, EntityType>())
        {

        }


        public PocoRelationalDatabase(string fileName, IQueryableFormatter formatter, IProxyFactory<IdType, EntityType> proxyFactory)
            : this(fileName, formatter, proxyFactory
            , new PocoTransactionManager<IdType, EntityType>())
        {

        }


        public PocoRelationalDatabase(string fileName
            , IQueryableFormatter formatter
            , IProxyFactory<IdType, EntityType> proxyFactory
            , IPocoTransactionManager<IdType, EntityType> transactionManager)
            : this(fileName, formatter, proxyFactory, transactionManager
            , new AtomicFileManagerFactory())
        {

        }


        public PocoRelationalDatabase(string fileName
            , IQueryableFormatter formatter
            , IProxyFactory<IdType, EntityType> proxyFactory
            , IPocoTransactionManager<IdType, EntityType> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory)
            : this(fileName, formatter, proxyFactory, transactionManager, fileManagerFactory
            , new DatabaseCacheFactory())
        {

        }


        public PocoRelationalDatabase(string fileName
            , IQueryableFormatter formatter
            , IProxyFactory<IdType, EntityType> proxyFactory
            , IPocoTransactionManager<IdType, EntityType> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory
            , IDatabaseCacheFactory cacheFactory)
            : this(fileName, formatter, proxyFactory, transactionManager, fileManagerFactory, cacheFactory
            , new IndexFileFactory()
            , new IndexFactory())
        {

        }


        public PocoRelationalDatabase(string fileName
            , IQueryableFormatter formatter
            , IProxyFactory<IdType, EntityType> proxyFactory
            , IPocoTransactionManager<IdType, EntityType> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory
            , IDatabaseCacheFactory cacheFactory
            , IIndexFileFactory indexFileFactory
            , IIndexFactory indexFactory)
            : base(fileName, formatter, transactionManager, fileManagerFactory, cacheFactory, indexFileFactory, indexFactory
            , new RowSynchronizer<long>(new BinConverter64()))
        {
            _proxyFactory = proxyFactory;

            var tm = _transactionManager as IPocoTransactionManager<IdType, EntityType>;

            if (tm != null)
                tm.IdConverter = _idConverter;

            //var settings = Formatter.Settings;
            //settings.ContractResolver = new PocoProxySerializer() { GetTypeFor = t => _proxyFactory.GetProxyTypeFor(t) };
            //Formatter.Settings = settings;
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
            , new BSONFormatter())
        {

        }


        public PocoRelationalDatabase(string fileName
            , string idToken
            , IFileCore<IdType, long> core
            , IQueryableFormatter formatter)
            : this(fileName, idToken, core, formatter
            , TypeFactory.GetBinConverterFor<IdType>())
        {

        }

        public PocoRelationalDatabase(string fileName
            , string idToken
            , IFileCore<IdType, long> core
            , IQueryableFormatter formatter
            , IBinConverter<IdType> converter)
            : this(fileName, idToken, core, formatter, converter,
            new PocoProxyFactory<IdType, EntityType>())
        {

        }

        public PocoRelationalDatabase(string fileName
            , string idToken
            , IFileCore<IdType, long> core
            , IQueryableFormatter formatter
            , IBinConverter<IdType> converter
            , IProxyFactory<IdType, EntityType> proxyFactory)
            : this(fileName, idToken, core, formatter, converter, proxyFactory,
            new PocoTransactionManager<IdType, EntityType>(converter))
        {

        }


        public PocoRelationalDatabase(string fileName
            , string idToken
            , IFileCore<IdType, long> core
            , IQueryableFormatter formatter
            , IBinConverter<IdType> converter
            , IProxyFactory<IdType, EntityType> proxyFactory
            , IPocoTransactionManager<IdType, EntityType> transactionManager)
            : this(fileName, idToken, core, formatter, converter, proxyFactory, transactionManager
            , new AtomicFileManagerFactory())
        {

        }


        public PocoRelationalDatabase(string fileName
            , string idToken
            , IFileCore<IdType, long> core
            , IQueryableFormatter formatter
            , IBinConverter<IdType> converter
            , IProxyFactory<IdType, EntityType> proxyFactory
            , IPocoTransactionManager<IdType, EntityType> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory)
            : this(fileName, idToken, core, formatter, converter, proxyFactory, transactionManager, fileManagerFactory
            , new DatabaseCacheFactory())
        {

        }


        public PocoRelationalDatabase(string fileName
            , string idToken
            , IFileCore<IdType, long> core
            , IQueryableFormatter formatter
            , IBinConverter<IdType> converter
            , IProxyFactory<IdType, EntityType> proxyFactory
            , IPocoTransactionManager<IdType, EntityType> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory
            , IDatabaseCacheFactory cacheFactory)
            : this(fileName, idToken, core, formatter, converter, proxyFactory, transactionManager, fileManagerFactory, cacheFactory
            , new IndexFileFactory()
            , new IndexFactory())
        {

        }

        public PocoRelationalDatabase(string fileName
            , string idToken
            , IFileCore<IdType, long> core
            , IQueryableFormatter formatter
            , IBinConverter<IdType> converter
            , IProxyFactory<IdType, EntityType> proxyFactory
            , ITransactionManager<IdType, EntityType> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory
            , IDatabaseCacheFactory cacheFactory
            , IIndexFileFactory indexFileFactory
            , IIndexFactory indexFactory)
            : base(fileName, idToken, core, converter, formatter, transactionManager, fileManagerFactory, cacheFactory, indexFileFactory, indexFactory
            , new RowSynchronizer<long>(new BinConverter64()))
        {
            _proxyFactory = proxyFactory;

            //var settings = Formatter.Settings;
            //settings.ContractResolver = new PocoProxySerializer() { GetTypeFor = t => _proxyFactory.GetProxyTypeFor(t) };
            //Formatter.Settings = settings;
        }

        protected object _hashRoot = new object();

        protected Dictionary<Guid, Dictionary<int, IdType>> _transactionHashMash = new Dictionary<Guid, Dictionary<int, IdType>>();

        protected IProxyFactory<IdType, EntityType> _proxyFactory;

        protected IIndex<string, EntityType, IdType> _cascadeIndex = null;

        protected IPocoTransactionManager<IdType, EntityType> _pocoTransactionMananger { get { return _transactionManager as IPocoTransactionManager<IdType, EntityType>; } }

        protected internal IPocoTransaction<IdType, EntityType> _pocoCurrentTransaction { get { return _transactionManager.CurrentTransaction as IPocoTransaction<IdType, EntityType>; } }

        public IBinConverter<IdType> IdConverter { get { return _idConverter; } }

        public string IdToken { get { return _idToken; } }

        public override long Load()
        {
            var length = base.Load();

            _proxyFactory.IdGet = this._idGet;
            _proxyFactory.IdSet = this._idSet;
            _proxyFactory.IdToken = this._idToken;

            _cascadeIndex = new Index<string, EntityType, IdType>(_fileName + ".cascade" + ".index", null, false);
            _cascadeIndex.Load();

            _proxyFactory.GetProxyTypeFor(typeof(EntityType));

            return length;
        }

        protected override void OnTransactionCommitted(ITransaction<IdType, EntityType> transaction)
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
                            transaction.Enlist(Action.Delete, id, default(EntityType));
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

        protected override IdType GetSeededId(EntityType item)
        {
            var id = Seed.Increment();

            if (!Seed.Passive)
                _idSet(item, id);

            else
                id = _idGet(item);

            return id;
        }

        public override ITransaction BeginTransaction()
        {
            var tLock = base.BeginTransaction();

            if (!_transactionHashMash.ContainsKey(tLock.Id))
                _transactionHashMash.Add(tLock.Id, new Dictionary<int, IdType>());

            return tLock;
        }

        public override IdType Add(EntityType item)
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

                    proxyItem.Bessy_Proxy_Shallow_Copy_From(item);

                    var id = GetSeededId(proxy);

                    tLock.Transaction.Enlist(Action.Create, id, proxy);

                    lock (_hashRoot)
                        _transactionHashMash[tLock.TransactionId].Add(item.GetHashCode(), id);

                    proxyItem.Bessy_Proxy_Deep_Copy_From(item);

                    tLock.Transaction.Enlist(Action.Update, id, proxy);

                    _idSet(item, id);

                    return id;
                }
            }
            finally { lock (_syncOperations) _operations.Pop(); }
        }

        public override void Update(EntityType item, IdType id)
        {
            var newId = _idGet(item);
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
                            tLock.Transaction.Enlist(Action.Delete, id, proxy);
                            tLock.Transaction.Enlist(Action.Create, newId, proxy);
                        }
                        else
                            tLock.Transaction.Enlist(Action.Update, newId, proxy);

                        proxyItem.Bessy_Proxy_Deep_Copy_From(item);
                    }
                    else
                    {
                        if (deleteFirst)
                        {
                            tLock.Transaction.Enlist(Action.Delete, id, item);
                            tLock.Transaction.Enlist(Action.Create, newId, item);
                        }
                        else
                            tLock.Transaction.Enlist(Action.Update, newId, item);
                    }
                }
            }
            finally { lock (_syncOperations) _operations.Pop(); }
        }

        public override void Update(Type type, JObject obj, IdType id)
        {
            base.Update(type, obj, id);
        }

        public override int UpdateScalar(Type updateType, Func<JObject, bool> selector, Action<JObject> update)
        {
            return base.UpdateScalar(updateType, selector, update);
        }

        public override EntityType Fetch(IdType id)
        {
            var item = base.Fetch(id);

            if (item != null)
            {
                var p = (item as IBESSyProxy<IdType, EntityType>);
                p.Bessy_Proxy_Repository = this;
                p.Bessy_Proxy_Factory = _proxyFactory;
            }

            return item;
        }

        public override IList<EntityType> Select(Func<JObject, bool> selector)
        {
            var selects = base.Select(selector);

            foreach (var s in selects)
            {
                var p = (s as IBESSyProxy<IdType, EntityType>);

                if (s == null)
                    continue;

                p.Bessy_Proxy_Repository = this;
                p.Bessy_Proxy_Factory = _proxyFactory;
            }

            return selects;
        }

        public override IList<EntityType> SelectFirst(Func<JObject, bool> selector, int max)
        {
            var first = base.SelectFirst(selector, max);

            foreach (var s in first)
            {
                var p = (s as IBESSyProxy<IdType, EntityType>);

                if (s == null)
                    continue;

                p.Bessy_Proxy_Repository = this;
                p.Bessy_Proxy_Factory = _proxyFactory;
            }

            return first;
        }

        public override IList<EntityType> SelectLast(Func<JObject, bool> selector, int max)
        {
            var last = base.SelectLast(selector, max);

            foreach (var s in last)
            {
                var p = (s as IBESSyProxy<IdType, EntityType>);

                if (s == null)
                    continue;

                p.Bessy_Proxy_Repository = this;
                p.Bessy_Proxy_Factory = _proxyFactory;
            }

            return last;
        }

        public override IList<EntityType> FetchFromIndex<IndexType>(string name, IndexType indexProperty)
        {
            var list = base.FetchFromIndex<IndexType>(name, indexProperty);

            foreach (var s in list)
            {
                var p = (s as IBESSyProxy<IdType, EntityType>);

                if (s == null)
                    continue;

                p.Bessy_Proxy_Repository = this;
                p.Bessy_Proxy_Factory = _proxyFactory;
            }

            return list;
        }

        public override void Dispose()
        {
            base.Dispose();

            if (_cascadeIndex != null)
                _cascadeIndex.Dispose();
        }
    }
}
