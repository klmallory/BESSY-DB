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


namespace BESSy.Tests.SerializationTests
{
    [TestFixture]
    public class CryptoFormatterTests
    {
        IList<MockClassA> _testEntities;
        object[] _keys;

        [SetUp]
        public void Setup()
        {
            _testEntities = TestResourceFactory.GetMockClassAObjects(12);
            _keys = _testEntities.ToList().ToArray();
        }

        [Test]
        public void CryptoFormatsToBytes()
        {
            var test = _testEntities[0] as MockClassC;
            var bson = TestResourceFactory.CreateBsonFormatter();
            var crypto = new CryptoFormatter(TestResourceFactory.CreateCrypto(), bson, _keys);

            var encrypted = crypto.FormatObj(test);

            var unencrypted = crypto.UnformatObj<MockClassA>(encrypted) as MockClassC;

            Assert.AreEqual(unencrypted.Id, test.Id);
            Assert.AreEqual(unencrypted.Name, test.Name);
            Assert.AreEqual(unencrypted.GetSomeCheckSum[0], test.GetSomeCheckSum[0]);
            Assert.AreEqual(unencrypted.Location.X, test.Location.X);
            Assert.AreEqual(unencrypted.Location.Y, test.Location.Y);
            Assert.AreEqual(unencrypted.Location.Z, test.Location.Z);
            Assert.AreEqual(unencrypted.Location.W, test.Location.W);
            Assert.AreEqual(unencrypted.ReferenceCode, test.ReferenceCode);
            Assert.AreEqual(unencrypted.ReplicationID, test.ReplicationID);
        }

        [Test]
        public void CryptoFormatsToStream()
        {
            var test = _testEntities[0] as MockClassC;
            var bson = TestResourceFactory.CreateBsonFormatter();
            var crypto = new CryptoFormatter(TestResourceFactory.CreateCrypto(), bson, _keys);

            var encrypted = crypto.FormatObjStream(test);

            var unencrypted = crypto.UnformatObj<MockClassA>(encrypted) as MockClassC;

            Assert.AreEqual(unencrypted.Id, test.Id);
            Assert.AreEqual(unencrypted.Name, test.Name);
            Assert.AreEqual(unencrypted.GetSomeCheckSum[0], test.GetSomeCheckSum[0]);
            Assert.AreEqual(unencrypted.Location.X, test.Location.X);
            Assert.AreEqual(unencrypted.Location.Y, test.Location.Y);
            Assert.AreEqual(unencrypted.Location.Z, test.Location.Z);
            Assert.AreEqual(unencrypted.Location.W, test.Location.W);
            Assert.AreEqual(unencrypted.ReferenceCode, test.ReferenceCode);
            Assert.AreEqual(unencrypted.ReplicationID, test.ReplicationID);
        }

        [Test]
        public void CryptoSafeFormatsToBytes()
        {
            var test = _testEntities[0] as MockClassC;
            var bson = TestResourceFactory.CreateBsonFormatter();
            var crypto = new CryptoFormatter(TestResourceFactory.CreateCrypto(), bson, _keys);

            byte[] encrypted;

            Assert.IsTrue(crypto.TryFormatObj(test, out encrypted));

            MockClassA raw;

            Assert.IsTrue(crypto.TryUnformatObj<MockClassA>(encrypted, out raw));

            var unencrypted = raw as MockClassC;

            Assert.AreEqual(unencrypted.Id, test.Id);
            Assert.AreEqual(unencrypted.Name, test.Name);
            Assert.AreEqual(unencrypted.GetSomeCheckSum[0], test.GetSomeCheckSum[0]);
            Assert.AreEqual(unencrypted.Location.X, test.Location.X);
            Assert.AreEqual(unencrypted.Location.Y, test.Location.Y);
            Assert.AreEqual(unencrypted.Location.Z, test.Location.Z);
            Assert.AreEqual(unencrypted.Location.W, test.Location.W);
            Assert.AreEqual(unencrypted.ReferenceCode, test.ReferenceCode);
            Assert.AreEqual(unencrypted.ReplicationID, test.ReplicationID);

            //Check false conditions.
            Assert.IsFalse(crypto.TryFormatObj(default(MockClassA), out encrypted));
            Assert.IsFalse(crypto.TryUnformatObj(new byte[0], out raw));
        }

