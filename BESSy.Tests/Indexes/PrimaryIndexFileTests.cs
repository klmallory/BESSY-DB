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
using BESSy.Indexes;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using BESSy.Tests.Mocks;
using BESSy.Transactions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;


namespace BESSy.Tests.Indexes
{
    [TestFixture]
    public class PrimaryIndexFileTests : FileTest
    {
        [Test]
        public void PrimaryIndexFileCreate()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var seed = new Seed32(999) { IdConverter = new BinConverter32(), IdProperty = "Id" };

            using (var index = new PrimaryIndexFileManager<int, MockClassA>
                (_testName + ".index"
                , 4096, 2048, 4096
                , new BSONFormatter()
                , seed
                , new RowSynchronizer<int>(new BinConverter32())))
            {
                index.Load();

                index.SaveSegment(new IndexPropertyPair<int, int>(1000, 1), 1);
                index.SaveSegment(new IndexPropertyPair<int, int>(1001, 2), 2);
                index.SaveSegment(new IndexPropertyPair<int, int>(1002, 3), 3);

                var pair = index.LoadSegmentFrom(2);

                Assert.AreEqual(1001, pair.Id);
                Assert.AreEqual(2, pair.Property);
            }
        }

        [Test]
        public void PrimaryIndexFileCreateWithDefaults()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var seed = new Seed32(999) { IdConverter = new BinConverter32(), IdProperty = "Id" };

            using (var index = new PrimaryIndexFileManager<int, MockClassA>(_testName + ".index", seed))
            {
                index.Load();

                index.SaveSegment(new IndexPropertyPair<int, int>(1000, 1));
                index.SaveSegment(new IndexPropertyPair<int, int>(1001, 2));
                index.SaveSegment(new IndexPropertyPair<int, int>(1002, 3));

                var pair = index.LoadSegmentFrom(2);

                Assert.AreEqual(1001, pair.Id);
                Assert.AreEqual(2, pair.Property);
            }

