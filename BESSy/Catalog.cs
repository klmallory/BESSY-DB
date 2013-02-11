/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Serialization.Converters;
using BESSy.Serialization;
using BESSy.Files;
using System.IO;
using BESSy.Seeding;
using System.Reflection;
using BESSy.Cache;

namespace BESSy
{
    public class Catalog<EntityType, IdType, PropertyType> : AbstractMappedCatalog<EntityType, IdType, PropertyType>
    {
        static ISafeFormatter DefaultFormatter { get { return new BSONFormatter(); } }

        static IBatchFileManager<IndexPropertyPair<IdType, PropertyType>> DefaultIndexFileManager { get { return new BatchFileManager<IndexPropertyPair<IdType, PropertyType>>(DefaultFormatter); } }

        static IBatchFileManager<EntityType> DefaultRepositoryFileManager { get { return new BatchFileManager<EntityType>(DefaultFormatter); } }

        /// <summary>
        /// Opens an existing catalog with the specified fileName, with the specified working folder.
        /// </summary>
        /// <param name="indexFileName"></param>
        /// <param name="workingFolder"></param>
        public Catalog
            (string indexFileName
            , string workingFolder) 
            
            : this(indexFileName, workingFolder, -1, 8192, DefaultFormatter, DefaultRepositoryFileManager, DefaultIndexFileManager)
        {
        }

        /// <summary>
        /// Opens an existing catalog with the specified fileName, with the specified working folder.
        /// </summary>
        /// <param name="indexFileName"></param>
        /// <param name="workingFolder"></param>
        /// <param name="cacheSize"></param>
        /// <param name="repositoryCacheSize"></param>
        /// <param name="mapFormatter"></param>
        /// <param name="indexFileManager"></param>
        public Catalog
            (string indexFileName
            , string workingFolder
            , int cacheSize
            , int repositoryCacheSize
            , ISafeFormatter mapFormatter
            , IBatchFileManager<EntityType> repositoryFileManager
            , IBatchFileManager<IndexPropertyPair<IdType, PropertyType>> indexFileManager)
        {
            AutoCache = true;
            CacheSize = cacheSize;
            WorkingFolder = workingFolder;

            _fileManager = repositoryFileManager;
            _mapFormatter = mapFormatter;

            _index = new IndexRepository<IdType, PropertyType>(Path.Combine(workingFolder, indexFileName), cacheSize, mapFormatter, indexFileManager);

            DefaultRepositoryCacheSize = repositoryCacheSize;
        }

        /// <summary>
        /// Creates or opens an existing catalog with the specified filename and settings.
        /// </summary>
        /// <param name="indexFileName"></param>
        /// <param name="workingFolder"></param>
        /// <param name="propertyConverter"></param>
        public Catalog(string indexFileName
            , string workingFolder
            , IBinConverter<PropertyType> propertyConverter
            , string getIdMethod
            , string setIdMethod
            , string getPropertyMethod) 

            : this(indexFileName
            , workingFolder
            , -1, 8192
            , TypeFactory.GetBinConverterFor<IdType>()
            , TypeFactory.GetBinConverterFor<PropertyType>()
            , TypeFactory.GetSeedFor<IdType>()
            , getIdMethod
            , setIdMethod
            , getPropertyMethod
            , DefaultFormatter
            , DefaultRepositoryFileManager
            , DefaultIndexFileManager)
        {

        }

        /// <summary>
        /// Creates or opens an existing catalog with the specified filename and settings. 
        /// </summary>
        /// <param name="indexFileName"></param>
        /// <param name="workingFolder"></param>
        /// <param name="cacheSize"></param>
        /// <param name="repositoryCacheSize"></param>
        /// <param name="idConverter"></param>
        /// <param name="propertyConverter"></param>
        /// <param name="seed"></param>
        /// <param name="getIdMethod"></param>
        /// <param name="setIdMethod"></param>
        /// <param name="getPropertyMethod"></param>
        /// <param name="mapFormatter"></param>
        /// <param name="repositoryFileManager"></param>
        /// <param name="indexFileManager"></param>
        public Catalog(string indexFileName
            , string workingFolder
            , int cacheSize
            , int repositoryCacheSize
            , IBinConverter<IdType> idConverter
            , IBinConverter<PropertyType> propertyConverter
            , ISeed<IdType> seed
            , string getIdMethod
            , string setIdMethod
            , string getPropertyMethod
            , ISafeFormatter mapFormatter
            , IBatchFileManager<EntityType> repositoryFileManager
            , IBatchFileManager<IndexPropertyPair<IdType, PropertyType>> indexFileManager)
        {
            AutoCache = true;
            CacheSize = cacheSize;
            WorkingFolder = workingFolder;

            _fileManager = repositoryFileManager;
            _mapFormatter = mapFormatter;
           
            seed.GetIdMethod = getIdMethod;
            seed.SetIdMethod = setIdMethod;
            seed.GetCategoryIdMethod = getPropertyMethod;

            _index = new IndexRepository<IdType, PropertyType>(cacheSize, Path.Combine(workingFolder, indexFileName), seed, idConverter, propertyConverter, mapFormatter, indexFileManager);

            DefaultRepositoryCacheSize = repositoryCacheSize;
        }

        protected IBatchFileManager<EntityType> _fileManager;
        protected ISafeFormatter _mapFormatter;
        protected Func<EntityType, IdType> _getId;
        protected Action<EntityType, IdType> _setId;
        protected Func<EntityType, PropertyType> _getProperty;

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

        public override void Load()
        {
            base.Load();

            _getId = (Func<EntityType, IdType>)Delegate.CreateDelegate(typeof(Func<EntityType, IdType>), typeof(EntityType).GetMethod(_index.Seed.GetIdMethod));
            _setId = (Action<EntityType, IdType>)Delegate.CreateDelegate(typeof(Action<EntityType, IdType>), typeof(EntityType).GetMethod(_index.Seed.SetIdMethod));
            _getProperty = (Func<EntityType, PropertyType>)Delegate.CreateDelegate(typeof(Func<EntityType, PropertyType>), typeof(EntityType).GetMethod(_index.Seed.GetCategoryIdMethod));

            if (CacheSize < 1)
                CacheSize = Caching.DetermineOptimumCacheSize(_index.Seed.Stride * DefaultRepositoryCacheSize);
        }

        protected override IMappedRepository<EntityType, IdType> GetCatalogFile(PropertyType catId)
        {
            string fileNamePath = Path.Combine(GetDirectoryForCatalog(catId), GetFileNameForCatalog(catId));

            return new CatalogRepository<EntityType, IdType>
                (DefaultRepositoryCacheSize
                , fileNamePath
                , AutoCache
                , _index.Seed
                , _idConverter
                , _mapFormatter
                , _fileManager);
        }
    }
}
