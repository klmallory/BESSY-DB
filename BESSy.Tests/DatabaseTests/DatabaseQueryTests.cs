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
using System.Reflection;
using System.Text;
using System.Threading;
using BESSy.Extensions;
using BESSy.Files;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using BESSy.Transactions;
using NUnit.Framework;
using BESSy.Json.Linq;

namespace BESSy.Tests.DatabaseTests
{
    [TestFixture]
    public class DatabaseQueryTests : FileTest
    {
        ISeed<int> _seed;
        IQueryableFormatter _formatter;

        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void SelectLastQuerySelectsLast10Matches()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var core = new FileCore<int, long>(new Seed32(999));
            var formatter = TestResourceFactory.CreateJsonFormatterWithoutArrayFormatting();

            var stopWatch = new Stopwatch();

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", core))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    TestResourceFactory.GetMockClassAObjects(25000).ToList().ForEach(a => db.Add(a));

                    t.Commit();
                }

                stopWatch.Reset();
                stopWatch.Start();
                var gets = db.SelectLast(o => o.Value<int>("Id") > 24000, 10);
                stopWatch.Stop();

                Console.WriteLine("query with 10 last records retreived in {0} seconds", stopWatch.ElapsedMilliseconds / 1000m);

                Assert.AreEqual(10, gets.Count());

                foreach (var item in gets)
                    Assert.Greater(item.Id, 24989);
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database"))
            {
                var len = db.Load();

                stopWatch.Reset();
                stopWatch.Start();
                var gets = db.SelectLast(o => o.Value<int>("Id") > 24000, 10);
                stopWatch.Stop();

                Console.WriteLine("query with 10 last records retreived in {0} seconds", stopWatch.ElapsedMilliseconds / 1000m);

                Assert.AreEqual(10, gets.Count());

                foreach (var item in gets)
                    Assert.Greater(item.Id, 24000);
            }
        }

        [Test]
        public void SelectFirstQuerySelectsFirst10Matches()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var core = new FileCore<int, long>(new Seed32(999));
            var formatter = TestResourceFactory.CreateJsonFormatterWithoutArrayFormatting();

            var stopWatch = new Stopwatch();

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", core))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    TestResourceFactory.GetMockClassAObjects(25000).ToList().ForEach(a => db.Add(a));

                    t.Commit();
                }

                stopWatch.Reset();
                stopWatch.Start();
                var gets = db.SelectFirst(o => o.Value<int>("Id") > 24000, 10);
                stopWatch.Stop();

                Console.WriteLine("query with 10 first records retreived in {0} seconds", stopWatch.ElapsedMilliseconds / 1000m);

                Assert.AreEqual(10, gets.Count());

                foreach (var item in gets)
                    Assert.Greater(item.Id, 23999);

                db.FlushAll();
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database"))
            {
                var len = db.Load();

                stopWatch.Reset();
                stopWatch.Start();
                var gets = db.SelectFirst(o => o.Value<int>("Id") > 24000, 10);
                stopWatch.Stop();

                Console.WriteLine("query with 10 first records retreived in {0} seconds", stopWatch.ElapsedMilliseconds / 1000m);

                Assert.AreEqual(10, gets.Count());

                foreach (var item in gets)
                    Assert.Greater(item.Id, 23999);
            }
        }

        [Test]
        public void DatabaseFetchesUpdatesAndDeletesWithQueries()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var ids = new List<int>();

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>(), new BinConverter32(), new JSONFormatter()))
            {
                db.Load();

                objs.ToList().ForEach(o => ids.Add(db.Add(o)));

                db.FlushAll();
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database", new JSONFormatter()))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    var last = db.SelectLast(s => true, 1).LastOrDefault();

                    Assert.IsNotNull(last);

                    db.Update(s => s.Value<string>("Name") == last.Name
                        , new System.Action<MockClassA>(a => a.Name = "last"));

                    db.Delete(s => true);

                    t.Commit();
                }

                var selected = db.Select(s => true);

                Assert.AreEqual(0, selected.Count);
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database", new JSONFormatter()))
            {
                db.Load();

                var selected = db.Select(s => true);

                Assert.AreEqual(0, selected.Count);
            }
        }

        [Test]
        public void DatabaseFetchesUpdatesAndDeletesWithActiveTransactions()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var ids = new List<int>();

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>(), new BinConverter32(), new JSONFormatter()))
            {
                db.Load();

                var t = db.BeginTransaction();

                objs.ToList().ForEach(o => ids.Add(db.Add(o)));

                var last = db.SelectLast(s => true, 1).LastOrDefault();

                Assert.IsNotNull(last);

                var count = db.Update(s => s.Value<string>("Name") == last.Name
                    , new System.Action<MockClassA>(a => a.Name = "last"));

                Assert.AreEqual(1, count);

                var selected = db.Select(s => true);

                Assert.AreEqual(100, selected.Count);

                count = db.Delete(s => true);

                Assert.AreEqual(100, count);

                selected = db.Select(s => true);

                Assert.AreEqual(0, selected.Count);

                t.Rollback();

            }
        }

        [Test]
        public void DatabaseDeletesFirstLastWithActiveTransactions()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var ids = new List<int>();

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>(), new BinConverter32(), new JSONFormatter()))
            {
                db.Load();

                var t = db.BeginTransaction();

                objs.ToList().ForEach(o => ids.Add(db.Add(o)));

                var last = db.SelectLast(s => true, 1).LastOrDefault();

                Assert.IsNotNull(last);

                var count = db.Update(s => s.Value<string>("Name") == last.Name
                    , new System.Action<MockClassA>(a => a.Name = "last"));

                Assert.AreEqual(1, count);

                var selected = db.Select(s => true);

                Assert.AreEqual(100, selected.Count);

                count = db.DeleteFirst(s => true, 10);

                Assert.AreEqual(10, count);

                selected = db.Select(s => true);

                Assert.AreEqual(90, selected.Count);

                count = db.DeleteLast(s => true, 10);

                Assert.AreEqual(10, count);

                selected = db.Select(s => true);

                Assert.AreEqual(80, selected.Count);

                t.Rollback();

            }
        }

        [Test]
        public void DatabaseSelectsScalarWithActiveTransactions()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var ids = new List<int>();

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>(), new BinConverter32(), new JSONFormatter()))
            {
                db.Load();

                var t = db.BeginTransaction();

                objs.ToList().ForEach(o => ids.Add(db.Add(o)));

                var last = db.SelectLast(s => true, 1).LastOrDefault();

                Assert.IsNotNull(last);

                var count = db.Update(s => s.Value<string>("Name") == last.Name
                    , new System.Action<MockClassA>(a => a.Name = "last"));

                Assert.AreEqual(1, count);

                var selected = db.Select(s => true);

                Assert.AreEqual(100, selected.Count);

                var names = db.SelectScalar( s => s.Value<int>("Id") <= 15, "Name");

                Assert.AreEqual(15, names.Count);

                names = db.SelectScalarFirst(s => s.Value<int>("Id") <= 15, 10, "Name");

                Assert.AreEqual(10, names.Count);

                names = db.SelectScalarLast(s => s.Value<int>("Id") <= 15, 8, "Name");

                Assert.AreEqual(8, names.Count);
                
                t.Rollback();

            }
        }
    }
}