            using (var index = new PrimaryIndexFileManager<int, MockClassA>
                (_testName + ".index"))
            {
                index.Load();

                var pair = index.LoadSegmentFrom(1);

                Assert.AreEqual(1000, pair.Id);
                Assert.AreEqual(1, pair.Property);

                pair = index.LoadSegmentFrom(2);

                Assert.AreEqual(1001, pair.Id);
                Assert.AreEqual(2, pair.Property);

                pair = index.LoadSegmentFrom(3);

                Assert.AreEqual(1002, pair.Id);
                Assert.AreEqual(3, pair.Property);
            }
        }

        [Test]
        public void PrimaryIndexFileLoadsFromExisting()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var seed = new Seed32(999) { IdConverter = new BinConverter32(), IdProperty = "Id" };

            using (var index = new PrimaryIndexFileManager<int, MockClassA>
                (_testName + ".index"
                , 4096, 2048, 4096
                , new BSONFormatter()
                , seed
                , new RowSynchronizer<int>(new BinConverter32())))
            {
                index.Load();

                index.SaveSegment(new IndexPropertyPair<int, int>(1000, 1));
                index.SaveSegment(new IndexPropertyPair<int, int>(1001, 2));
                index.SaveSegment(new IndexPropertyPair<int, int>(1002, 3));
            }

            using (var index = new PrimaryIndexFileManager<int, MockClassA>
                (_testName + ".index"
                , 4096, 2048, 4096
                , new BSONFormatter()
                , new RowSynchronizer<int>(new BinConverter32())))
            {
                index.Load();

                var pair = index.LoadSegmentFrom(1);

                Assert.AreEqual(1000, pair.Id);
                Assert.AreEqual(1, pair.Property);

                pair = index.LoadSegmentFrom(2);

                Assert.AreEqual(1001, pair.Id);
                Assert.AreEqual(2, pair.Property);

                pair = index.LoadSegmentFrom(3);

                Assert.AreEqual(1002, pair.Id);
                Assert.AreEqual(3, pair.Property);
            }
        }

        [Test]
        public void DeletesAndUpdatesExisting()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var seed = new Seed32(999) { IdConverter = new BinConverter32(), IdProperty = "Id" };

            using (var index = new PrimaryIndexFileManager<int, MockClassA>
                (_testName + ".index"
                , 4096, 2048, 4096
                , new BSONFormatter()
                , seed
                , new RowSynchronizer<int>(new BinConverter32())))
            {
                index.Load();

                index.SaveSegment(new IndexPropertyPair<int, int>(1000, 1));
                index.SaveSegment(new IndexPropertyPair<int, int>(1001, 2));
                index.SaveSegment(new IndexPropertyPair<int, int>(1002, 3));
            }

            using (var index = new PrimaryIndexFileManager<int, MockClassA>
                (_testName + ".index"
                , 4096, 2048, 4096
                , new BSONFormatter()
                , new RowSynchronizer<int>(new BinConverter32())))
            {
                index.Load();

                index.SaveSegment(new IndexPropertyPair<int, int>(1009, 2), 2);

                var pair = index.LoadSegmentFrom(2);

                Assert.AreEqual(1009, pair.Id);
                Assert.AreEqual(2, pair.Property);
            }

            using (var index = new PrimaryIndexFileManager<int, MockClassA>
                (_testName + ".index"
                , 4096, 2048, 4096
                , new BSONFormatter()
                , new RowSynchronizer<int>(new BinConverter32())))
            {
                index.Load();

                index.DeleteSegment(2);

                var pair = index.LoadSegmentFrom(2);

                Assert.AreEqual(0, pair.Id);
                Assert.AreEqual(0, pair.Property);
            }
        }

        [Test]
        public void LookupIndex()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var seed = new Seed32(999) { IdConverter = new BinConverter32(), IdProperty = "Id" };

            using (var index = new PrimaryIndexFileManager<int, MockClassA>
                (_testName + ".index"
                , 4096, 2048, 4096
                , new BSONFormatter()
                , seed
                , new RowSynchronizer<int>(new BinConverter32())))
            {
                index.Load();

                index.SaveSegment(new IndexPropertyPair<int, int>(seed.Increment(), 1));
                index.SaveSegment(new IndexPropertyPair<int, int>(seed.Increment(), 2));
                index.SaveSegment(new IndexPropertyPair<int, int>(seed.Increment(), 3));
                index.SaveSegment(new IndexPropertyPair<int, int>(seed.Increment(), 4));
                index.SaveSegment(new IndexPropertyPair<int, int>(seed.Increment(), 5));
                index.SaveSegment(new IndexPropertyPair<int, int>(seed.Increment(), 6));
                index.SaveSegment(new IndexPropertyPair<int, int>(seed.Increment(), 7));
                index.SaveSegment(new IndexPropertyPair<int, int>(seed.Increment(), 8));
                index.SaveSegment(new IndexPropertyPair<int, int>(seed.Increment(), 9));
                index.SaveSegment(new IndexPropertyPair<int, int>(seed.Increment(), 10));
            }

            using (var index = new PrimaryIndexFileManager<int, MockClassA>
                (_testName + ".index"
                , 4096, 2048, 4096
                , new BSONFormatter()
                , new RowSynchronizer<int>(new BinConverter32())))
            {
                index.Load();

                foreach (var page in index.AsEnumerable())
                {
                    var pair = page.FirstOrDefault(query => query.Value<int>("Id") == 1009);

                    Assert.IsFalse(object.Equals(pair, default(JObject)));
                    Assert.AreEqual(1009, pair.Value<int>("Id"));
                    Assert.AreEqual(10, pair.Value<int>("Property"));

                    break;
                }
            }
        }

        [Test]
        public void SeedRebuildsOnSave()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var seed = new Seed32(999) { IdConverter = new BinConverter32(), IdProperty = "Id" };
            seed.MinimumSeedStride = 512;

            using (var index = new PrimaryIndexFileManager<int, MockClassA>
                (_testName + ".index"
                , 4096, 2048, 4096
                , new BSONFormatter()
                , seed
                , new RowSynchronizer<int>(new BinConverter32())))
            {
                index.Load();

                index.SaveSegment(new IndexPropertyPair<int, int>(1000, 1), 1);

                for (var i = 0; i < 5000; i++)
                    index.Seed.Open(i);
            }

            using (var index = new PrimaryIndexFileManager<int, MockClassA>
                (_testName + ".index"
                , 4096, 2048, 4096
                , new BSONFormatter()
                , new RowSynchronizer<int>(new BinConverter32())))
            {
                index.Load();

                Assert.Greater(index.Seed.MinimumSeedStride, 42000);
            }
        }

        [Test]
        public void DeleteDoesntFailWithEroniousSegment()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var seed = new Seed32(999) { IdConverter = new BinConverter32(), IdProperty = "Id", MinimumSeedStride = 512 };

            using (var index = new PrimaryIndexFileManager<int, MockClassA>(_testName + ".index", seed))
            {
                index.Load();

                index.SaveSegment(new IndexPropertyPair<int, int>(seed.Increment(), 1));
                index.SaveSegment(new IndexPropertyPair<int, int>(seed.Increment(), 2));
                index.SaveSegment(new IndexPropertyPair<int, int>(seed.Increment(), 3));
            }

            using (var index = new PrimaryIndexFileManager<int, MockClassA>
                (_testName + ".index"))
            {
                index.Load();

                index.DeleteSegment(43);
            }
        }

        [Test]
        public void IndexSyncsAfterTransaction()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var seed = new Seed32(999) { IdConverter = new BinConverter32(), IdProperty = "Id", MinimumSeedStride = 512 };
            using (var tm = new TransactionManager<int, MockClassA>())
            {
                var transaction = new MockTransaction<int, MockClassA>(tm);
                var results = new List<TransactionIndexResult<int>>();
                using (var index = new PrimaryIndexFileManager<int, MockClassA>(_testName + ".index", seed))
                {
                    index.Load();

                    for (var i = 0; i < 4096; i++)
                    {
                        var id = seed.Increment();
                        var entity = TestResourceFactory.CreateRandom().WithId(id);
                        transaction.Enlist(Action.Create, id, entity);
                        results.Add(new TransactionIndexResult<int>(id, Action.Create, id - 999, id - 999));
                    }

                    index.UpdateFromTransaction(results, transaction);
                }

                using (var index = new PrimaryIndexFileManager<int, MockClassA>(_testName + ".index"))
                {
                    index.Load();

                    foreach (var e in transaction.GetEnlistedItems())
                    {
                        var pair = index.LoadSegmentFrom(e.Id - 999);

                        Assert.AreEqual(e.Id, pair.Id);
                    }
                }
            }
        }


        [Test]
        public void IndexReorganizesOnFlush()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var random = new Random();
            var seed = new Seed32(999) { IdConverter = new BinConverter32(), IdProperty = "Id", MinimumSeedStride = 512 };

            using (var tm = new TransactionManager<int, MockClassA>())
            {
                var transaction = new MockTransaction<int, MockClassA>(tm);
                var results = new List<TransactionIndexResult<int>>();
                using (var index = new PrimaryIndexFileManager<int, MockClassA>(_testName + ".index", seed))
                {
                    index.Load();

                    for (var i = 0; i < 4096; i++)
                    {
                        var id = random.Next();
                        var entity = TestResourceFactory.CreateRandom().WithId(id);
                        transaction.Enlist(Action.Create, id, entity);
                        results.Add(new TransactionIndexResult<int>(id, Action.Create, i + 1, i + 1));
                    }

                    index.UpdateFromTransaction(results, transaction);

                    index.Reorganize(new BinConverter32(), o => o.Value<int>("Id"));
                }

                using (var index = new PrimaryIndexFileManager<int, MockClassA>(_testName + ".index"))
                {
                    index.Load();

                    var seg = 0;
                    foreach (var e in transaction.GetEnlistedItems().OrderBy(e => e.Id))
                    {
                        seg++;

                        var pair = index.LoadSegmentFrom(seg);

                        Assert.AreEqual(e.Id, pair.Id);
                    }
                }
            }
        }
    }
}
