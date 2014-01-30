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
using BESSy.Json.Linq;
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

            using (var index = new IndexFileManager<int, MockClassA>
                (_testName + ".index"
                , "Id"
                , 4096, 2048, 4096
                , new BSONFormatter()
                , new RowSynchronizer<int>(new BinConverter32())
                , new BinConverter32()))
            {
                index.Load<int>();

                index.UpdateFromTransaction(new List<TransactionIndexResult<int>>()
                {
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0),
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0),
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0)
                });

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

            using (var index = new IndexFileManager<int, MockClassA>(_testName + ".index", "Id", new BinConverter32()))
            {
                index.Load<int>();

                index.UpdateFromTransaction(new List<TransactionIndexResult<int>>()
                {
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0),
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0),
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0)
                });

                var pair = index.LoadSegmentFrom(2);

                Assert.AreEqual(1001, pair.Id);
                Assert.AreEqual(2, pair.Property);
            }

            using (var index = new IndexFileManager<int, MockClassA>
                (_testName + ".index"))
            {
                index.Load<int>();

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

            using (var index = new IndexFileManager<int, MockClassA>
                (_testName + ".index"
                , "Id"
                , 4096, 2048, 4096 
                , new BSONFormatter()
                , new RowSynchronizer<int>(new BinConverter32())
                , new BinConverter32()))
            {
                index.Load<int>();

                index.UpdateFromTransaction(new List<TransactionIndexResult<int>>()
                {
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0),
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0),
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0)
                });
            }

            using (var index = new IndexFileManager<int, MockClassA>
                (_testName + ".index"
                , "Id"
                , 4096, 2048, 4096
                , new BSONFormatter()
                , new RowSynchronizer<int>(new BinConverter32())
                , new BinConverter32()))
            {
                index.Load<int>();

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

            using (var index = new IndexFileManager<int, MockClassA>
                (_testName + ".index"
                , "Id"
                , 4096, 2048, 4096
                , new BSONFormatter()
                , new RowSynchronizer<int>(new BinConverter32())
                , new BinConverter32()))
            {
                index.Load<int>();

                index.UpdateFromTransaction(new List<TransactionIndexResult<int>>()
                {
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0),
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0),
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0)
                });
            }

            using (var index = new IndexFileManager<int, MockClassA>
                (_testName + ".index"
                , 4096, 2048, 4096
                , new BSONFormatter()
                , new RowSynchronizer<int>(new BinConverter32())))
            {
                index.Load<int>();

                index.UpdateFromTransaction(new List<TransactionIndexResult<int>>()
                {
                    new TransactionIndexResult<int>(1002, Action.Update, 3, 3)
                });

                var pair = index.LoadSegmentFrom(3);

                Assert.AreEqual(1002, pair.Id);
                Assert.AreEqual(3, pair.Property);
            }

            using (var index = new IndexFileManager<int, MockClassA>
                (_testName + ".index"
                , "Id"
                , 4096, 2048, 4096
                , new BSONFormatter()
                , new RowSynchronizer<int>(new BinConverter32())
                , new BinConverter32()))
            {
                index.Load<int>();

                index.UpdateFromTransaction(new List<TransactionIndexResult<int>>()
                {
                    new TransactionIndexResult<int>(1002, Action.Delete, 3, 3)
                });

                var pair = index.LoadSegmentFrom(3);

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

            using (var index = new IndexFileManager<int, MockClassA>
                (_testName + ".index"
                , "Id"
                , 4096, 2048, 4096
                , new BSONFormatter()
                , new RowSynchronizer<int>(new BinConverter32())
                , new BinConverter32()))
            {
                index.Load<int>();

                index.UpdateFromTransaction(new List<TransactionIndexResult<int>>()
                {
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0),
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0),
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0),
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0),
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0),
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0),
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0),
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0),
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0),
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0)
                });
            }

            using (var index = new IndexFileManager<int, MockClassA>
                (_testName + ".index"
                , "Id"
                , 4096, 2048, 4096
                , new BSONFormatter()
                , new RowSynchronizer<int>(new BinConverter32())
                , new BinConverter32()))
            {
                index.Load<int>();

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

            using (var index = new IndexFileManager<int, MockClassA>
                (_testName + ".index"
                , "Id"
                , 4096, 2048, 4096
                , new BSONFormatter()
                , new RowSynchronizer<int>(new BinConverter32())
                , new BinConverter32()))
            {
                index.Load<int>();

                index.UpdateFromTransaction(new List<TransactionIndexResult<int>>()
                {
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0)
                });

                for (var i = 0; i < 5000; i++)
                    index.SegmentSeed.Open(i);
            }

            using (var index = new IndexFileManager<int, MockClassA>
                (_testName + ".index"
                , 4096, 2048, 4096
                , new BSONFormatter()
                , new RowSynchronizer<int>(new BinConverter32())))
            {
                index.Load<int>();

                Assert.Greater(index.SegmentSeed.MinimumSeedStride, 42000);
            }
        }

        [Test]
        public void DeleteDoesntFailWithEroniousSegment()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var seed = new Seed32(999) { IdConverter = new BinConverter32(), IdProperty = "Id", MinimumSeedStride = 512 };

            using (var index = new IndexFileManager<int, MockClassA>(_testName + ".index", "Id", new BinConverter32()))
            {
                index.Load<int>();

                index.UpdateFromTransaction(new List<TransactionIndexResult<int>>()
                {
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0),
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0),
                    new TransactionIndexResult<int>(seed.Increment(), Action.Create, index.SegmentSeed.Increment(), 0)
                });
            }

            using (var index = new IndexFileManager<int, MockClassA>
                (_testName + ".index"))
            {
                index.Load<int>();

                index.UpdateFromTransaction(new List<TransactionIndexResult<int>>()
                {
                    new TransactionIndexResult<int>(1001, Action.Delete, 1, 0)
                });
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
                using (var index = new IndexFileManager<int, MockClassA>(_testName + ".index", "Id", new BinConverter32()))
                {
                    index.Load<int>();

                    index.Rebuilt += new Rebuild<IndexPropertyPair<int, int>>(delegate(Guid transactionId, int newStride, int newLength, int newSeedStride)
                        {
                            index.SaveSeed<int>();
                        });

                    index.SaveFailed += new SaveFailed<IndexPropertyPair<int, int>>(delegate(SaveFailureInfo<IndexPropertyPair<int, int>> saveFailInfo)
                        {
                            index.Rebuild(saveFailInfo.NewRowSize, saveFailInfo.NewDatabaseSize, -1);
                        });

                    for (var i = 0; i < 4096; i++)
                    {
                        var id = seed.Increment();
                        var entity = TestResourceFactory.CreateRandom().WithId(id);
                        transaction.Enlist(Action.Create, id, entity);
                        results.Add(new TransactionIndexResult<int>(id, Action.Create, index.SegmentSeed.Increment(), 0));
                    }

                    index.UpdateFromTransaction(results);

                    index.SaveSeed<int>();
                }

                using (var index = new IndexFileManager<int, MockClassA>(_testName + ".index"))
                {
                    index.Load<int>();

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

                using (var index = new IndexFileManager<int, MockClassA>(_testName + ".index", "Id", new BinConverter32()))
                {
                    index.Load<int>();

                    index.Rebuilt += new Rebuild<IndexPropertyPair<int, int>>(delegate(Guid transactionId, int newStride, int newLength, int newSeedStride)
                    {
                        index.SaveSeed<int>();
                    });

                    index.SaveFailed += new SaveFailed<IndexPropertyPair<int, int>>(delegate(SaveFailureInfo<IndexPropertyPair<int, int>> saveFailInfo)
                    {
                        index.Rebuild(saveFailInfo.NewRowSize, saveFailInfo.NewDatabaseSize, -1);
                    });

                    for (var i = 0; i < 4096; i++)
                    {
                        var id = seed.Increment();
                        var entity = TestResourceFactory.CreateRandom().WithId(id);
                        transaction.Enlist(Action.Create, id, entity);
                        results.Add(new TransactionIndexResult<int>(id, Action.Create, index.SegmentSeed.Increment(), -1));
                    }

                    index.UpdateFromTransaction(results);

                    index.Reorganize(new BinConverter32(), o => o.Value<int>("Id"));
                }

                using (var index = new IndexFileManager<int, MockClassA>(_testName + ".index"))
                {
                    index.Load<int>();

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