        [Test]
        public void CryptoSafeFormatsToStream()
        {
            var test = _testEntities[0] as MockClassC;
            var bson = TestResourceFactory.CreateBsonFormatter();
            var crypto = new CryptoFormatter(TestResourceFactory.CreateCrypto(), bson, _keys);

            Stream encrypted;

            Assert.IsTrue(crypto.TryFormatObj(test, out encrypted));

            MockClassA raw;

            Assert.IsTrue(crypto.TryUnformatObj<MockClassA>(encrypted, out raw));

            var unencrypted = raw as MockClassC;

            Assert.AreEqual(unencrypted.Id, test.Id);
            Assert.AreEqual(unencrypted.Name, test.Name);
            Assert.AreEqual(unencrypted.GetSomeCheckSum[0], test.GetSomeCheckSum[0]);
            Assert.AreEqual(unencrypted.Location.X, test.Location.X);
            Assert.AreEqual(unencrypted.Location.Y, test.Location.Y);
            Assert.AreEqual(unencrypted.Location.Z, test.Location.Z);
            Assert.AreEqual(unencrypted.Location.W, test.Location.W);
            Assert.AreEqual(unencrypted.ReferenceCode, test.ReferenceCode);
            Assert.AreEqual(unencrypted.ReplicationID, test.ReplicationID);

            Assert.IsFalse(crypto.TryFormatObj(default(MockClassA), out encrypted));
            Assert.IsFalse(crypto.TryUnformatObj(new MemoryStream(), out raw));
        }

        [Test]
        public void CryptoBinFormatsToBytes()
        {
            var test = _testEntities[0] as MockClassC;
            var bson = TestResourceFactory.CreateBsonFormatter();
            var crypto = new CryptoFormatter(TestResourceFactory.CreateCrypto(), bson, _keys);

            var bytes = bson.FormatObj(test);

            var encrypted = crypto.Format(bytes);
            var buffer = crypto.Unformat(encrypted);

            var unencrypted = bson.UnformatObj<MockClassA>(buffer) as MockClassC;

            Assert.AreEqual(unencrypted.Id, test.Id);
            Assert.AreEqual(unencrypted.Name, test.Name);
            Assert.AreEqual(unencrypted.GetSomeCheckSum[0], test.GetSomeCheckSum[0]);
            Assert.AreEqual(unencrypted.Location.X, test.Location.X);
            Assert.AreEqual(unencrypted.Location.Y, test.Location.Y);
            Assert.AreEqual(unencrypted.Location.Z, test.Location.Z);
            Assert.AreEqual(unencrypted.Location.W, test.Location.W);
            Assert.AreEqual(unencrypted.ReferenceCode, test.ReferenceCode);
            Assert.AreEqual(unencrypted.ReplicationID, test.ReplicationID);
        }

        [Test]
        public void CryptoBinFormatsToStream()
        {
            var test = _testEntities[0] as MockClassC;
            var bson = TestResourceFactory.CreateBsonFormatter();
            var crypto = new CryptoFormatter(TestResourceFactory.CreateCrypto(), bson, _keys);

            var stream = bson.FormatObjStream(test);

            var encrypted = crypto.Format(stream);
            var buffer = crypto.Unformat(encrypted);

            var unencrypted = bson.UnformatObj<MockClassA>(buffer) as MockClassC;

            Assert.AreEqual(unencrypted.Id, test.Id);
            Assert.AreEqual(unencrypted.Name, test.Name);
            Assert.AreEqual(unencrypted.GetSomeCheckSum[0], test.GetSomeCheckSum[0]);
            Assert.AreEqual(unencrypted.Location.X, test.Location.X);
            Assert.AreEqual(unencrypted.Location.Y, test.Location.Y);
            Assert.AreEqual(unencrypted.Location.Z, test.Location.Z);
            Assert.AreEqual(unencrypted.Location.W, test.Location.W);
            Assert.AreEqual(unencrypted.ReferenceCode, test.ReferenceCode);
            Assert.AreEqual(unencrypted.ReplicationID, test.ReplicationID);
        }
    }
}
