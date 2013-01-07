using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Serialization.Converters;

namespace BESSy.Seeding
{
    internal static class TypeFactory
    {
        internal static void GetTypesFor<IdType>(out ISeed<IdType> seed, out IBinConverter<IdType> binConverter)
        {
            if (typeof(IdType).Equals(typeof(Int32)))
            {
                seed = (ISeed<IdType>)((object)new Seed32());
                binConverter = (IBinConverter<IdType>)((object)new BinConverter32());
            }
            else if (typeof(IdType).Equals(typeof(Int64)))
            {
                seed = (ISeed<IdType>)((object)new Seed64());
                binConverter = (IBinConverter<IdType>)((object)new BinConverter64());
            }
            else if (typeof(IdType).Equals(typeof(Guid)))
            {
                seed = (ISeed<IdType>)((object)new SeedGuid());
                binConverter = (IBinConverter<IdType>)((object)new BinConverterGuid());
            }
            else if (typeof(IdType).Equals(typeof(String)))
            {
                seed = (ISeed<IdType>)((object)new SeedString());
                binConverter = (IBinConverter<IdType>)((object)new BinConverterString());
            }
            else throw new ArgumentException(string.Format("{0} is not a known type, use overloaded constructor.", typeof(IdType)));
        }

        internal static IBinConverter<PropertyType> GetBinConverterFor<PropertyType>()
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
