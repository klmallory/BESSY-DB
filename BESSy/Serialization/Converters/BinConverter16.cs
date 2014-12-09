using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using BESSy.Json;

namespace BESSy.Serialization.Converters
{
    [Serializable]
    public class BinConverter16 : IBinConverter<short>
    {
        public byte[] ToBytes(short item)
        {
            return BitConverter.GetBytes(item);
        }

        public short FromBytes(byte[] bytes)
        {
            return BitConverter.ToInt16(bytes, 0);
        }

        public short FromStream(Stream inStream)
        {
            var bytes = new byte[Length];

            inStream.Read(bytes, 0, bytes.Length);

            return FromBytes(bytes);
        }

        public int Compare(short v1, short v2)
        {
            if (v1 > v2)
                return 1;
            if (v1 < v2)
                return -1;
            if (v1 == v2)
                return 0;

            throw new InvalidOperationException(String.Format("values {0} and {1} could not be compared.", v1, v2));
        }

        [JsonIgnore]
        public short Min { get { return short.MinValue; } }
        [JsonIgnore]
        public short Max { get { return short.MaxValue; } }
        [JsonIgnore]
        public int Length
        {
            get
            {
                return 2;
            }
        }
    }
}
