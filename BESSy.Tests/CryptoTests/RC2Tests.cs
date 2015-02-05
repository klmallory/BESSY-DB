using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using BESSy.Crypto;
using BESSy.Files;
using BESSy.Json;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using NUnit.Framework;

namespace BESSy.Tests.CryptoTests
{
    [TestFixture]
    public class RC2Tests
    {
        ICrypto _crypto;
        IQueryableFormatter _bsonFormatter;
        byte[] _key = new byte[8];

        [TestFixtureSetUp()]
        public void FixtureSetup()
        {
            _crypto = TestResourceFactory.CreateCrypto();
            _bsonFormatter = TestResourceFactory.CreateBsonFormatter();

            new Random().NextBytes(_key);
        }

        [SetUp]
        public void Setup()
        {

        }

        [Test]
        public void RC2EncryptsDecryptsString()
        {
            var text = " My Test String With Lot'aqn of wierd ch@r@cters..~`;']{}[-=+_#%^&     ";

            var encrypted = _crypto.Encrypt(text, _key);

            var result = _crypto.Decrypt(encrypted, _key);

            Assert.AreEqual(text, result);
        }

        [Test]
        public void RC2EncryptsDecryptsByteArray()
        {
            var obj = TestResourceFactory.CreateRandom();

            var bson = _bsonFormatter.FormatObj(obj);

            var encrypted = _crypto.Encrypt(bson, _key);

            var decrypted = _crypto.Decrypt(encrypted, _key);

            var result = _bsonFormatter.UnformatObj<MockClassA>(decrypted);

            Assert.AreEqual(obj.Id, result.Id);
            Assert.AreEqual(obj.Name, result.Name);
        }

        [Test]
        public void RC2EncryptsDecryptsStream()
        {
            var objs = TestResourceFactory.GetMockClassAObjects(50);

            var bson = _bsonFormatter.FormatObj(objs);

            using (var input = new MemoryStream(bson))
            {
                var encrypted = _crypto.Encrypt(input, _key);

                var decrypted = _crypto.Decrypt(encrypted, _key);

                var results = _bsonFormatter.UnformatObj<IList<MockClassA>>(decrypted);

                Assert.AreEqual(objs.Count, results.Count);

                for (var i = 0; i < objs.Count; i++)
                {
                    var obj = objs[i];
                    var result = results[i];

                    Assert.AreEqual(obj.Id, result.Id);
                    Assert.AreEqual(obj.Name, result.Name);
                }
            }
        }

        [Test]
        public void RC2EncryptsDecryptsBuffer()
        {
            var objs = TestResourceFactory.GetMockClassAObjects(50);

            var bson = _bsonFormatter.FormatObj(objs);

            var input = new MemoryStream(bson).ToArray();
            
            var encrypted = _crypto.Encrypt(input, _key);

            var decrypted = _crypto.Decrypt(encrypted, _key);

            var results = _bsonFormatter.UnformatObj<IList<MockClassA>>(decrypted);

            Assert.AreEqual(objs.Count, results.Count);

            for (var i = 0; i < objs.Count; i++)
            {
                var obj = objs[i];
                var result = results[i];

                Assert.AreEqual(obj.Id, result.Id);
                Assert.AreEqual(obj.Name, result.Name);
            }
        }
    }
}
