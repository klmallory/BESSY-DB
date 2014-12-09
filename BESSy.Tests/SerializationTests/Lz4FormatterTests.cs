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
using System.Threading;
using System.Linq;
using BESSy.Files;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using NUnit.Framework;
using System.Text;

namespace BESSy.Tests.SerializationTests
{
    [TestFixture]
    public class Lz4FormatterTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Lz4FormatsToBytes()
        {
            var test = TestResourceFactory.CreateRandom() as MockClassC;
            var zip = new LZ4ZipFormatter(TestResourceFactory.CreateBsonFormatter());

            var formatted = zip.FormatObj(test);

            var unformatted = zip.UnformatObj<MockClassA>(formatted) as MockClassC;

            Assert.AreEqual(unformatted.Id, test.Id);
            Assert.AreEqual(unformatted.Name, test.Name);
            Assert.AreEqual(unformatted.GetSomeCheckSum[0], test.GetSomeCheckSum[0]);
            Assert.AreEqual(unformatted.Location.X, test.Location.X);
            Assert.AreEqual(unformatted.Location.Y, test.Location.Y);
            Assert.AreEqual(unformatted.Location.Z, test.Location.Z);
            Assert.AreEqual(unformatted.Location.W, test.Location.W);
            Assert.AreEqual(unformatted.ReferenceCode, test.ReferenceCode);
            Assert.AreEqual(unformatted.ReplicationID, test.ReplicationID);
        }

        [Test]
        public void Lz4FormatsToStream()
        {
            var test = TestResourceFactory.CreateRandom() as MockClassC;
            var zip = new LZ4ZipFormatter(TestResourceFactory.CreateBsonFormatter());

            var formatted = zip.FormatObjStream(test);

            var unformatted = zip.UnformatObj<MockClassA>(formatted) as MockClassC;

            var con = zip.AsQueryableObj(test);

            Assert.AreEqual(unformatted.Id, test.Id);
            Assert.AreEqual(unformatted.Name, test.Name);
            Assert.AreEqual(unformatted.GetSomeCheckSum[0], test.GetSomeCheckSum[0]);
            Assert.AreEqual(unformatted.Location.X, test.Location.X);
            Assert.AreEqual(unformatted.Location.Y, test.Location.Y);
            Assert.AreEqual(unformatted.Location.Z, test.Location.Z);
            Assert.AreEqual(unformatted.Location.W, test.Location.W);
            Assert.AreEqual(unformatted.ReferenceCode, test.ReferenceCode);
            Assert.AreEqual(unformatted.ReplicationID, test.ReplicationID);
        }

        [Test]
        public void Lz4SafeFormatsToBytes()
        {
            var test = TestResourceFactory.CreateRandom() as MockClassC;
            var zip = new LZ4ZipFormatter(TestResourceFactory.CreateBsonFormatter());

            byte[] formatted;

            Assert.IsTrue(zip.TryFormatObj(test, out formatted));

            MockClassA raw;

            Assert.IsTrue(zip.TryUnformatObj<MockClassA>(formatted, out raw));

            var unformatted = raw as MockClassC;

            Assert.AreEqual(unformatted.Id, test.Id);
            Assert.AreEqual(unformatted.Name, test.Name);
            Assert.AreEqual(unformatted.GetSomeCheckSum[0], test.GetSomeCheckSum[0]);
            Assert.AreEqual(unformatted.Location.X, test.Location.X);
            Assert.AreEqual(unformatted.Location.Y, test.Location.Y);
            Assert.AreEqual(unformatted.Location.Z, test.Location.Z);
            Assert.AreEqual(unformatted.Location.W, test.Location.W);
            Assert.AreEqual(unformatted.ReferenceCode, test.ReferenceCode);
            Assert.AreEqual(unformatted.ReplicationID, test.ReplicationID);

            var invalid = Encoding.UTF8.GetBytes("{ this is invalid.");

            //Check false conditions.
            Assert.IsFalse(zip.TryFormatObj(default(MockClassA), out formatted));
            Assert.IsFalse(zip.TryUnformatObj((byte[])null, out raw));
            Assert.IsFalse(zip.TryUnformatObj(invalid, out raw));
        }

        [Test]
        public void Lz4SafeFormatsToStream()
        {
            var test = TestResourceFactory.CreateRandom() as MockClassC;
            var zip = new LZ4ZipFormatter(TestResourceFactory.CreateBsonFormatter());

            Stream formatted;

            Assert.IsTrue(zip.TryFormatObj(test, out formatted));

            MockClassA raw;

            Assert.IsTrue(zip.TryUnformatObj<MockClassA>(formatted, out raw));

            var unformatted = raw as MockClassC;

            Assert.AreEqual(unformatted.Id, test.Id);
            Assert.AreEqual(unformatted.Name, test.Name);
            Assert.AreEqual(unformatted.GetSomeCheckSum[0], test.GetSomeCheckSum[0]);
            Assert.AreEqual(unformatted.Location.X, test.Location.X);
            Assert.AreEqual(unformatted.Location.Y, test.Location.Y);
            Assert.AreEqual(unformatted.Location.Z, test.Location.Z);
            Assert.AreEqual(unformatted.Location.W, test.Location.W);
            Assert.AreEqual(unformatted.ReferenceCode, test.ReferenceCode);
            Assert.AreEqual(unformatted.ReplicationID, test.ReplicationID);

            var invalid = Encoding.UTF8.GetBytes("{ this is invalid.");

            //Check false conditions.
            Assert.IsFalse(zip.TryFormatObj(default(MockClassA), out formatted));
            Assert.IsFalse(zip.TryUnformatObj((byte[])null, out raw));
            Assert.IsFalse(zip.TryUnformatObj(invalid, out raw));
        }

