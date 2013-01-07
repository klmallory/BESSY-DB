/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Seeding;
using BESSy.Serialization.Converters;
using BESSy.Files;
using BESSy.Serialization;

namespace BESSy
{
    public class Repository<EntityType, IdType> : AbstractMappedRepository<EntityType, IdType>
    {
        public Repository
            (string fileName,
            Func<EntityType, IdType> getUniqueIdDelegate,
            Action<EntityType, IdType> setUniqueIdDelegate) : base(fileName)
        {
            AutoCache = true;

            TypeFactory.GetTypesFor(out _seed, out _idConverter);

            var formatter = new BSONFormatter(BSONFormatter.GetDefaultSettings());
            _fileManager = new BatchFileManager<EntityType>(2048, formatter);
            _mapFileManager = new IndexedEntityMapManager<EntityType, IdType>(_idConverter, formatter);
            _create = true;

            DetermineOptimumCacheSize();

            _getUniqueId = getUniqueIdDelegate;
            _setUniqueId = setUniqueIdDelegate;

            _mapFileManager.OnFlushCompleted += new FlushCompleted<EntityType, IdType>(HandleOnFlushCompleted);
        }

        public Repository
            (string fileName,
            bool autoCache,
            ISeed<IdType> seed,
            IBinConverter<IdType> idConverter,
            IBatchFileManager<EntityType> fileManager,
            IIndexedEntityMapManager<EntityType, IdType> mapFileManager,
            Func<EntityType, IdType> getUniqueIdDelegate,
            Action<EntityType, IdType> setUniqueIdDelegate)

            : base(true
            , -1
            , fileName
            , seed
            , idConverter
            , fileManager
            , mapFileManager)
        {
            AutoCache = autoCache;

            _getUniqueId = getUniqueIdDelegate;
            _setUniqueId = setUniqueIdDelegate;
        }

        public Repository
            (int cacheSize,
            string fileName,
            bool autoCache,
            ISeed<IdType> seed,
            IBinConverter<IdType> idConverter,
            IBatchFileManager<EntityType> fileManager,
            IIndexedEntityMapManager<EntityType, IdType> mapFileManager,
            Func<EntityType, IdType> getUniqueIdDelegate,
            Action<EntityType, IdType> setUniqueIdDelegate)

            : base(true
            , cacheSize
            , fileName
            , seed
            , idConverter
            , fileManager
            , mapFileManager)
        {
            AutoCache = autoCache;

            _getUniqueId = getUniqueIdDelegate;
            _setUniqueId = setUniqueIdDelegate;
        }


        Func<EntityType, IdType> _getUniqueId;
        Action<EntityType, IdType> _setUniqueId;

        protected override IdType GetIdFrom(EntityType item)
        {
            return _getUniqueId(item);
        }

        protected override void SetIdFor(EntityType item, IdType id)
        {
            _setUniqueId(item, id);
        }
    }
}
