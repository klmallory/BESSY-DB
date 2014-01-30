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
using BESSy.Factories;
using BESSy.Files;
using BESSy.Indexes;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using BESSy.Tests.Mocks;
using BESSy.Transactions;
using BESSy.Json.Linq;
using NUnit.Framework;

namespace BESSy.Tests.Indexes
{
    [TestFixture]
    public class IndexTests : FileTest
    {
        ISeed<Int32> _seed = new Seed32(999);
        ISeed<Int32> _segmentSeed = new Seed32(0);
        IQueryableFormatter _formatter = new BSONFormatter();

        [Test]
        public void IndexLoads()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            IDictionary<int, int> segments = new Dictionary<int, int>();

            using (var db = new AtomicFileManager<MockClassA>(_testName + ".database", _seed, _segmentSeed, _formatter))
            {
                db.Load<int>();

                var seed = new Seed32();
                var objs = TestResourceFactory.GetMockClassAObjects(2500).ToList();
                objs.ForEach(a => a.WithId(seed.Increment()));

                using (var index = new PrimaryIndex<int, MockClassA>
                    (_testName + ".test.index"
                    , "Id"
                    , new BinConverter32()
                    , new RepositoryCacheFactory()
                    , new BSONFormatter()
                    , new IndexFileFactory()
                    , new RowSynchronizer<int>(new BinConverter32())))
                {
                    index.Load();

                    index.Register(db);

                    using (var t = new MockTransaction<int, MockClassA>())
                    {
                        objs.ForEach(a => t.Enlist(Action.Create, a.Id, a));

                        segments = db.CommitTransaction(t, objs.Select(o => o.Id + 999).ToDictionary<int, int>(i => i - 999));
                    }
                }
            }
        }

        [Test]
        public void IndexUpdatesSegment()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            using (var db = new AtomicFileManager<MockClassA>(_testName + ".database", _seed, _segmentSeed, _formatter))
            {
                db.Load<int>();

                var seed = new Seed32();
                var objs = TestResourceFactory.GetMockClassAObjects(2500).ToList();
                IDictionary<int, int> segments = new Dictionary<int, int>();

                objs.ForEach(a => a.WithId(seed.Increment()));

                using (var index = new PrimaryIndex<int, MockClassA>
                    (_testName + ".test.index"
                    , "Id"
                    , new BinConverter32()
                    , new RepositoryCacheFactory()
                    , new BSONFormatter()
                    , new IndexFileFactory()
                    , new RowSynchronizer<int>(new BinConverter32())))
                {
                    index.Load();

                    index.Register(db);

                    using (var t = new MockTransaction<int, MockClassA>())
                    {
                        objs.ForEach(a => t.Enlist(Action.Create, a.Id, a));

                        segments = db.CommitTransaction(t, objs.Select(o => o.Id + 999).ToDictionary<int, int>(i => i - 999));
                    }

                    using (var t = new MockTransaction<int, MockClassA>())
                    {
                        var update = objs.Where(o => o.Id == segments.Last().Key).FirstOrDefault();
                        update.Name = "Hello Kitty";

                        t.Enlist(Action.Update, segments.Last().Key, update);

                        db.CommitTransaction(t, new Dictionary<int, int>() { { segments.Last().Key, segments.Last().Value } });
                    }

                    while (db.FileFlushQueueActive)
                        Thread.Sleep(100);

                    Assert.AreEqual(segments.Last().Value, index.Fetch(segments.Last().Key));
                }
            }
        }
    

        [Test]
        public void IndexDeletesSegment()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            using (var db = new AtomicFileManager<MockClassA>(_testName + ".database", _seed, _segmentSeed, _formatter))
            {
                db.Load<int>();

                var seed = new Seed32();
                var objs = TestResourceFactory.GetMockClassAObjects(2500).ToList();
                IDictionary<int, int> segments = new Dictionary<int, int>();

                objs.ForEach(a => a.WithId(seed.Increment()));

                using (var index = new PrimaryIndex<int, MockClassA>
                    (_testName + ".test.index"
                    , "Id"
                    , new BinConverter32()
                    , new RepositoryCacheFactory()
                    , new BSONFormatter()
                    , new IndexFileFactory()
                    , new RowSynchronizer<int>(new BinConverter32())))
                {
                    index.Load();

                    index.Register(db);

                    using (var t = new MockTransaction<int, MockClassA>())
                    {
                        objs.ForEach(a => t.Enlist(Action.Create, a.Id, a));

                        segments = db.CommitTransaction(t, objs.Select(o => o.Id + 999).ToDictionary<int, int>(i => i - 999));
                    }

                    var deleteId = segments.Last().Key;
                    var deleteSegment = segments.Last().Value;

                    using (var t = new MockTransaction<int, MockClassA>())
                    {
                        t.Enlist(Action.Delete, deleteId, null);

                        db.CommitTransaction(t, new Dictionary<int, int>() { { deleteId, deleteSegment } });
                    }

                    Assert.AreEqual(0, index.Fetch(deleteId));
                }
            }
        }

        [Test]
        public void IndexReorganizes()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var sw = new Stopwatch();

            using (var db = new AtomicFileManager<MockClassA>(_testName + ".database", _seed, _segmentSeed, _formatter))
            {
                db.Load<int>();

                var seed = new Seed32();
                var objs = TestResourceFactory.GetMockClassAObjects(2500).ToList();
                objs.ForEach(a => a.WithId(seed.Increment()));

                using (var index = new PrimaryIndex<int, MockClassA>
                    (_testName + ".test.index"
                    , "Id"
                    , new BinConverter32()
                    , new RepositoryCacheFactory()
                    , new BSONFormatter()
                    , new IndexFileFactory()
                    , new RowSynchronizer<int>(new BinConverter32())))
                {
                    index.Load();

                    var t = new MockTransaction<int, MockClassA>();
                    objs.ForEach(a => t.Enlist(Action.Create, a.Id, a));

                    index.Register(db);

                    db.CommitTransaction(t, objs.Select(o => o.Id + 999).ToDictionary<int, int>(i => i - 999));

                    sw.Reset();
                    sw.Start();

                    index.Flush();

                    sw.Stop();

                    Console.WriteLine("PrimaryIndex reorganization took {0} seconds for {1} records.", sw.ElapsedMilliseconds / 1000.00, objs.Count); 
                }
            }
        }

        [Test]
        public void DatabaseDoesNotDuplicateOnAddOrUpdate()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(25);

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new Seed32()))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    foreach (var o in objs)
                        o.Id = db.Add(o);

                    t.Commit();
                }
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new Seed32()))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    var update = db.Fetch(3);

                    update.Name = "updated";

                    db.AddOrUpdate(update, 3);

                    t.Commit();
                }
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new Seed32()))
            {
                db.Load();

                var check = db.Fetch(3);

                Assert.AreEqual("updated", check.Name);

                var allChecks = db.Select(s => s.Value<string>("Name") == "updated");

                Assert.IsNotNull(allChecks);
                Assert.AreEqual(1, allChecks.Count());

            }
        }
    }
}