        [Test]
        public void Lz4BinFormatsToBytes()
        {
            var test = TestResourceFactory.CreateRandom() as MockClassC;
            var bson = new BSONFormatter();
            var zip = new LZ4ZipFormatter(bson);

            var bytes = bson.FormatObj(test);

            var binFormatted = zip.Format(bytes);
            var buffer = zip.Unformat(binFormatted);

            var unformatted = bson.UnformatObj<MockClassA>(buffer) as MockClassC;

            Assert.AreEqual(unformatted.Id, test.Id);
            Assert.AreEqual(unformatted.Name, test.Name);
            Assert.AreEqual(unformatted.GetSomeCheckSum[0], test.GetSomeCheckSum[0]);
            Assert.AreEqual(unformatted.Location.X, test.Location.X);
            Assert.AreEqual(unformatted.Location.Y, test.Location.Y);
            Assert.AreEqual(unformatted.Location.Z, test.Location.Z);
            Assert.AreEqual(unformatted.Location.W, test.Location.W);
            Assert.AreEqual(unformatted.ReferenceCode, test.ReferenceCode);
            Assert.AreEqual(unformatted.ReplicationID, test.ReplicationID);
        }

        [Test]
        public void Lz4BinFormatsToStream()
        {
            var test = TestResourceFactory.CreateRandom() as MockClassC;
            var bson = new BSONFormatter();
            var zip = new LZ4ZipFormatter(bson);

            var bytes = bson.FormatObjStream(test);

            var binFormatted = zip.Format(bytes);
            var buffer = zip.Unformat(binFormatted);

            var unformatted = bson.UnformatObj<MockClassA>(buffer) as MockClassC;

            Assert.AreEqual(unformatted.Id, test.Id);
            Assert.AreEqual(unformatted.Name, test.Name);
            Assert.AreEqual(unformatted.GetSomeCheckSum[0], test.GetSomeCheckSum[0]);
            Assert.AreEqual(unformatted.Location.X, test.Location.X);
            Assert.AreEqual(unformatted.Location.Y, test.Location.Y);
            Assert.AreEqual(unformatted.Location.Z, test.Location.Z);
            Assert.AreEqual(unformatted.Location.W, test.Location.W);
            Assert.AreEqual(unformatted.ReferenceCode, test.ReferenceCode);
            Assert.AreEqual(unformatted.ReplicationID, test.ReplicationID);
        }

        [Test]
        public void Lz4ParsesJObjectFromStream()
        {
            var arraySettings = BSONFormatter.GetDefaultSettings();
            arraySettings.TypeNameHandling = BESSy.Json.TypeNameHandling.Objects;

            var bson = new BSONFormatter(arraySettings);
            var zip = new LZ4ZipFormatter(bson);

            var test = TestResourceFactory.CreateRandom() as MockClassC;

            var stream = zip.FormatObjStream(test);

            var unformatted = zip.Parse(stream);

            Assert.AreEqual(unformatted.Value<int>("Id"), test.Id);
            Assert.AreEqual(unformatted.Value<string>("Name"), test.Name);
            Assert.AreEqual((double)unformatted["GetSomeCheckSum"][0], test.GetSomeCheckSum[0]);
            Assert.AreEqual((double)unformatted["Location"]["X"], test.Location.X);
            Assert.AreEqual((double)unformatted["Location"]["Y"], test.Location.Y);
            Assert.AreEqual((double)unformatted["Location"]["Z"], test.Location.Z);
            Assert.AreEqual((double)unformatted["Location"]["W"], test.Location.W);
            Assert.AreEqual((string)unformatted["ReferenceCode"], test.ReferenceCode);
            Assert.AreEqual((Guid)unformatted["ReplicationID"], test.ReplicationID);
        }

        [Test]
        public void Lz4UnparsesJObjectToStream()
        {
            var arraySettings = BSONFormatter.GetDefaultSettings();
            arraySettings.TypeNameHandling = BESSy.Json.TypeNameHandling.Objects;

            var bson = new BSONFormatter(arraySettings);
            var zip = new LZ4ZipFormatter(bson);

            var test = TestResourceFactory.CreateRandom() as MockClassC;

            var stream = zip.FormatObjStream(test);

            var copy = new MemoryStream();
            stream.CopyTo(copy);

            var unformatted = zip.Parse(stream);

            var formatted = zip.Unparse(unformatted);

            Assert.AreEqual(new StreamReader(copy).ReadToEnd(), new StreamReader(formatted).ReadToEnd());
        }
    }
}
