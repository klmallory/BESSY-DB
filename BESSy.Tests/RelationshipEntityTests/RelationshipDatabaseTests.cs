using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using BESSy.Tests.Mocks;

namespace BESSy.Tests.RelationshipEntityTests
{
    [TestFixture]
    public class RelationshipDatabaseTests : FileTest
    {
        [Test]
        public void RSelectTest()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            using (var db = new Relational.RelationalDatabase<int, MockClassD>(_testName + ".database", "Id"))
            {
                db.Load();

                var objs = TestResourceFactory.GetMockClassDObjects(3, db).ToList();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(o));

                    t.Commit();
                }
            }

            using (var db = new Relational.RelationalDatabase<int, MockClassD>(_testName + ".database", "Id"))
            {
                db.Load();

                var all = db.Select(s => s.Value<int>("Id") > 0);

                Assert.AreEqual(15, all.Count);
            }
        }
    }
}
