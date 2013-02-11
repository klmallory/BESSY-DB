/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace BESSy.Serialization.Converters
{
    [Serializable]
    public class BinConverter64 : IBinConverter<long>
    {
        public byte[] ToBytes(long item)
        {
            return BitConverter.GetBytes(item);
        }

        public long FromBytes(byte[] bytes)
        {
            return BitConverter.ToInt64(bytes, 0);
        }

        public long FromStream(Stream inStream)
        {
            var bytes = new byte[Length];

            inStream.Read(bytes, 0, bytes.Length);

            return FromBytes(bytes);
        }

        public int Compare(long v1, long v2)
        {
            if (v1 > v2)
                return 1;
            if (v1 < v2)
                return -1;
            if (v1 == v2)
                return 0;

            throw new InvalidOperationException(String.Format("values {0} and {1} could not be compared.", v1, v2));
        }

        public long Min { get { return long.MinValue; } }
        public long Max { get { return long.MaxValue; } }

        public int Length
        {
            get
            {
                return 8;
            }
        }
    }
}
