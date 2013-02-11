using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Files;
using BESSy.Serialization.Converters;
using BESSy.Serialization;

namespace BESSy.Factories
{
    public class MapManangerFactory
    {
        public IIndexedEntityMapManager<EntityType, IdType> Create<IdType, EntityType>(IBinConverter<IdType> idConverter, ISafeFormatter formatter)
        {
            return new IndexedEntityMapManager<EntityType, IdType>(idConverter, formatter);
        }
    }
}
