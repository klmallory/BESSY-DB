using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Serialization.Converters;
using BESSy.Seeding;
using BESSy.Files;

namespace BESSy
{
    internal class CatalogRepository<EntityType, IdType> : Repository<EntityType, IdType>
    {
        public CatalogRepository
            (int cacheSize,
            string fileName,
            bool autoCache,
            ISeed<IdType> seed,
            IBinConverter<IdType> idConverter,
            IBatchFileManager<EntityType> fileManager,
            IIndexedEntityMapManager<EntityType, IdType> mapFileManager,
            Func<EntityType, IdType> getUniqueIdDelegate,
            Action<EntityType, IdType> setUniqueIdDelegate)

            : base(cacheSize
            , fileName
            , autoCache
            , seed
            , idConverter
            , fileManager
            , mapFileManager
            , getUniqueIdDelegate
            , setUniqueIdDelegate)
        {

        }

        protected override Seeding.ISeed<IdType> LoadSeed(System.IO.Stream stream)
        {
            return default(ISeed<IdType>);
        }

        //In catalogs, the seed is not managed by the individual repositories.
        //TODO: find a better way to do this than parasitical inheritance.
        protected override long SaveSeed(System.IO.Stream f)
        {
            return 0;
        }
    }
}
