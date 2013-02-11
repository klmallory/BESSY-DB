using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Serialization.Converters;
using BESSy.Seeding;
using BESSy.Files;
using BESSy.Serialization;

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
            ISafeFormatter mapFormatter,
            IBatchFileManager<EntityType> fileManager)

            : base(cacheSize
            , fileName
            , autoCache
            , seed
            , idConverter
            , mapFormatter
            , fileManager)
        {

        }

        protected override Seeding.ISeed<IdType> LoadSeed(System.IO.Stream stream)
        {
            return default(ISeed<IdType>);
        }

        public override int Load()
        {
            return base.Load();
        }

        protected override void InitializeDatabase(ISeed<IdType> seed, int count)
        {
            base.InitializeDatabase(seed, count);
        }

        //In catalogs, the seed is not managed by the individual repositories.
        //TODO: find a better way to do this than parasitical inheritance.
        protected override long SaveSeed(System.IO.Stream f)
        {
            if (_mapFileManager.Stride > _seed.Stride)
                _seed.Stride = _mapFileManager.Stride;

            return 0;
        }
    }
}
