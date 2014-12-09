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

namespace BESSy
{
    public class Database<IdType, EntityType> : AbstractTransactionalDatabase<IdType, EntityType>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param property="fileName"></param>
        public Database(string fileName)
            : this(fileName
            , new BSONFormatter())
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param property="fileName"></param>
        public Database(string fileName, IQueryableFormatter formatter)
            : this(fileName, formatter
            , new TransactionManager<IdType, EntityType>())
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param property="fileName"></param>
        /// <param property="transactionManager"></param>
        public Database(string fileName
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
        public Database(string fileName
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
        public Database(string fileName
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
        public Database(string fileName
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
        public Database(string fileName, string idToken)
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
        public Database(string fileName, string idToken
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
        public Database(string fileName, string idToken
            , IFileCore<IdType, long> core
            , IQueryableFormatter formatter)
            : this(fileName, idToken, core, TypeFactory.GetBinConverterFor<IdType>()
            , formatter)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param property="fileName"></param>
        /// <param property="idToken"></param>
        /// <param property="segmentSeed"></param>
        /// <param property="converter"></param>
        public Database(string fileName, string idToken
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
        public Database(string fileName, string idToken
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
        public Database(string fileName, string idToken
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
        public Database(string fileName, string idToken
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
        public Database(string fileName, string idToken
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
        public Database(string fileName, string idToken
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
    }
}
