﻿/*
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
    public class BinConverterGuid : IBinConverter<Guid>
    {
        static readonly Guid _maxGuid = new Guid(new byte[16] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 });

        public byte[] ToBytes(Guid item)
        {
            return item.ToByteArray();
        }

        public Guid FromBytes(byte[] bytes)
        {
            return new Guid(bytes);
        }

        public Guid FromStream(Stream inStream)
        {
            var bytes = new byte[Length];

            inStream.Read(bytes, 0, bytes.Length);

            return FromBytes(bytes);
        }

        [JsonIgnore]
        public Guid Min { get { return Guid.Empty; } }
        [JsonIgnore]
        public Guid Max { get { return _maxGuid; } }
        [JsonIgnore]
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
