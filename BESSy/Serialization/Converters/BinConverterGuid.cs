/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Serialization.Converters
{
    public class BinConverterGuid : IBinConverter<Guid>
    {
        public byte[] ToBytes(Guid item)
        {
            return item.ToByteArray();
        }

        public Guid FromBytes(byte[] bytes)
        {
            return new Guid(bytes);
        }

        public int Length
        {
            get { return 16; }
        }

        public int Compare(Guid item1, Guid item2)
        {
            return item1.CompareTo(item2);
        }

    }
}
