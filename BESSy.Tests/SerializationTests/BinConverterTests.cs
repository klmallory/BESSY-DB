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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using BESSy.Files;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using NUnit.Framework;


namespace BESSy.Tests.SerializationTests.ConverterTests
{
    [TestFixture]
    public class BinConverterTests
    {

        [SetUp]
        public void Setup()
        {

        }


        [Test]
        public void BinConverterByteAllRoundTest()
        {
            var bin = new BinConverterByte();

            var bytes = bin.ToBytes(3);

            Assert.AreEqual(3, bin.FromBytes(bytes));
            Assert.AreEqual(3, bin.FromStream(new MemoryStream(bytes)));
            Assert.AreEqual(bytes.Length, bin.Length);
            Assert.AreEqual(1, bin.Length);
            Assert.AreEqual(1, bin.Compare(4, 3));
            Assert.AreEqual(-1, bin.Compare(3, 4));
            Assert.AreEqual(0, bin.Compare(235, 235));

            Assert.AreEqual(byte.MaxValue, bin.Max);
            Assert.AreEqual(byte.MinValue, bin.Min);

        }

        [Test]
        public void BinConverter16AllRoundTest()
        {
            var bin = new BinConverter16();

            var bytes = bin.ToBytes(3);

            Assert.AreEqual(3, bin.FromBytes(bytes));
            Assert.AreEqual(3, bin.FromStream(new MemoryStream(bytes)));
            Assert.AreEqual(bytes.Length, bin.Length);
            Assert.AreEqual(2, bin.Length);
            Assert.AreEqual(1, bin.Compare(4, 3));
            Assert.AreEqual(-1, bin.Compare(3, 4));
            Assert.AreEqual(0, bin.Compare(9487, 9487));

            Assert.AreEqual(short.MaxValue, bin.Max);
            Assert.AreEqual(short.MinValue, bin.Min);

        }

        [Test]
        public void BinConverterU16AllRoundTest()
        {
            var bin = new BinConverterU16();

            var bytes = bin.ToBytes(3);

            Assert.AreEqual(3, bin.FromBytes(bytes));
            Assert.AreEqual(3, bin.FromStream(new MemoryStream(bytes)));
            Assert.AreEqual(bytes.Length, bin.Length);
            Assert.AreEqual(2, bin.Length);
            Assert.AreEqual(1, bin.Compare(4, 3));
            Assert.AreEqual(-1, bin.Compare(3, 4));
            Assert.AreEqual(0, bin.Compare(64875, 64875));

            Assert.AreEqual(ushort.MaxValue, bin.Max);
            Assert.AreEqual(ushort.MinValue, bin.Min);

        }

        [Test]
        public void BinConverter64AllRoundTest()
        {
            var bin = new BinConverter64();

            var bytes= bin.ToBytes(3);

            Assert.AreEqual(3, bin.FromBytes(bytes));
            Assert.AreEqual(3, bin.FromStream(new MemoryStream(bytes)));
            Assert.AreEqual(bytes.Length, bin.Length);
            Assert.AreEqual(8, bin.Length);
            Assert.AreEqual(1, bin.Compare(4, 3));
            Assert.AreEqual(-1, bin.Compare(3, 4));
            Assert.AreEqual(0, bin.Compare(948752397450959734, 948752397450959734));

            Assert.AreEqual(long.MaxValue, bin.Max);
            Assert.AreEqual(long.MinValue, bin.Min);
        }

        [Test]
        public void BinConverterU64AllRoundTest()
        {
            var bin = new BinConverterU64();

            var bytes = bin.ToBytes(3);

            Assert.AreEqual(3, bin.FromBytes(bytes));
            Assert.AreEqual(3, bin.FromStream(new MemoryStream(bytes)));
            Assert.AreEqual(bytes.Length, bin.Length);
            Assert.AreEqual(8, bin.Length);
            Assert.AreEqual(1, bin.Compare(4, 3));
            Assert.AreEqual(-1, bin.Compare(3, 4));
            Assert.AreEqual(0, bin.Compare(948752397450959734, 948752397450959734));

            Assert.AreEqual(ulong.MaxValue, bin.Max);
            Assert.AreEqual(ulong.MinValue, bin.Min);
        }


        [Test]
        public void BinConverter32AllRoundTest()
        {
            var bin = new BinConverter32();

            var bytes = bin.ToBytes(3);

            Assert.AreEqual(3, bin.FromBytes(bytes));
            Assert.AreEqual(3, bin.FromStream(new MemoryStream(bytes)));
            Assert.AreEqual(bytes.Length, bin.Length);
            Assert.AreEqual(4, bin.Length);
            Assert.AreEqual(1, bin.Compare(4, 3));
            Assert.AreEqual(-1, bin.Compare(3, 4));
            Assert.AreEqual(0, bin.Compare(948752397, 948752397));

            Assert.AreEqual(int.MaxValue, bin.Max);
            Assert.AreEqual(int.MinValue, bin.Min);

        }

        [Test]
        public void BinConverterU32AllRoundTest()
        {
            var bin = new BinConverterU32();

            var bytes = bin.ToBytes(3);

            Assert.AreEqual(3, bin.FromBytes(bytes));
            Assert.AreEqual(3, bin.FromStream(new MemoryStream(bytes)));
            Assert.AreEqual(bytes.Length, bin.Length);
            Assert.AreEqual(4, bin.Length);
            Assert.AreEqual(1, bin.Compare(4, 3));
            Assert.AreEqual(-1, bin.Compare(3, 4));
            Assert.AreEqual(0, bin.Compare(948752397, 948752397));

            Assert.AreEqual(uint.MaxValue, bin.Max);
            Assert.AreEqual(uint.MinValue, bin.Min);

        }

