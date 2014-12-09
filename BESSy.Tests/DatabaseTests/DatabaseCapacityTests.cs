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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BESSy.Extensions;
using BESSy.Files;
using BESSy.Indexes;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using BESSy.Tests.Mocks;
using BESSy.Tests.ResourceRepositoryTests.Resources;
using BESSy.Transactions;
using BESSy.Json.Linq;
using NUnit.Framework;
using BESSy.Containers;

namespace BESSy.Tests.DatabaseTests
{
    [TestFixture]
    public class DatabaseCapacityTests : FileTest
    {
        ISeed<int> _seed;
        IQueryableFormatter _formatter;

        [SetUp]
        public void Setup()
        {
            _seed = new Seed32(999);
            _formatter = TestResourceFactory.CreateJsonFormatter();
        }

        [Test]
        public void DatabaseSavesTwentyThousandRecordsAndReorganizes()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(10000).ToList();

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>(new Seed32(999)), _formatter))
            {
                db.Load();

                objs.ToList().ForEach(o => db.Add(o));
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database", _formatter))
            {
                var len = db.Load();

                Assert.AreEqual(objs.Count(), len);

                var items = db.Select(s => s.Value<int>("Id") < 5000).Cast<MockClassC>();

                Assert.AreEqual(4000, items.Count());

                db.Reorganize();

                Assert.AreEqual(11000, db.Add(TestResourceFactory.CreateRandom()));
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database", _formatter))
            {
                db.Load();

                var items = db.Select(s => s.Value<int>("Id") < 10000);

                foreach (var obj in items)
                {
                    var item = obj as MockClassC;
                    var orig = db.Fetch(obj.Id) as MockClassC;

                    Assert.AreEqual(item.Id, orig.Id);
                    Assert.AreEqual(item.Name, orig.Name);
                    Assert.AreEqual(item.GetSomeCheckSum[0], orig.GetSomeCheckSum[0]);
                    Assert.AreEqual(item.Location.X, orig.Location.X);
                    Assert.AreEqual(item.Location.Y, orig.Location.Y);
                    Assert.AreEqual(item.Location.Z, orig.Location.Z);
                    Assert.AreEqual(item.Location.W, orig.Location.W);
                    Assert.AreEqual(item.ReferenceCode, orig.ReferenceCode);
                    Assert.AreEqual(item.ReplicationID, orig.ReplicationID);
                }
            }
        }

        [Test]
        public void DatabaseSavesTenThousandRecords()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(10000).ToList();

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>(new Seed32(999))))
            {
                db.Load();

                objs.ToList().ForEach(o => db.Add(o));
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database"))
            {
                var len = db.Load();

                Assert.AreEqual(objs.Count(), len);
            }
        }

        [Test]
        public void DatabaseSavesLargeFile()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            using (var db = new Database<string, ResourceContainer>(_testName + ".database", "Name"))
            {
                db.Load();

                using (var tran = db.BeginTransaction())
                {
                    db.Add(new MockImageContainer(testRes.Luna_DIFF) { Name = "Luna_DIFF" });
                    db.Add(new MockImageContainer(testRes.Luna_MAT) { Name = "Luna_MAT" });
                    db.Add(new MockImageContainer(testRes.Luna_NRM) { Name = "Luna_NRM" });

                    tran.Commit();
                }
            }

            using (var db = new Database<string, ResourceContainer>(_testName + ".database"))
            {
                var len = db.Load();

                Assert.AreEqual(3, len);

                Assert.AreEqual(db.Fetch("Luna_DIFF").GetResource<Bitmap>().Width, testRes.Luna_DIFF.Width);
                Assert.AreEqual(db.Fetch("Luna_MAT").GetResource<Bitmap>().Width, testRes.Luna_MAT.Width);
                Assert.AreEqual(db.Fetch("Luna_NRM").GetResource<Bitmap>().Width, testRes.Luna_NRM.Width);
            }
        }

        [Test]
        public void DatabaseRebuildsWithLargeFile()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            using (var db = new Database<string, ResourceContainer>(_testName + ".database", "Name"))
            {
                db.Load();

                using (var tran = db.BeginTransaction())
                {
                    db.Add(new MockImageContainer(testRes.Luna_DIFF) { Name = "Luna_DIFF" });
                    db.Add(new MockImageContainer(testRes.Luna_DIFF) { Name = "Luna_DIFF1" });
                    db.Add(new MockImageContainer(testRes.Luna_DIFF) { Name = "Luna_DIFF2" });
                    db.Add(new MockImageContainer(testRes.Luna_DIFF) { Name = "Luna_DIFF3" });
                    tran.Commit();
                }

                using (var tran = db.BeginTransaction())
                {
                    db.Add(new MockImageContainer(testRes.Luna_MAT) { Name = "Luna_MAT" });
                    db.Add(new MockImageContainer(testRes.Luna_MAT) { Name = "Luna_MAT1" });
                    db.Add(new MockImageContainer(testRes.Luna_MAT) { Name = "Luna_MAT2" });
                    db.Add(new MockImageContainer(testRes.Luna_MAT) { Name = "Luna_MAT3" });

                    tran.Commit();
                }

                using (var tran = db.BeginTransaction())
                {
                    db.Add(new MockImageContainer(testRes.Luna_NRM) { Name = "Luna_NRM" });
                    db.Add(new MockImageContainer(testRes.Luna_NRM) { Name = "Luna_NRM1" });
                    db.Add(new MockImageContainer(testRes.Luna_NRM) { Name = "Luna_NRM2" });
                    db.Add(new MockImageContainer(testRes.Luna_NRM) { Name = "Luna_NRM3" });

                    tran.Commit();
                }
            }

            using (var db = new Database<string, ResourceContainer>(_testName + ".database"))
            {
                var len = db.Load();

                Assert.AreEqual(12, len);

                Assert.AreEqual(db.Fetch("Luna_DIFF").GetResource<Bitmap>().Width, testRes.Luna_DIFF.Width);
                Assert.AreEqual(db.Fetch("Luna_MAT").GetResource<Bitmap>().Width, testRes.Luna_MAT.Width);
                Assert.AreEqual(db.Fetch("Luna_NRM").GetResource<Bitmap>().Width, testRes.Luna_NRM.Width);
            }
        }

        [Test]
        public void DatabaseSavesOneHundredThousandRecords()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            decimal avgTime = 0;
            var stopWatch = new Stopwatch();

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                db.Load();

                stopWatch.Start();
                using (var t = db.BeginTransaction())
                {
                    TestResourceFactory.GetMockClassAObjects(25000).ToList().ForEach(a => db.Add(a));

                    t.Commit();
                }
                stopWatch.Stop();
                avgTime = (avgTime + stopWatch.ElapsedMilliseconds);

                Console.WriteLine("Transaction with 25000 entities committed in {0} seconds", stopWatch.ElapsedMilliseconds / 1000m);

                stopWatch.Reset();
                stopWatch.Start();
                using (var t = db.BeginTransaction())
                {
                    TestResourceFactory.GetMockClassAObjects(25000).ToList().ForEach(a => db.Add(a));

                    t.Commit();
                }
                stopWatch.Stop();
                avgTime = (avgTime + stopWatch.ElapsedMilliseconds) / 2;

                Console.WriteLine("Transaction with 25000 entities committed in {0} seconds", stopWatch.ElapsedMilliseconds / 1000m);

                stopWatch.Reset();
                stopWatch.Start();
                using (var t = db.BeginTransaction())
                {
                    TestResourceFactory.GetMockClassAObjects(25000).ToList().ForEach(a => db.Add(a));

                    t.Commit();
                }
                stopWatch.Stop();
                avgTime = (avgTime + stopWatch.ElapsedMilliseconds) / 2;

                Console.WriteLine("Transaction with 25000 entities committed in {0} seconds", stopWatch.ElapsedMilliseconds / 1000m);

                stopWatch.Reset();
                stopWatch.Start();
                using (var t = db.BeginTransaction())
                {
                    TestResourceFactory.GetMockClassAObjects(25000).ToList().ForEach(a => db.Add(a));

                    t.Commit();
                }
                stopWatch.Stop();
                avgTime = (avgTime + stopWatch.ElapsedMilliseconds) / 2;

                Console.WriteLine("Transaction with 25000 entities committed in {0} seconds", stopWatch.ElapsedMilliseconds / 1000m);

                Console.WriteLine("Avg Commit time for trans with 25000 entities {0} seconds", avgTime / 1000m);

                stopWatch.Reset();
                stopWatch.Start();
                Assert.AreEqual(20000, db.Select(o => o.Value<int>("Id") > 80000).Count());
                stopWatch.Stop();

                Console.WriteLine("query with 20000 records retreived in {0} seconds", stopWatch.ElapsedMilliseconds / 1000m);

                db.Flush();
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database"))
            {
                var len = db.Load();

                Assert.AreEqual(100000, len);

                stopWatch.Reset();
                stopWatch.Start();
                Assert.AreEqual(20000, db.Select(o => o.Value<int>("Id") > 80000).Count());
                stopWatch.Stop();

                Console.WriteLine("query with 20000 records retreived in {0} seconds", stopWatch.ElapsedMilliseconds / 1000m);

                db.Clear();
            }
        }
    }
}

