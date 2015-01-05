using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Serialization.Converters;

namespace BESSy.Seeding
{
    public static class TypeFactory
    {
        public static IFileCore<IdType, SegmentType> GetFileCoreFor<IdType, SegmentType>()
        {
            var core = new FileCore<IdType, SegmentType>();

            core.IdSeed = GetSeedFor<IdType>();
            core.SegmentSeed = GetSeedFor<SegmentType>();

            core.IdConverter = GetBinConverterFor<IdType>();
            core.PropertyConverter = GetBinConverterFor<SegmentType>();

            return core;
        }

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
            else if (typeof(PropertyType).Equals(typeof(Int16)))
                return (IBinConverter<PropertyType>)((object)new BinConverter16());
            else if (typeof(PropertyType).Equals(typeof(Int64)))
                return (IBinConverter<PropertyType>)((object)new BinConverter64());
            else if (typeof(PropertyType).Equals(typeof(UInt16)))
                return (IBinConverter<PropertyType>)((object)new BinConverterU16());
            else if (typeof(PropertyType).Equals(typeof(UInt32)))
                return (IBinConverter<PropertyType>)((object)new BinConverterU32());
            else if (typeof(PropertyType).Equals(typeof(UInt64)))
                return (IBinConverter<PropertyType>)((object)new BinConverterU64());
            else if (typeof(PropertyType).Equals(typeof(float)))
                return (IBinConverter<PropertyType>)((object)new BinConverterFloat());
            else if (typeof(PropertyType).Equals(typeof(double)))
                return (IBinConverter<PropertyType>)((object)new BinConverterDouble());
            else if (typeof(PropertyType).Equals(typeof(decimal)))
                return (IBinConverter<PropertyType>)((object)new BinConverterDecimal());
            else if (typeof(PropertyType).Equals(typeof(Guid)))
                return (IBinConverter<PropertyType>)((object)new BinConverterGuid());
            else if (typeof(PropertyType).Equals(typeof(String)))
                return (IBinConverter<PropertyType>)((object)new BinConverterString());

            else throw new ArgumentException(string.Format("{0} is not a known type, use overloaded constructor.", typeof(PropertyType)));
        }

        public static object GetBinConverterFor(Type type)
        {
            if (type.Equals(typeof(Int32)))
                return new BinConverter32();
            else if (type.Equals(typeof(Int16)))
                return new BinConverter16();
            else if (type.Equals(typeof(Int64)))
                return new BinConverter64();
            else if (type.Equals(typeof(UInt16)))
                return new BinConverterU16();
            else if (type.Equals(typeof(UInt32)))
                return new BinConverterU32();
            else if (type.Equals(typeof(UInt64)))
                return new BinConverterU64();
            else if (type.Equals(typeof(float)))
                return new BinConverterFloat();
            else if (type.Equals(typeof(double)))
                return new BinConverterDouble();
            else if (type.Equals(typeof(decimal)))
                return new BinConverterDecimal();
            else if (type.Equals(typeof(Guid)))
                return new BinConverterGuid();
            else if (type.Equals(typeof(String)))
                return new BinConverterString();

            else throw new ArgumentException(string.Format("{0} is not a known type, use overloaded constructor.", type));
        }
    }
}
