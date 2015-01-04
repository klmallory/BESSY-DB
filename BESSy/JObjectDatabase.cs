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
using BESSy.Json;
using BESSy.Json.Linq;
using BESSy.Parallelization;
using BESSy.Queries;
using BESSy.Reflection;
using BESSy.Replication;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using BESSy.Transactions;

namespace BESSy
{
    public class JObjectDatabase<IdType> : AbstractTransactionalDatabase<IdType, JObject>
    {
        public JObjectDatabase(string fileName)
            : this(fileName, new BSONFormatter())
        {

        }

        public JObjectDatabase(string fileName, IQueryableFormatter formatter)
            : this(fileName, formatter
            , new TransactionManager<IdType, JObject>())
        {

        }

        public JObjectDatabase(string fileName
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, JObject> transactionManager)
            : this(fileName, formatter, transactionManager
            , new AtomicFileManagerFactory())
        {

        }

        public JObjectDatabase(string fileName
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, JObject> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory)
            : this(fileName, formatter, transactionManager, fileManagerFactory
            , new DatabaseCacheFactory())
        {

        }

        public JObjectDatabase(string fileName
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, JObject> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory
            , IDatabaseCacheFactory cacheFactory)
            : this(fileName, formatter, transactionManager, fileManagerFactory, cacheFactory
            , new IndexFileFactory()
            , new IndexFactory())
        {

        }

        public JObjectDatabase(string fileName
            , IQueryableFormatter formatter
            , ITransactionManager<IdType, JObject> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory
            , IDatabaseCacheFactory cacheFactory
            , IIndexFileFactory indexFileFactory
            , IIndexFactory indexFactory)
            : base(fileName, formatter, transactionManager, fileManagerFactory, cacheFactory, indexFileFactory, indexFactory
            , new RowSynchronizer<long>(new BinConverter64()))
        {

        }

        public JObjectDatabase(string fileName, string idToken)
            : this(fileName, idToken
            , TypeFactory.GetFileCoreFor<IdType, long>())
        {

        }

        public JObjectDatabase(string fileName
            , string idToken
            , IFileCore<IdType, long> core)
            : this(fileName, idToken, core
            , new BSONFormatter())
        {

        }

        public JObjectDatabase(string fileName
            , string idToken
            , IFileCore<IdType, long> core
            , IQueryableFormatter formatter)
            : this(fileName, idToken, core, formatter
            , TypeFactory.GetBinConverterFor<IdType>())
        {

        }

        public JObjectDatabase(string fileName
            , string idToken
            , IFileCore<IdType, long> core
            , IQueryableFormatter formatter
            , IBinConverter<IdType> converter)
            : this(fileName, idToken, core, formatter, converter,
            new TransactionManager<IdType, JObject>())
        {

        }

        public JObjectDatabase(string fileName
            , string idToken
            , IFileCore<IdType, long> core
            , IQueryableFormatter formatter
            , IBinConverter<IdType> converter
            , ITransactionManager<IdType, JObject> transactionManager)
            : this(fileName, idToken, core, formatter, converter, transactionManager
            , new AtomicFileManagerFactory())
        {

        }

        public JObjectDatabase(string fileName
            , string idToken
            , IFileCore<IdType, long> core
            , IQueryableFormatter formatter
            , IBinConverter<IdType> converter
            , ITransactionManager<IdType, JObject> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory)
            : this(fileName, idToken, core, formatter, converter, transactionManager, fileManagerFactory
            , new DatabaseCacheFactory())
        {

        }

        public JObjectDatabase(string fileName
            , string idToken
            , IFileCore<IdType, long> core
            , IQueryableFormatter formatter
            , IBinConverter<IdType> converter
            , ITransactionManager<IdType, JObject> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory
            , IDatabaseCacheFactory cacheFactory)
            : this(fileName, idToken, core, formatter, converter, transactionManager, fileManagerFactory, cacheFactory
            , new IndexFileFactory()
            , new IndexFactory())
        {

        }

        public JObjectDatabase(string fileName
            , string idToken
            , IFileCore<IdType, long> core
            , IQueryableFormatter formatter
            , IBinConverter<IdType> converter
            , ITransactionManager<IdType, JObject> transactionManager
            , IAtomicFileManagerFactory fileManagerFactory
            , IDatabaseCacheFactory cacheFactory
            , IIndexFileFactory indexFileFactory
            , IIndexFactory indexFactory)
            : base(fileName, idToken, core, converter, formatter, transactionManager, fileManagerFactory, cacheFactory, indexFileFactory, indexFactory
            , new RowSynchronizer<long>(new BinConverter64()))
        {

        }

        protected override void InitializePrimaryIndex()
        {
            _primaryIndex = _indexFactory.Create<IdType, JObject, long>
                (GetIndexName(_fileName), _idToken, true, 1024, _idConverter, new BinConverter64(), _rowSynchronizer, new RowSynchronizer<int>(new BinConverter32()), _idGet);

            _primaryIndex.Load();

            _primaryIndex.Register(_fileManager);
        }

        protected override void InitIdMethods()
        {
            _idGet = new Func<JObject, IdType>(j => j.Value<IdType>(_idToken));
            _idSet = new Action<JObject, IdType>((j, v) => j.SetValue<IdType>(_idToken, v, Formatter.Serializer)); 
        }

        protected override JObject LoadFromFile(long seg)
        {
            return _fileManager.LoadJObjectFrom(seg);
        }

        public override IList<JObject> Select(Func<JObject, bool> selector)
        {
            return base.SelectJObj(selector);
        }

        public override IList<JObject> SelectFirst(Func<JObject, bool> selector, int max)
        {
            return base.SelectJObjFirst(selector, max);
        }

        public override IList<JObject> SelectLast(Func<JObject, bool> selector, int max)
        {
            return base.SelectJObjLast(selector, max);
        }

        protected override object GetEntityFrom(Type type, JObject obj)
        {
            return obj;
        }

        public override IdType AddJObj(Type type, JObject obj)
        {
            throw new NotSupportedException();
        }

        public override IdType AddOrUpdateJObj(Type type, JObject obj, IdType id)
        {
            throw new NotSupportedException();
        }

        public override int DeleteLast(Func<JObject, bool> selector, int max)
        {
            return base.DeleteLast(selector, max);
        }

        public override void Delete(IEnumerable<IdType> ids)
        {
            base.Delete(ids);
        }

        protected override IdType GetSeededId(JObject item)
        {
            return base.GetSeededId(item);
        }
    }
}
