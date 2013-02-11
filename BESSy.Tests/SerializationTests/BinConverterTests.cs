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
            Assert.AreEqual(50, bin.Length);
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
