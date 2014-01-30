using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using BESSy.Crypto;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using NUnit.Framework;
using BESSy.Tests.ResourceRepositoryTests.Resources;

namespace BESSy.Tests.CryptoTests
{
    [TestFixture]
    public class CryptoDatabaseTests : FileTest
    {
        SecureString _key = new SecureString();
        SecureString _vec = new SecureString();

        [Test]
        public void CryptoSavesLargeFiles()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            for (var i = 0; i <= 25; i++)
            {
                _key.AppendChar((char)(((i + 25763) * i * Math.PI) % char.MaxValue));
                _vec.AppendChar((char)(((i + 41359) * i * Math.PI) % char.MaxValue));
            }

            var objects = new List<ResourceContainer>()
            {
                new Mocks.MockImageContainer(testRes.Luna_DIFF) { Name = "Luna_DIFF"},
                new Mocks.MockImageContainer(testRes.Luna_MAT) { Name = "Luna_MAT"},
                new Mocks.MockImageContainer(testRes.Luna_NRM) { Name = "Luna_NRM"}
            };

            using (var db = new Database<string, ResourceContainer>
                (_testName + ".database", "Name", new SeedString(255),
                new BinConverterString(),
                new QueryCryptoFormatter(new RC2Crypto(_vec), new BSONFormatter(), _key)))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    foreach (var o in objects)
                        o.Name = db.Add(o);

                    t.Commit();
                }
            }

            using (var db = new Database<string, ResourceContainer>
                (_testName + ".database", "Name", new SeedString(255),
                new BinConverterString(),
                new QueryCryptoFormatter(new RC2Crypto(_vec), new BSONFormatter(), _key)))
            {
                db.Load();

                var diff = db.Fetch("Luna_DIFF");

                Assert.IsNotNull(diff);

                var list = db.Select(s => true);

                Assert.AreEqual(3, list.Count);
            }
        }

        [Test]
        public void CryptoCommitsLargeTransactions()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            for (var i = 0; i <= 25; i++)
            {
                _key.AppendChar((char)(((i + 25763) * i * Math.PI) % char.MaxValue));
                _vec.AppendChar((char)(((i + 41359) * i * Math.PI) % char.MaxValue));
            }

            var objects = new List<ResourceContainer>()
            {
                new Mocks.MockImageContainer(testRes.Luna_DIFF) { Name = "Luna_DIFF"},
                new Mocks.MockImageContainer(testRes.Luna_MAT) { Name = "Luna_MAT"},
                new Mocks.MockImageContainer(testRes.Luna_NRM) { Name = "Luna_NRM"}
            };

            using (var db = new Database<string, ResourceContainer>
                (_testName + ".database", "Name", new SeedString(255),
                new BinConverterString(),
                new QueryCryptoFormatter(new RC2Crypto(_vec), new BSONFormatter(), _key)))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    foreach (var o in objects)
                        o.Name = db.Add(o);

                    t.Commit();
                }

                using (var t = db.BeginTransaction())
                {
                    foreach (var o in objects)
                        db.Update(o, o.Name);

                    t.Commit();
                }
            }
        }

        [Test]
        public void CryptoSerializesQueries()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var key = new SecureString();
            var vec = new SecureString();

            for (var i = 0; i <= 25; i++)
            {
                key.AppendChar((char)(((i + 25763) * i * Math.PI) % char.MaxValue));
                vec.AppendChar((char)(((i + 41359) * i * Math.PI) % char.MaxValue));
            }

            var objects = new List<ResourceContainer>()
            {
                new Mocks.MockImageContainer(testRes.Luna_DIFF) { Name = "Luna_DIFF"},
                new Mocks.MockImageContainer(testRes.Luna_MAT) { Name = "Luna_MAT"},
                new Mocks.MockImageContainer(testRes.Luna_NRM) { Name = "Luna_NRM"}
            };

            using (var db = new Database<string, ResourceContainer>
                (_testName + ".database", "Name", new SeedString(255),
                new BinConverterString(),
                new QueryCryptoFormatter(new RC2Crypto(vec), new BSONFormatter(), key)))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    foreach (var o in objects)
                        o.Name = db.Add(o);

                    t.Commit();
                }

                using (var t = db.BeginTransaction())
                {
                    foreach (var o in objects)
                        db.Update(o, o.Name);

                    t.Commit();
                }
            }

            using (var db = new Database<string, ResourceContainer>
                (_testName + ".database", new QueryCryptoFormatter(new RC2Crypto(vec), new BSONFormatter(), key)))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    db.Delete("Luna_MAT");
                    t.Commit();
                }

                Assert.IsNull(db.Fetch("Luna_MAT"));

                var o = db.Select(s => true);

                Assert.AreEqual(2, o.Count);
            }
        }
    }
}
