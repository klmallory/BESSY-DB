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
using BESSy.Json;
using BESSy.Json.Linq;

namespace BESSy.Relational
{
    public interface IRelationalDatabase<IdType, EntityType> : ITransactionalDatabase<IdType, EntityType>
    {
        IBinConverter<IdType> IdConverter { get; }
        void UpdateCascade(Tuple<string, IEnumerable<IdType>, IEnumerable<IdType>> cascade);
    }

    public interface IRelationalEntity<IdType, EntityType>
    {
        IRelationalDatabase<IdType, EntityType> Repository { set; }
        IdType Id { get; set; }
    }

    internal interface IRelationalAccessor<IdType, EntityType>
    {
        IDictionary<string, IdType[]> RelationshipIds { get; }
    }

    public class RelationalDatabase<IdType, EntityType> : AbstractTransactionalDatabase<IdType, EntityType>, IRelationalDatabase<IdType, EntityType> where EntityType : IRelationalEntity<IdType, EntityType>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param property="fileName"></param>
        public RelationalDatabase(string fileName)
            : this(fileName
            , new BSONFormatter())
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param property="fileName"></param>
        public RelationalDatabase(string fileName, IQueryableFormatter formatter)
            : this(fileName, formatter
            , new TransactionManager<IdType, EntityType>())
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param property="fileName"></param>
        /// <param property="transactionManager"></param>
        public RelationalDatabase(string fileName
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, EntityType> transactionManager)
            : this(fileName, formatter, transactionManager
            , new AtomicFileManagerFactory())
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param property="fileName"></param>
        /// <param property="transactionManager"></param>
        /// <param property="cacheFactory"></param>
        public RelationalDatabase(string fileName
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, EntityType> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory)
            : this(fileName, formatter, transactionManager, fileManagerFactory
            , new DatabaseCacheFactory())
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param property="fileName"></param>
        /// <param property="transactionManager"></param>
        /// <param property="cacheFactory"></param>
        /// <param property="cacheFactory"></param>
        public RelationalDatabase(string fileName
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, EntityType> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory
            , IDatabaseCacheFactory cacheFactory)
            : this(fileName, formatter, transactionManager, fileManagerFactory, cacheFactory
            , new IndexFileFactory()
            , new IndexFactory())
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param property="fileName"></param>
        /// <param property="transactionManager"></param>
        /// <param property="cacheFactory"></param>
        /// <param property="cacheFactory"></param>
        /// <param property="cacheFactory"></param>
        /// <param property="cacheFactory"></param>
        public RelationalDatabase(string fileName
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, EntityType> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory
            , IDatabaseCacheFactory cacheFactory
            , IIndexFileFactory indexFileFactory
            , IIndexFactory indexFactory)
            : base(fileName, formatter, transactionManager, fileManagerFactory, cacheFactory, indexFileFactory, indexFactory
            , new RowSynchronizer<long>(new BinConverter64()))
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param property="fileName"></param>
        /// <param property="idToken"></param>
        public RelationalDatabase(string fileName, string idToken)
            : this(fileName, idToken
            ,TypeFactory.GetFileCoreFor<IdType, long>())
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param property="fileName"></param>
        /// <param property="idToken"></param>
        /// <param property="segmentSeed"></param>
        public RelationalDatabase(string fileName, string idToken
            , IFileCore<IdType, long> core)
            : this(fileName, idToken, core            
            , TypeFactory.GetBinConverterFor<IdType>())
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param property="fileName"></param>
        /// <param property="idToken"></param>
        /// <param property="segmentSeed"></param>
        /// <param property="converter"></param>
        public RelationalDatabase(string fileName, string idToken
            , IFileCore<IdType, long> core
            , IBinConverter<IdType> converter)
            : this(fileName, idToken, core, converter
            , new BSONFormatter())
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param property="fileName"></param>
        /// <param property="idToken"></param>
        /// <param property="segmentSeed"></param>
        /// <param property="converter"></param>
        /// <param property="formatter"></param>
        /// <param property="transactionManager"></param>
        public RelationalDatabase(string fileName, string idToken
            , IFileCore<IdType, long> core
            , IBinConverter<IdType> converter
            , IQueryableFormatter formatter)
            : this(fileName, idToken, core, converter, formatter,
            new TransactionManager<IdType, EntityType>())
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param property="fileName"></param>
        /// <param property="idToken"></param>
        /// <param property="segmentSeed"></param>
        /// <param property="converter"></param>
        /// <param property="formatter"></param>
        /// <param property="transactionManager"></param>
        public RelationalDatabase(string fileName, string idToken
            , IFileCore<IdType, long> core
            , IBinConverter<IdType> converter
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, EntityType> transactionManager)
            : this(fileName, idToken, core, converter, formatter, transactionManager
            , new AtomicFileManagerFactory())
        {

        }
       
