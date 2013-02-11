using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Serialization.Converters;

namespace BESSy.Seeding
{
    public static class TypeFactory
    {
        public static ISeed<IdType> GetSeedFor<IdType>()
        {
            if (typeof(IdType).Equals(typeof(Int32)))
                return (ISeed<IdType>)((object)new Seed32());
            else if (typeof(IdType).Equals(typeof(Int64)))
                return (ISeed<IdType>)((object)new Seed64());
            else if (typeof(IdType).Equals(typeof(Guid)))
                return (ISeed<IdType>)((object)new SeedGuid());
            else if (typeof(IdType).Equals(typeof(String)))
                return (ISeed<IdType>)((object)new SeedString());
            else throw new ArgumentException(string.Format("{0} is not a known type, use overloaded constructor.", typeof(IdType)));
        }

        public static IBinConverter<PropertyType> GetBinConverterFor<PropertyType>()
        {
            if (typeof(PropertyType).Equals(typeof(Int32)))
                return (IBinConverter<PropertyType>)((object)new BinConverter32());
            else if (typeof(PropertyType).Equals(typeof(Int64)))
                return (IBinConverter<PropertyType>)((object)new BinConverter64());
            else if (typeof(PropertyType).Equals(typeof(Guid)))
                return (IBinConverter<PropertyType>)((object)new BinConverterGuid());
            else if (typeof(PropertyType).Equals(typeof(String)))
                return (IBinConverter<PropertyType>)((object)new BinConverterString());
            else throw new ArgumentException(string.Format("{0} is not a known type, use overloaded constructor.", typeof(PropertyType)));
        }
    }
}
