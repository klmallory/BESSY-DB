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

namespace BESSy
{
    public class Catalog<EntityType, IdType, PropertyType> : AbstractMappedCatalog<EntityType, IdType, PropertyType>
    {
        public Catalog
            (string indexFileName
            , string workingFolder
            , Func<EntityType, IdType> getIdDelegate
            , Action<EntityType, IdType> setIdDelegate
            , Func<EntityType, PropertyType> getPropertyDelegate)
        {
            var fileNamePath = Path.Combine(workingFolder, indexFileName);
            WorkingFolder = workingFolder;

            ISeed<IdType> seed;
            TypeFactory.GetTypesFor(out seed, out _idConverter);

            _mapFormatter = new BSONFormatter(BSONFormatter.GetDefaultSettings());
            _fileManager = new BatchFileManager<EntityType>(4096, 4096, _mapFormatter);
            _propertyConverter = TypeFactory.GetBinConverterFor<PropertyType>();

            var indexFileManager = new IndexFileManager<IdType, PropertyType>(4096, _mapFormatter);
            var indexMapManager = new IndexMapManager<IdType, PropertyType>(fileNamePath, _idConverter, _propertyConverter);
            _index = new IndexRepository<IdType, PropertyType>
                (true
                , -1
                , fileNamePath
                , seed
                , _idConverter
                , indexFileManager
                , indexMapManager);

            _getId = getIdDelegate;
            _setId = setIdDelegate;
            _getProperty = getPropertyDelegate;

            DefaultCatalogCacheSize = 8192;
            AutoCache = true;
        }

        public Catalog(
            string workingFolder,
            int defaultCatalogCacheSize,
            ISafeFormatter mapFormatter,
            IBinConverter<IdType> idConverter,
            IBinConverter<PropertyType> propertyConverter,
            IIndexRepository<IdType, PropertyType> index,
            IBatchFileManager<EntityType> fileManager,
            Func<EntityType, IdType> getIdDelegate,
            Action<EntityType, IdType> setIdDelegate,
            Func<EntityType, PropertyType> getPropertyDelegate)
            : base(index, idConverter, propertyConverter)
        {
            WorkingFolder = workingFolder;
            _mapFormatter = mapFormatter;
            _idConverter = idConverter;
            _propertyConverter = propertyConverter;
            _fileManager = fileManager;

            _getId = getIdDelegate;
            _setId = setIdDelegate;
            _getProperty = getPropertyDelegate;

            DefaultCatalogCacheSize = defaultCatalogCacheSize;
            AutoCache = true;
        }

        protected IBatchFileManager<EntityType> _fileManager;
        protected ISafeFormatter _mapFormatter;
        protected Func<EntityType, IdType> _getId;
        protected Action<EntityType, IdType> _setId;
        protected Func<EntityType, PropertyType> _getProperty;

        public string WorkingFolder { get; protected set; }
        public int DefaultCatalogCacheSize { get; protected set; }

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

        protected override IMappedRepository<EntityType, IdType> GetCatalogFile(PropertyType catId)
        {
            string fileNamePath = Path.Combine(GetDirectoryForCatalog(catId), GetFileNameForCatalog(catId));

            return new CatalogRepository<EntityType, IdType>
                (DefaultCatalogCacheSize
                , fileNamePath
                , AutoCache
                , _index.Seed
                , _idConverter
                , _fileManager
                , new IndexedEntityMapManager<EntityType, IdType>(_idConverter, _mapFormatter)
                , _getId
                , _setId);
        }

        public override void Dispose()
        {
            if (_index != null)
                _index.Dispose();

            base.Dispose();
        }
    }
}
