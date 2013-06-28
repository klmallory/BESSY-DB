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

namespace BESSy
{
    public class Catalog<EntityType, IdType, PropertyType> : AbstractMappedCatalog<EntityType, IdType, PropertyType>
    {
        static ISafeFormatter DefaultFormatter { get { return new BSONFormatter(); } }
        static IBatchFileManager<IndexPropertyPair<IdType, PropertyType>> DefaultIndexFileManager { get { return new BatchFileManager<IndexPropertyPair<IdType, PropertyType>>(DefaultFormatter); } }
        static IBatchFileManager<EntityType> DefaultRepositoryFileManager { get { return new BatchFileManager<EntityType>(DefaultFormatter); } }
        static IIndexRepositoryFactory<IdType, PropertyType> DefaultIndexFactory { get { return new IndexRepositoryFactory<IdType, PropertyType>(); } }

        /// <summary>
        /// Opens an existing catalog with the specified fileName, with the specified working folder.
        /// </summary>
        /// <param name="indexFileName"></param>
        /// <param name="workingFolder"></param>
        public Catalog
            (string indexFileName
            , string workingFolder)

            : this(indexFileName, workingFolder, 8192, DefaultFormatter, DefaultRepositoryFileManager, DefaultIndexFactory.DefaultCacheFactory, DefaultIndexFactory)
        {

        }

        /// <summary>
        /// Opens an existing catalog with the specified fileName, with the specified working folder.
        /// </summary>
        /// <param name="indexFileName"></param>
        /// <param name="workingFolder"></param>
        /// <param name="repositoryCacheSize"></param>
        /// <param name="mapFormatter"></param>
        /// <param name="indexFileManager"></param>
        public Catalog
            (string indexFileName
            , string workingFolder
            , int repositoryCacheSize
            , ISafeFormatter mapFormatter
            , IBatchFileManager<EntityType> repositoryFileManager
            , IRepositoryCacheFactory cacheFactory
            , IIndexRepositoryFactory<IdType, PropertyType> indexFactory)
        {
            WorkingFolder = workingFolder;

            _indexFileNamePath = Path.Combine(workingFolder, indexFileName);
            _cacheFactory = cacheFactory;
            _indexFactory = indexFactory;

            _fileManager = repositoryFileManager;
            _mapFormatter = mapFormatter;

            DefaultRepositoryCacheSize = repositoryCacheSize;
        }

        /// <summary>
        /// Creates or opens an existing catalog with the specified filename and settings.
        /// </summary>
        /// <param name="indexFileName"></param>
        /// <param name="workingFolder"></param>
        /// <param name="propertyConverter"></param>
        /// <param name="idPropertyName"></param>
        /// <param name="setIdMethod"></param>
        /// <param name="categoryIdPropertyName"></param>
        public Catalog(string indexFileName
            , string workingFolder
            , IBinConverter<PropertyType> propertyConverter
            , string idPropertyName
            , string categoryIdPropertyName) 

            : this(indexFileName
            , workingFolder
            , 8192
            , TypeFactory.GetBinConverterFor<IdType>()
            , TypeFactory.GetBinConverterFor<PropertyType>()
            , idPropertyName
            , categoryIdPropertyName
            , DefaultFormatter
            , DefaultRepositoryFileManager
            , DefaultIndexFactory.DefaultCacheFactory
            , DefaultIndexFactory)
        {

        }


        
        /// <summary>
        /// Creates or opens an existing catalog with the specified filename and settings.
        /// </summary>
        /// <param name="indexFileName"></param>
        /// <param name="workingFolder"></param>
        /// <param name="repositoryCacheSize"></param>
        /// <param name="idConverter"></param>
        /// <param name="propertyConverter"></param>
        /// <param name="idPropertyName"></param>
        /// <param name="categoryIdPropertyName"></param>
        /// <param name="mapFormatter"></param>
        /// <param name="repositoryFileManager"></param>
        /// <param name="cacheFactory"></param>
        /// <param name="indexFactory"></param>
        public Catalog(string indexFileName
            , string workingFolder
            , int repositoryCacheSize
            , IBinConverter<IdType> idConverter
            , IBinConverter<PropertyType> propertyConverter
            , string idPropertyName
            , string categoryIdPropertyName
            , ISafeFormatter mapFormatter
            , IBatchFileManager<EntityType> repositoryFileManager
            , IRepositoryCacheFactory cacheFactory
            , IIndexRepositoryFactory<IdType, PropertyType> indexFactory)
        {
            WorkingFolder = workingFolder;

            _indexFileNamePath = Path.Combine(workingFolder, indexFileName);
            _cacheFactory = cacheFactory;
            _indexFactory = indexFactory;

            _fileManager = repositoryFileManager;
            _mapFormatter = mapFormatter;

            _idConverter = idConverter;
            _propertyConverter = propertyConverter;

            _indexFactory.DefaultIdConverter = idConverter;
            _indexFactory.DefaultPropertyConverter = propertyConverter;
            _indexFactory.DefaultSeed.IdProperty = idPropertyName;
            _indexFactory.DefaultSeed.CategoryIdProperty = categoryIdPropertyName;

            DefaultRepositoryCacheSize = repositoryCacheSize;
        }

        public string WorkingFolder { get; protected set; }
        public int DefaultRepositoryCacheSize { get; protected set; }

        protected override PropertyType GetCatalogIdFrom(EntityType item)
        {
            return _getProperty(item);
        }

        protected override IdType GetIdFrom(EntityType item)
        {
            return _getId(item);
        }

        protected virtual string GetDirectoryForCatalog(PropertyType catId)
        {
            return WorkingFolder;
        }

        protected virtual string GetFileNameForCatalog(PropertyType catId)
        {
            return catId.ToString() + ".catalog";
        }

        protected override IIndexedRepository<EntityType, IdType> GetCatalogFile(PropertyType catId)
        {
            string fileNamePath = Path.Combine(GetDirectoryForCatalog(catId), GetFileNameForCatalog(catId));

            return new CatalogRepository<EntityType, IdType>
                (DefaultRepositoryCacheSize
                , fileNamePath
                , true
                , _index.Seed
                , _idConverter
                , _mapFormatter
                , _fileManager);
        }
    }
}
