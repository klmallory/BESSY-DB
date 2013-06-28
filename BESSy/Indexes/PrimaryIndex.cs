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
using System.Linq;
using System.Reflection;
using System.Text;
using BESSy.Cache;
using BESSy.Factories;
using BESSy.Files;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using Newtonsoft.Json.Linq;
using BESSy.Synchronization;
using System.Diagnostics;
using BESSy.Transactions;
using BESSy.Parallelization;
using System.IO;
using Newtonsoft.Json;

namespace BESSy.Indexes
{
    public interface IPrimaryIndex<IdType, EntityType> : IIndex<IdType, EntityType, int>
    {
        ISeed<IdType> Seed { get; }
    }

    public sealed class PrimaryIndex<IdType, EntityType> : Index<IdType, EntityType>, IPrimaryIndex<IdType, EntityType>
    {
        //public PrimaryIndex
        //    (string fileName) 
        //    : this(fileName
        //    , new BSONFormatter())
        //{

        //}

        //public PrimaryIndex
        //    (string fileName
        //    , IQueryableFormatter formatter)
        //    : this(fileName, formatter
        //    , new RepositoryCacheFactory())
        //{

        //}

        //public PrimaryIndex
        //    (string fileName,
        //    IQueryableFormatter formatter,
        //    IRepositoryCacheFactory cacheFactory)
        //    : this(fileName, formatter, cacheFactory
        //    , new IndexFileFactory())
        //{

        //}

        //public PrimaryIndex
        //    (string fileName,
        //     IQueryableFormatter formatter,
        //    IRepositoryCacheFactory cacheFactory,
        //    IIndexFileFactory indexFileFactory)
        //    : this(fileName, formatter, cacheFactory, indexFileFactory,
        //    new RowSynchronizer<int>(new BinConverter32()))
        //{

        //}

        public PrimaryIndex
            (string fileName,
            IQueryableFormatter formatter,
            IRepositoryCacheFactory cacheFactory,
            IIndexFileFactory indexFileFactory,
            IRowSynchronizer<int> rowSynchonizer)
            : base(fileName, null, null, cacheFactory, formatter, indexFileFactory, rowSynchonizer)
        {

        }

        //public PrimaryIndex
        //    (string fileName, ISeed<IdType> seed)
        //    : this(fileName, seed
        //    , TypeFactory.GetBinConverterFor<IdType>())
        //{

        //}

        //public PrimaryIndex
        //    (string fileName,
        //    ISeed<IdType> seed,
        //    IBinConverter<IdType> propertyConverter)
        //    : this(fileName, seed, propertyConverter
        //    , new RepositoryCacheFactory())
        //{

        //}

        //public PrimaryIndex
        //    (string fileName,
        //    ISeed<IdType> seed,
        //    IBinConverter<IdType> propertyConverter,
        //    IRepositoryCacheFactory cacheFactory)
        //    : this(fileName, seed, propertyConverter, cacheFactory
        //    , new BSONFormatter())
        //{

        //}

        //public PrimaryIndex
        //    (string fileName,
        //    ISeed<IdType> seed,
        //    IBinConverter<IdType> propertyConverter,
        //    IRepositoryCacheFactory cacheFactory,
        //    IQueryableFormatter formatter)
        //    : this(fileName, seed, propertyConverter, cacheFactory, formatter
        //    , new IndexFileFactory())
        //{

        //}

        //public PrimaryIndex
        //    (string fileName,
        //    ISeed<IdType> seed,
        //    IBinConverter<IdType> propertyConverter,
        //    IRepositoryCacheFactory cacheFactory,
        //    IQueryableFormatter formatter,
        //    IIndexFileFactory indexFileFactory)
        //    : this(fileName, seed, propertyConverter, cacheFactory, formatter, indexFileFactory
        //    , new RowSynchronizer<int>(new BinConverter32()))
        //{

        //}

        public PrimaryIndex
            (string fileName,
            ISeed<IdType> seed,
            IBinConverter<IdType> propertyConverter,
            IRepositoryCacheFactory cacheFactory,
            IQueryableFormatter formatter,
            IIndexFileFactory indexFileFactory,
            IRowSynchronizer<int> rowSynchonizer)
            : base(fileName, seed.IdProperty, propertyConverter, cacheFactory, formatter, indexFileFactory, rowSynchonizer)
        {
            Seed = seed;

            if (Seed != null && Seed.PropertyConverter == null)
                Seed.PropertyConverter = propertyConverter;
        }

        IPrimaryIndexFileManager<IdType, EntityType, int> _primaryIndexFile
        {
            get { return _indexFile as IPrimaryIndexFileManager<IdType, EntityType, int>; }
            set { _indexFile = value; }
        }

        public ISeed<IdType> Seed { get; private set; }

        public override int Load()
        {
            Trace.TraceInformation("Primary index loading");

            lock (_syncFile)
            {
                if (Seed != null)
                    _primaryIndexFile = _indexFileFactory.CreatePrimary<IdType, EntityType>(_fileName, Environment.SystemPageSize, _fileFormatter, Seed, _rowSynchronizer);
                else
                    _primaryIndexFile = _indexFileFactory.CreatePrimary<IdType, EntityType>(_fileName, Environment.SystemPageSize, _fileFormatter, _rowSynchronizer);

                _primaryIndexFile.Load();

                Seed = _primaryIndexFile.Seed;
                _indexToken = Seed.IdProperty;
                _propertyConverter = (IBinConverter<IdType>)Seed.IdConverter;

                lock (_syncCache)
                    _indexCache = _cacheFactory.Create<IdType, SegmentMap<int, int>>(true, Caching.DetermineOptimumCacheSize(_primaryIndexFile.Stride), _propertyConverter);

                lock (_syncCache)
                    _indexStaging = _cacheFactory.Create<Guid, IDictionary<IdType, SegmentMap<int, int>>>(true, TaskGrouping.ArrayLimit, new BinConverterGuid());

                _indexFile.UpdateFailed += new UpdateFailed<IdType>
                    (delegate(IList<TransactionIndexResult<IdType>> results, IDisposable transaction, int newStride, int newLength)
                {
                    Trace.TraceInformation("Primary index rebuild triggered");

                    _indexFile.Rebuild(newStride, newLength, Seed.MinimumSeedStride);
                });
            }

            InitializeCache();

            return _indexFile.Length;
        }
    }
}
