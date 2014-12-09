using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using BESSy.Json;

namespace BESSy.Serialization.Converters
{
    public class BinConverterFloat : IBinConverter<float>
    {
        public byte[] ToBytes(float item)
        {
            return BitConverter.GetBytes(item);
        }

        public float FromBytes(byte[] bytes)
        {
            return BitConverter.ToSingle(bytes, 0);
        }

        public float FromStream(Stream inStream)
        {
            var bytes = new byte[Length];

            inStream.Read(bytes, 0, bytes.Length);

            return FromBytes(bytes);
        }

        public int Compare(float v1, float v2)
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
        public float Min { get { return float.MinValue; } }
        [JsonIgnore]
        public float Max { get { return float.MaxValue; } }
        [JsonIgnore]
        public int Length
        {
            get
            {
                return 4;
            }
        }
    }
}
