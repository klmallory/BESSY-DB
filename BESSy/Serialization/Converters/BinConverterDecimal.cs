using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Json;
using System.IO;

namespace BESSy.Serialization.Converters
{
    public class BinConverterDecimal : IBinConverter<decimal>
    {
        static byte[] GetBytes(decimal dec)
        {
            //Load four 32 bit integers from the Decimal.GetBits function
            Int32[] bits = decimal.GetBits(dec);
            //Create a temporary list to hold the bytes
            List<byte> bytes = new List<byte>();
            //iterate each 32 bit integer
            foreach (Int32 i in bits)
            {
                //add the bytes of the current 32bit integer
                //to the bytes list
                bytes.AddRange(BitConverter.GetBytes(i));
            }
            //return the bytes list as an array
            return bytes.ToArray();
        }

        static decimal ToDecimal(byte[] bytes)
        {
            //check that it is even possible to convert the array
            if (bytes.Count() != 16)
                throw new Exception("A decimal must be created from exactly 16 bytes");
            //make an array to convert back to int32'aqn
            Int32[] bits = new Int32[4];
            for (int i = 0; i <= 15; i += 4)
            {
                //convert every 4 bytes into an int32
                bits[i / 4] = BitConverter.ToInt32(bytes, i);
            }
            //Use the decimal'aqn new constructor to
            //create an instance of decimal
            return new decimal(bits);
        }

        public byte[] ToBytes(decimal item)
        {
            return GetBytes(item);
        }

        public decimal FromBytes(byte[] bytes)
        {
            return ToDecimal(bytes);
        }

        public decimal FromStream(Stream inStream)
        {
            var bytes = new byte[Length];

            inStream.Read(bytes, 0, bytes.Length);

            return FromBytes(bytes);
        }

        public int Compare(decimal v1, decimal v2)
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
        public decimal Min { get { return decimal.MinValue; } }
        [JsonIgnore]
        public decimal Max { get { return decimal.MaxValue; } }
        [JsonIgnore]
        public int Length
        {
            get
            {
                return 16;
            }
        }
    }
}
