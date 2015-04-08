using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using BESSy.Tests.Mocks;
using System.Reflection;
using BESSy.Seeding;
using BESSy.Files;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using BESSy.Tests.AtomicFileManagerTests;
using BESSy.Reflection;
using BESSy.Indexes;

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

        [Test]
        public void CascadeIndexClosesWithCorrectLength()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var formatter = TestResourceFactory.CreateJsonFormatterWithoutArrayFormatting();
            var core = new FileCore<string, long>() { IdSeed = new SeedString(2048), SegmentSeed = new Seed64(), Stride = 512 };

            using (var db = new Relational.RelationalDatabase<int, MockClassD>(_testName + ".database", "Id"))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    var objs = TestResourceFactory.GetMockClassDObjects(300, db).ToList();

                    objs.ToList().ForEach(o => o.Id = db.Add(o));

                    t.Commit();
                }

                var dbx = DynamicMemberManager.GetManager(db);
                var pt = DynamicMemberManager.GetManager(dbx._cascadeIndex);

                Assert.AreEqual(2, pt.Length);
            }

            using (var db = new Relational.RelationalDatabase<int, MockClassD>(_testName + ".database", "Id"))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    var additions = TestResourceFactory.GetMockClassDObjects(200, db).ToList();

                    additions.ToList().ForEach(o => o.Id = db.Add(o));

                    t.Commit();
                }

                var dbx = DynamicMemberManager.GetManager(db);
                var pt = DynamicMemberManager.GetManager(dbx._cascadeIndex);

                Assert.AreEqual(3, pt.Length);
            }
        }
    }
}
