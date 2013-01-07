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
    public class BinConverterString : IBinConverter<String>
    {
        public BinConverterString() : this(50)
        {

        }

        private char[] trimChararcters = new char[] { '\0' };

        public BinConverterString(int maxLength)
        {
            maxLen = maxLength;

            var litmus = new string('Z', maxLen);

            var b = Encoding.ASCII.GetBytes(litmus);

            Length = b.Length;
        }

        int maxLen {get; set;}

        public byte[] ToBytes(string item)
        {
            if (item == null)
                return new byte[Length];

            var b = Encoding.ASCII.GetBytes(item.Substring(0, (item.Length > maxLen ? maxLen : item.Length)));

            Array.Resize(ref b, Length);

            return b;
        }

        public string FromBytes(byte[] bytes)
        {
            return Encoding.ASCII.GetString(bytes).TrimEnd(trimChararcters);
        }

        public string Min { get { return String.Empty; } }
        public string Max { get { return new string(char.MaxValue, maxLen); } }

        public int Length {get; private set;}

        /// <summary>
        /// Compares string item1 against string item2.
        /// </summary>
        /// <param name="item1"></param>
        /// <param name="item2"></param>
        /// <returns>1 if item is greater than item2, 0 if they are equal, and -1 if item2 is greater than item 1</returns>
        public int Compare(string item1, string item2)
        {
            if (item1 == item2)
                return 0;
            if (item1 == null && item2 != null)
                return -1;
            if (item1 != null && item2 == null)
                return 1;

            return string.Compare(item1, 0, item2, 0, maxLen, false); 
        }

    }
}
