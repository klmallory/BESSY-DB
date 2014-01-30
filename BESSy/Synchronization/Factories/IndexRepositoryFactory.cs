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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BESSy.Cache;
using BESSy.Factories;
using BESSy.Files;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;


namespace BESSy.Factories
{
    public interface IIndexRepositoryFactory<IdType, PropertyType>
    {
        IBinConverter<IdType> DefaultIdConverter { get; set; }
        IBinConverter<PropertyType> DefaultPropertyConverter { get; set; }
        ISeed<IdType> DefaultSeed { get; set; }
        ISafeFormatter DefaultMapFormatter { get; set; }
        IRepositoryCacheFactory DefaultCacheFactory { get; set; }
        IBatchFileManager<IndexPropertyPair<IdType, PropertyType>> DefaultBatchFileManager { get; set; }

        IIndexRepository<IdType, PropertyType> Create(string fileNamePath);
        IIndexRepository<IdType, PropertyType> CreateNew(string fileNamePath);
    }

    public sealed class IndexRepositoryFactory<IdType, PropertyType> : IIndexRepositoryFactory<IdType, PropertyType>
    {
        public IBinConverter<IdType> DefaultIdConverter { get; set; }
        public IBinConverter<PropertyType> DefaultPropertyConverter { get; set; }
        public ISeed<IdType> DefaultSeed { get; set; }
        public ISafeFormatter DefaultMapFormatter { get; set; }
        public IRepositoryCacheFactory DefaultCacheFactory { get; set; }
        public IBatchFileManager<IndexPropertyPair<IdType, PropertyType>> DefaultBatchFileManager { get; set; }

        public IndexRepositoryFactory()
            : this(TypeFactory.GetBinConverterFor<IdType>()
            , TypeFactory.GetBinConverterFor<PropertyType>()
            , TypeFactory.GetSeedFor<IdType>())
        {

        }

        public IndexRepositoryFactory
        (IBinConverter<IdType> idConverter
        , IBinConverter<PropertyType> propertyConverter
        , ISeed<IdType> seed)
            : this(idConverter
            , propertyConverter
            , seed
            , new BSONFormatter())
        {

        }

        public IndexRepositoryFactory
        (IBinConverter<IdType> idConverter
        , IBinConverter<PropertyType> propertyConverter
        , ISeed<IdType> seed
        , ISafeFormatter mapFormatter)
            : this(idConverter
            , propertyConverter
            , seed
            , mapFormatter
            , new RepositoryCacheFactory(-1))
        {

        }

        public IndexRepositoryFactory
            (IBinConverter<IdType> idConverter
            , IBinConverter<PropertyType> propertyConverter
            , ISeed<IdType> seed
            , ISafeFormatter mapFormatter
            , IRepositoryCacheFactory cacheFactory)
            : this(idConverter
            , propertyConverter
            , seed
            , mapFormatter
            , cacheFactory
            , new BatchFileManager<IndexPropertyPair<IdType, PropertyType>>(Environment.SystemPageSize, Environment.SystemPageSize, mapFormatter))
        {

        }

        public IndexRepositoryFactory
            (IBinConverter<IdType> idConverter
            , IBinConverter<PropertyType> propertyConverter
            , ISeed<IdType> seed
            , ISafeFormatter mapFormatter
            , IRepositoryCacheFactory cacheFactory
            , IBatchFileManager<IndexPropertyPair<IdType, PropertyType>> batchManager)
        {
            DefaultIdConverter = idConverter;
            DefaultPropertyConverter = propertyConverter;
            DefaultSeed = seed;
            DefaultMapFormatter = mapFormatter;
            DefaultCacheFactory = cacheFactory;
            DefaultBatchFileManager = batchManager;
        }

        public IIndexRepository<IdType, PropertyType> Create(string fileNamePath)
        {
            return new IndexRepository<IdType, PropertyType>(fileNamePath, DefaultMapFormatter, DefaultCacheFactory, DefaultBatchFileManager);
        }

        public IIndexRepository<IdType, PropertyType> CreateNew(string fileNamePath)
        {
            return new IndexRepository<IdType, PropertyType>(fileNamePath, DefaultSeed, DefaultIdConverter, DefaultPropertyConverter, DefaultMapFormatter, DefaultCacheFactory, DefaultBatchFileManager);
        }
    }
}