        [Test]
        public void BinConverterDecimalAllRoundTest()
        {
            var bin = new BinConverterDecimal();

            var d = (decimal)30034523.0001087365978;

            var bytes = bin.ToBytes(d);

            Assert.AreEqual(d, bin.FromBytes(bytes));
            Assert.AreEqual(d, bin.FromStream(new MemoryStream(bytes)));
            Assert.AreEqual(bytes.Length, bin.Length);
            Assert.AreEqual(16, bin.Length);
            Assert.AreEqual(1, bin.Compare(4, 3));
            Assert.AreEqual(-1, bin.Compare(3, 4));
            Assert.AreEqual(0, bin.Compare(9487, 9487));

            Assert.AreEqual(decimal.MaxValue, bin.Max);
            Assert.AreEqual(decimal.MinValue, bin.Min);

        }

        [Test]
        public void BinConverterDoubleAllRoundTest()
        {
            var bin = new BinConverterDouble();

            var d = (double)30034523.0001087365978;
            var bytes = bin.ToBytes(d);

            Assert.AreEqual(d, bin.FromBytes(bytes));
            Assert.AreEqual(d, bin.FromStream(new MemoryStream(bytes)));
            Assert.AreEqual(bytes.Length, bin.Length);
            Assert.AreEqual(8, bin.Length);
            Assert.AreEqual(1, bin.Compare(4, 3));
            Assert.AreEqual(-1, bin.Compare(3, 4));
            Assert.AreEqual(0, bin.Compare(64875, 64875));

            Assert.AreEqual(double.MaxValue, bin.Max);
            Assert.AreEqual(double.MinValue, bin.Min);

        }

        [Test]
        public void BinConverterFloatAllRoundTest()
        {
            var bin = new BinConverterFloat();

            var f = (float)3003.0001;
            var bytes = bin.ToBytes(f);

            Assert.AreEqual(f, bin.FromBytes(bytes));
            Assert.AreEqual(f, bin.FromStream(new MemoryStream(bytes)));
            Assert.AreEqual(bytes.Length, bin.Length);
            Assert.AreEqual(4, bin.Length);
            Assert.AreEqual(1, bin.Compare(4, 3));
            Assert.AreEqual(-1, bin.Compare(3, 4));
            Assert.AreEqual(0, bin.Compare(64875, 64875));

            Assert.AreEqual(float.MaxValue, bin.Max);
            Assert.AreEqual(float.MinValue, bin.Min);

        }

        [Test]
        public void BinConverterDateTimeAllRoundTest()
        {
            var bin = new BinConverterDateTime();

            var t = DateTime.Now;
            var bytes = bin.ToBytes(t);

            Assert.AreEqual(t, bin.FromBytes(bytes));
            Assert.AreEqual(t, bin.FromStream(new MemoryStream(bytes)));
            Assert.AreEqual(bytes.Length, bin.Length);
            Assert.AreEqual(8, bin.Length);
            Assert.AreEqual(1, bin.Compare(DateTime.Now.AddDays(1), DateTime.Now));
            Assert.AreEqual(-1, bin.Compare(DateTime.Now, DateTime.Now.AddDays(1)));
            Assert.AreEqual(0, bin.Compare(t, t));

            Assert.AreEqual(DateTime.MaxValue, bin.Max);
            Assert.AreEqual(DateTime.MinValue, bin.Min);

        }

        [Test]
        public void BinConverterGuidAllRoundTest()
        {
            var bin = new BinConverterGuid();

            var guid = Guid.NewGuid();
            
            var bytes = bin.ToBytes(guid);
            
            var lessBytes = new byte[16];
            Enumerable.Range(0, 16).ToList().ForEach(e => lessBytes[e] = 3);
            var lessGuid = new Guid(lessBytes);
            var moreBytes = new byte[16];
            Enumerable.Range(0, 16).ToList().ForEach(e => moreBytes[e] = 200);
            var moreGuid = new Guid(moreBytes);

            var maxGuid = new Guid(new byte[16] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 });

            Assert.AreEqual(guid, bin.FromBytes(bytes));
            Assert.AreEqual(guid, bin.FromStream(new MemoryStream(bytes)));
            Assert.AreEqual(bytes.Length, bin.Length);
            Assert.AreEqual(16, bin.Length);
            Assert.AreEqual(1, bin.Compare(moreGuid, lessGuid));
            Assert.AreEqual(-1, bin.Compare(lessGuid, moreGuid));
            Assert.AreEqual(0, bin.Compare(moreGuid, moreGuid));

            Assert.AreEqual(maxGuid, bin.Max);
            Assert.AreEqual(Guid.Empty, bin.Min);

        }

        [Test]
        public void BinConverterStringAllRoundTest()
        {
            var bin = new BinConverterString();

            var text = "lol_~!@#$%^&*()_+";

            var bytes = bin.ToBytes(text);

            Assert.AreEqual(text, bin.FromBytes(bytes));
            Assert.AreEqual(text, bin.FromStream(new MemoryStream(bytes)));
            Assert.AreEqual(bytes.Length, bin.Length);
            Assert.AreEqual(100, bin.Length);
            Assert.AreEqual(1, bin.Compare("Z", "a"));
            Assert.AreEqual(1, bin.Compare("a", null));
            Assert.AreEqual(-1, bin.Compare("a", "Z"));
            Assert.AreEqual(-1, bin.Compare(null, "a"));
            Assert.AreEqual(0, bin.Compare("Z", "Z"));

            Assert.AreEqual(new string(char.MaxValue, 50), bin.Max);
            Assert.AreEqual(String.Empty, bin.Min);
        }
    }
}
