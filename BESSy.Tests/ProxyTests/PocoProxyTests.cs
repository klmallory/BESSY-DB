using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BESSy.Relational;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using NUnit.Framework;

namespace BESSy.Tests.ProxyTests
{
    [TestFixture]
    public class PocoProxyTests : FileTest
    {
        [Test]
        [Category("Performance")]
        public void ProxySavesData()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var domain = TestResourceFactory.CreateRandomDomain();

            using (var db = new PocoRelationalDatabase<int, MockClassA>
                (_testName + ".database", "Id", 
                new FileCore<int, long>(), new BSONFormatter(), new BinConverter32(),
                new PocoProxyFactory<int, MockClassA>("BESSy.Proxy", false)))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    domain.Id = db.Add(domain);

                    var d = db.Fetch(domain.Id);

                    Validation.ValidateDomain(d, domain as MockDomain);

                    t.Commit();
                }
            }

            using (var db = new PocoRelationalDatabase<int, MockClassA>
                (_testName + ".database", 
                new BSONFormatter(), 
                new PocoProxyFactory<int, MockClassA>("BESSy.Proxy", false)))
            {
                db.Load();

                Assert.AreEqual(9, db.Length);

                var d = db.Fetch(domain.Id);

                Validation.ValidateDomain(d, domain as MockDomain);
            }
        }

        [Test]
        [Category("Performance")]
        public void ProxyReusesTypes()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var domain = TestResourceFactory.CreateRandomDomain();
            var domain2 = TestResourceFactory.CreateRandomDomain();

            using (var db = new PocoRelationalDatabase<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>(), new BSONFormatter(), new BinConverter32(), new PocoProxyFactory<int, MockClassA>()))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    domain.Id = db.Add(domain);
                    domain2.Id = db.Add(domain2);

                    var d = db.Fetch(domain2.Id);

                    Validation.ValidateDomain(d, domain2 as MockDomain);

                    t.Commit();
                }
            }

            using (var db = new PocoRelationalDatabase<int, MockClassA>(_testName + ".database", new BSONFormatter(), new PocoProxyFactory<int, MockClassA>()))
            {
                db.Load();

                var d = db.Fetch(domain.Id);

                Assert.AreEqual(18, db.Length);

                Validation.ValidateDomain(d, domain as MockDomain);
            }
        }

        [Test]
        [Category("Performance")]
        public void ProxyOverridesIdAndStoresOldId()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var domain = TestResourceFactory.CreateRandomDomain();

            using (var db = new PocoRelationalDatabase<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>(), new BSONFormatter(), new BinConverter32(), new PocoProxyFactory<int, MockClassA>()))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    domain.Id = db.Add(domain);

                    var d = db.Fetch(domain.Id);
                    var obj = db.FetchJObj(domain.Id);

                    Validation.ValidateDomain(d, domain as MockDomain);

                    Assert.AreEqual(0, obj.Value<int>("Bessy_Proxy_OldId"));

                    t.Commit();
                }
            }

            using (var db = new PocoRelationalDatabase<int, MockClassA>(_testName + ".database", new BSONFormatter(), new PocoProxyFactory<int, MockClassA>()))
            {
                db.Load();

                Assert.AreEqual(9, db.Length);

                var d = db.Fetch(domain.Id);

                Validation.ValidateDomain(d, domain as MockDomain);
            }
        }

        [Test]
        [Category("Performance")]
        public void PocoProxyConvertsExternalDomain()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var domain = TestResourceFactory.CreateRandomDomain();

            domain = (domain as MockDomain).WithIds() as MockClassA;

            using (var db = new PocoRelationalDatabase<int, MockClassA>
                (_testName + ".database", "Id",
                new FileCore<int, long>(new Seed32(999)), new BSONFormatter(), new BinConverter32(),
                new PocoProxyFactory<int, MockClassA>("BESSy.Proxy", false)))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    domain.Id = db.Add(domain);

                    var d = db.Fetch(domain.Id);

                    Validation.ValidateA(domain, d as MockDomain);

                    t.Commit();
                }
            }

            using (var db = new PocoRelationalDatabase<int, MockClassA>
                (_testName + ".database",
                new BSONFormatter(),
                new PocoProxyFactory<int, MockClassA>("BESSy.Proxy", false)))
            {
                db.Load();

                Assert.AreEqual(9, db.Length);

                var d = db.Fetch(domain.Id);

                Validation.ValidateDomain(d, domain as MockDomain);
            }
        }

        [Test]
        [Category("Performance")]
        public void PocoProxyUpdatesExternalDomain()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var domain = TestResourceFactory.CreateRandomDomain();

            domain = (domain as MockDomain).WithIds() as MockClassA;
                    var c = (domain as MockClassC);

            using (var db = new PocoRelationalDatabase<int, MockClassA>
                (_testName + ".database", "Id",
                new FileCore<int, long>(new Seed32(999)), new BSONFormatter(), new BinConverter32(),
                new PocoProxyFactory<int, MockClassA>("BESSy.Proxy", false)))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    domain.Id = db.Add(domain);

                    c.LittleId =7;
                    var oldId = c.Id;
                    c.Id = 99999;

                    db.Update(c, oldId);

                    c.Location.X = 999;
                    db.Update(c, c.Id);

                    var d = db.Fetch(c.Id);

                    Validation.ValidateDomain(d, domain as MockDomain);

                    var prox = db.Fetch(c.Id);

                    var g = Guid.NewGuid();
                    prox.ReplicationID = g;

                    db.Update(prox, prox.Id);

                    d = db.Fetch(prox.Id);

                    domain.ReplicationID = g;
                    Validation.ValidateDomain(d, domain as MockDomain);

                    t.Commit();
                }

                using (var t = db.BeginTransaction())
                {
                    var proxy = db.Fetch(99999);

                    proxy.Name = "Proxy Update Test";

                    db.Update(proxy, proxy.Id);

                    domain.Name = "Proxy Update Test";
                    var p = db.Fetch(99999);
                    Validation.ValidateDomain(p, domain as MockDomain);

                    t.Commit();
                }
            }

            using (var db = new PocoRelationalDatabase<int, MockClassA>
                (_testName + ".database",
                new BSONFormatter(),
                new PocoProxyFactory<int, MockClassA>("BESSy.Proxy", false)))
            {
                db.Load();

                Assert.AreEqual(9, db.Length);

                var d = db.Fetch(domain.Id);

                domain.Name = "Proxy Update Test";
                Validation.ValidateDomain(d, domain as MockDomain);
            }
        }
    }
}
