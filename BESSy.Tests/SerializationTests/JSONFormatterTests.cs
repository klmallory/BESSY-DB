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
using NUnit.Framework;

namespace BESSy.Tests.SerializationTests
{
    [TestFixture]
    public class JSONFormatterTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void JSONFormatterThrowsExceptionWithNullSettings()
        {
            new JSONFormatter(null);
        }

        [Test]
        public void JSONFormatsToBytes()
        {
            var test = TestResourceFactory.CreateRandom() as MockClassC;
            var json = new JSONFormatter();

            var formatted = json.FormatObj(test);

            var unformatted = json.UnformatObj<MockClassA>(formatted) as MockClassC;

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
        public void JSONFormatsToStream()
        {
            var test = TestResourceFactory.CreateRandom() as MockClassC;
            var json = new JSONFormatter();

            var formatted = json.FormatObjStream(test);

            var unformatted = json.UnformatObj<MockClassA>(formatted) as MockClassC;

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
        public void JSONSafeFormatsToBytes()
        {
            var test = TestResourceFactory.CreateRandom() as MockClassC;
            var json = new JSONFormatter();

            byte[] formatted;

            Assert.IsTrue(json.TryFormatObj(test, out formatted));

            MockClassA raw;

            Assert.IsTrue(json.TryUnformatObj<MockClassA>(formatted, out raw));

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
            Assert.IsFalse(json.TryFormatObj(default(MockClassA), out formatted));
            Assert.IsFalse(json.TryUnformatObj((byte[])null, out raw));
            Assert.IsFalse(json.TryUnformatObj(invalid, out raw));
        }

        [Test]
        public void JSONSafeFormatsToStream()
        {
            var test = TestResourceFactory.CreateRandom() as MockClassC;
            var json = new JSONFormatter();

            Stream formatted;

            Assert.IsTrue(json.TryFormatObj(test, out formatted));

            MockClassA raw;

            Assert.IsTrue(json.TryUnformatObj<MockClassA>(formatted, out raw));

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
            Assert.IsFalse(json.TryFormatObj(default(MockClassA), out formatted));
            Assert.IsFalse(json.TryUnformatObj((Stream)null, out raw));
            Assert.IsFalse(json.TryUnformatObj(invalid, out raw));
        }

        [Test]
        public void JSONBinFormatsToBytes()
        {
            var test = TestResourceFactory.CreateRandom() as MockClassC;
            var json = new JSONFormatter();

            var bytes = json.FormatObj(test);

            var binFormatted = json.Format(bytes);
            var buffer = json.Unformat(binFormatted);

            Assert.AreEqual(binFormatted, buffer);

            var unformatted = json.UnformatObj<MockClassA>(binFormatted) as MockClassC;

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
        public void JSONBinFormatsToStream()
        {
            var test = TestResourceFactory.CreateRandom() as MockClassC;
            var json = new JSONFormatter();

            var stream = json.FormatObjStream(test);

            var binFormatted = json.Format(stream);
            var buffer = json.Unformat(binFormatted);

            Assert.AreEqual(binFormatted, buffer);

            var unformatted = json.UnformatObj<MockClassA>(binFormatted) as MockClassC;

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
            var test = TestResourceFactory.CreateRandom() as MockClassC;

            var json = new JSONFormatter();

            var stream = json.FormatObjStream(test);

            var unformatted = json.Parse(stream);

            Assert.AreEqual(unformatted.Value<int>("Id"), test.Id);
            Assert.AreEqual(unformatted.Value<string>("Name"), test.Name);
            Assert.AreEqual((double)unformatted["GetSomeCheckSum"][0], test.GetSomeCheckSum[0]);
            Assert.AreEqual((float)unformatted["Location"]["X"], test.Location.X);
            Assert.AreEqual((float)unformatted["Location"]["Y"], test.Location.Y);
            Assert.AreEqual((float)unformatted["Location"]["Z"], test.Location.Z);
            Assert.AreEqual((float)unformatted["Location"]["W"], test.Location.W);
            Assert.AreEqual((string)unformatted["ReferenceCode"], test.ReferenceCode);
            Assert.AreEqual((Guid)unformatted["ReplicationID"], test.ReplicationID);
        }
    }
}
