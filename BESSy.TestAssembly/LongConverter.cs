using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Serialization.Converters;

namespace BESSy.TestAssembly
{
    public class LongConverter : IBinConverter<long>
    {
        public long Min
        {
            get { return long.MinValue; }
        }

        public long Max
        {
            get { return long.MaxValue; }
        }

        public byte[] ToBytes(long item)
        {
            return BitConverter.GetBytes(item);
        }

        public long FromBytes(byte[] bytes)
        {
            return BitConverter.ToUInt32(bytes, 0);
        }

        public long FromStream(System.IO.Stream inStream)
        {
            var bytes = new byte[Length];

            inStream.Read(bytes, 0, bytes.Length);

            return FromBytes(bytes);
        }

        public int Length
        {
            get { return 4; }
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
    }
}