        /// <summary>
        /// 
        /// </summary>
        /// <param property="fileName"></param>
        /// <param property="idToken"></param>
        /// <param property="segmentSeed"></param>
        /// <param property="converter"></param>
        /// <param property="formatter"></param>
        /// <param property="transactionManager"></param>
        /// <param property="cacheFactory"></param>
        public RelationalDatabase(string fileName, string idToken
            , IFileCore<IdType, long> core
            , IBinConverter<IdType> converter
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, EntityType> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory)
            : this(fileName, idToken, core, converter, formatter, transactionManager, fileManagerFactory
            , new DatabaseCacheFactory())
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param property="fileName"></param>
        /// <param property="idToken"></param>
        /// <param property="segmentSeed"></param>
        /// <param property="converter"></param>
        /// <param property="formatter"></param>
        /// <param property="transactionManager"></param>
        /// <param property="cacheFactory"></param>
        /// <param property="cacheFactory"></param>
        public RelationalDatabase(string fileName, string idToken
            , IFileCore<IdType, long> core
            , IBinConverter<IdType> converter
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, EntityType> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory
            , IDatabaseCacheFactory cacheFactory)
            : this(fileName, idToken, core, converter, formatter, transactionManager, fileManagerFactory, cacheFactory
            , new IndexFileFactory()
            , new IndexFactory())
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param property="fileName"></param>
        /// <param property="idToken"></param>
        /// <param property="segmentSeed"></param>
        /// <param property="converter"></param>
        /// <param property="formatter"></param>
        /// <param property="transactionManager"></param>
        /// <param property="cacheFactory"></param>
        /// <param property="cacheFactory"></param>
        /// <param property="cacheFactory"></param>
        /// <param property="cacheFactory"></param>
        public RelationalDatabase(string fileName, string idToken
            , IFileCore<IdType, long> core
            , IBinConverter<IdType> converter
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, EntityType> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory
            , IDatabaseCacheFactory cacheFactory
            , IIndexFileFactory indexFileFactory
            , IIndexFactory indexFactory)
            : base(fileName, idToken, core, converter, formatter, transactionManager, fileManagerFactory, cacheFactory, indexFileFactory, indexFactory
            , new RowSynchronizer<long>(new BinConverter64()))
        {

        }

        protected IIndex<string, EntityType, IdType> _cascadeIndex = null;

        public IBinConverter<IdType> IdConverter { get { return _idConverter; } }

        public override long Load()
        {
            var length = base.Load();

            _cascadeIndex = new CascadeIndex<string, EntityType, IdType>(_fileName + ".cascade" + ".index", null, false);

            _cascadeIndex.Load();

            _cascadeIndex.Register(this._fileManager);

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
        }

        public void UpdateCascade(Tuple<string, IEnumerable<IdType>, IEnumerable<IdType>> cascade)
        {
            using (var tLock = _transactionManager.GetActiveTransaction(false))
            {
                tLock.Transaction.Cascade(cascade);
            }
        }

        public override EntityType Fetch(IdType id)
        {
            var item = base.Fetch(id);

            if (item != null)
                item.Repository = this;

            return item;
        }

        public override IList<EntityType> Select(Func<JObject, bool> selector)
        {
            var selects = base.Select(selector);

            foreach (var s in selects)
                s.Repository = this;

            return selects;
        }

        public override IList<EntityType> SelectFirst(Func<JObject, bool> selector, int max)
        {
            var first = base.SelectFirst(selector, max);

            foreach (var s in first)
                s.Repository = this;

            return first;
        }

        public override IList<EntityType> SelectLast(Func<JObject, bool> selector, int max)
        {
            var last = base.SelectLast(selector, max);

            foreach (var s in last)
                s.Repository = this;

            return last;
        }

        public override IList<EntityType> FetchFromIndex<IndexType>(string name, IndexType indexProperty)
        {
            var list = base.FetchFromIndex<IndexType>(name, indexProperty);

            foreach (var i in list)
                i.Repository = this;

            return list;
        }

        public override void Dispose()
        {
            lock (_syncIndex)
                if (_cascadeIndex != null)
                    _cascadeIndex.Dispose();

            base.Dispose();
        }
    }
}
