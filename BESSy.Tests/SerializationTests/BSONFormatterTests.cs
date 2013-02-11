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
using BESSy.Extensions;
using BESSy.Files;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Newtonsoft.Json.Bson;

namespace BESSy.Tests.SerializationTests
{
    [TestFixture]
    public class BSONFormatterTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void BSONFormatterThrowsExceptionWithNullSettings()
        {
            new BSONFormatter(null);
        }

        [Test]
        public void BSONFormatsToBytes()
        {
            var test = TestResourceFactory.CreateRandom() as MockClassC;
            var bson = new BSONFormatter();

            var formatted = bson.FormatObj(test);

            var unformatted = bson.UnformatObj<MockClassA>(formatted) as MockClassC;

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
        public void BSONFormatsToStream()
        {
            var test = TestResourceFactory.CreateRandom() as MockClassC;
            var bson = new BSONFormatter();

            var formatted = bson.FormatObjStream(test);

            var unformatted = bson.UnformatObj<MockClassA>(formatted) as MockClassC;

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
        public void BSONSafeFormatsToBytes()
        {
            var test = TestResourceFactory.CreateRandom() as MockClassC;
            var bson = new BSONFormatter();

            byte[] formatted;

            Assert.IsTrue(bson.TryFormatObj(test, out formatted));

            MockClassA raw;

            Assert.IsTrue(bson.TryUnformatObj<MockClassA>(formatted, out raw));

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
            Assert.IsFalse(bson.TryFormatObj(default(MockClassA), out formatted));
            Assert.IsFalse(bson.TryUnformatObj((byte[])null, out raw));
            Assert.IsFalse(bson.TryUnformatObj(invalid, out raw));
        }

        [Test]
        public void BSONSafeFormatsToStream()
        {
            var test = TestResourceFactory.CreateRandom() as MockClassC;
            var bson = new BSONFormatter();

            Stream formatted;

            Assert.IsTrue(bson.TryFormatObj(test, out formatted));

            MockClassA raw;

            Assert.IsTrue(bson.TryUnformatObj<MockClassA>(formatted, out raw));

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

            var invalid = new MemoryStream(Encoding.UTF8.GetBytes("{ this is invalid."));

            //Check false conditions.
            Assert.IsFalse(bson.TryFormatObj(default(MockClassA), out formatted));
            Assert.IsFalse(bson.TryUnformatObj((Stream)null, out raw));
            Assert.IsFalse(bson.TryUnformatObj(invalid, out raw));
        }

        [Test]
        public void BSONBinFormatsToBytes()
        {
            var test = TestResourceFactory.CreateRandom() as MockClassC;
            var bson = new BSONFormatter();

            var bytes = bson.FormatObj(test);

            var binFormatted = bson.Format(bytes);
            var buffer = bson.Unformat(binFormatted);

            Assert.AreEqual(binFormatted, buffer);

            var unformatted = bson.UnformatObj<MockClassA>(binFormatted) as MockClassC;

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
        public void BSONBinFormatsToStream()
        {
            var test = TestResourceFactory.CreateRandom() as MockClassC;
            var bson = new BSONFormatter();

            var stream = bson.FormatObjStream(test);

            var binFormatted = bson.Format(stream);
            var buffer = bson.Unformat(binFormatted);

            Assert.AreEqual(binFormatted, buffer);

            var unformatted = bson.UnformatObj<MockClassA>(binFormatted) as MockClassC;

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
        public void BSONParsesJObjectFromStream()
        {
            var arraySettings = BSONFormatter.GetDefaultSettings();
            arraySettings.TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Objects;

            var test = TestResourceFactory.CreateRandom() as MockClassC;

            var bson = new BSONFormatter(arraySettings);

            var stream = bson.FormatObjStream(test);

            var unformatted = bson.Parse(stream);

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
    }
}
