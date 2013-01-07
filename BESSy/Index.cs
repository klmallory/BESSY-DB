using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Serialization.Converters;

namespace BESSy
{
    public struct Index<IdType, PropertyType, EntityType>
    {
        public string Name;
        public Func<EntityType, PropertyType> PropertyGet;
        public Func<EntityType, IdType> IdGet;
        public IBinConverter<PropertyType> PropertyConverter;
    }
}
