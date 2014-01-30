/*
Copyright (c) 2011,2012,2013 Kristen Mallory dba Klink

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using BESSy.Json;

namespace BESSy.Serialization.Converters
{
    [Serializable]
    public class BinConverterString : IBinConverter<String>
    {
        public BinConverterString()
            : this(50)
        {
            
        }

        public BinConverterString(int maxLength)
        {
            maxLen = maxLength;

            _max = new string(char.MaxValue, maxLen);

            var b = Encoding.Unicode.GetBytes(_max);

            Length = b.Length;
        }

        char[] trimChararcters = new char[] { '\0' };
        string _max;

        [JsonProperty]
        int maxLen { get; set; }

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

        public string FromStream(Stream inStream)
        {
            var bytes = new byte[Length];

            inStream.Read(bytes, 0, bytes.Length);

            return FromBytes(bytes);
        }

        [JsonIgnore]
        public string Min { get { return String.Empty; } }
        [JsonIgnore]
        public string Max { get { return _max; } }

        [JsonProperty]
        public int Length { get; private set; }

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
