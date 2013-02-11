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
    public class BinConverter32 : IBinConverter<int>
    {
        public byte[] ToBytes(int item)
        {
            return BitConverter.GetBytes(item);
        }

        public int FromBytes(byte[] bytes)
        {
            return BitConverter.ToInt32(bytes, 0);
        }

        public int FromStream(Stream inStream)
        {
            var bytes = new byte[Length];

            inStream.Read(bytes, 0, bytes.Length);

            return FromBytes(bytes);
        }

        public int Compare(int v1, int v2)
        {
            if (v1 > v2)
                return 1;
            if (v1 < v2)
                return -1;
            if (v1 == v2)
                return 0;

            throw new InvalidOperationException(String.Format("values {0} and {1} could not be compared.", v1, v2));
        }

        public int Min { get { return int.MinValue; } }
        public int Max { get { return int.MaxValue; } }

        public int Length
        {
            get
            {
                return 4;
            }
        }
    }
}
