using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Cache;
using BESSy.Seeding;
using BESSy.Serialization.Converters;

namespace BESSy.Factories
{
    internal class DatabaseCacheFactory
    {
        public IRepositoryCache<IdType, EntityType> Create<IdType, EntityType>() 
        {
            return Create<IdType, EntityType>(8192);
        }

        public IRepositoryCache<IdType, EntityType> Create<IdType, EntityType>(int cacheSize) 
        {
            return Create<IdType, EntityType>(cacheSize, TypeFactory.GetBinConverterFor<IdType>());
        }

        public IRepositoryCache<IdType, EntityType> Create<IdType, EntityType>(int cacheSize, IBinConverter<IdType> converter)
        {
            return new DatabaseCache<IdType, EntityType>(cacheSize, converter);
        }

    }
}
