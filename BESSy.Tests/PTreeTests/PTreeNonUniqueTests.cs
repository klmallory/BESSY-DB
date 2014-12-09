using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using BESSy.Seeding;
using BESSy.Cache;
using BESSy.Tests.Mocks;
using BESSy.Serialization.Converters;
using System.Reflection;

namespace BESSy.Tests.PTreeTests
{
    [TestFixture]
    public class PTreeNonUniqueTests : FileTest
    {
        [Test]
        public void PTreePushesIndividualEntities()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(50).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());

            using (var tree = new PTree<int, MockClassA, long>("Id", _testName + ".index"))
            {
                tree.Load();

                foreach (var o in objs)
                    tree.AddOrUpdate(new Tuple<MockClassA, long>(o, seedSegment.Increment()));

                Assert.AreEqual(50, tree.Length);

                Assert.AreEqual(5, tree.GetFirstByIndex(5));
                Assert.AreEqual(5, tree.GetFirstBySegment(5));
                Assert.AreEqual(1, tree.GetByIndex(5).Count());
            }
        }

        [Test]
        public void PTreePushesEntities()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(50).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());
            var toAdd = objs.Select(o => new Tuple<MockClassA, long>(o, seedSegment.Increment())).ToList();

            using (var tree = new PTree<int, MockClassA, long>("Id", _testName + ".index"))
            {
                tree.Load();

                var ids = tree.AddOrUpdateRange(toAdd);
                tree.AddOrUpdate(toAdd.First());

                Assert.AreEqual(51, tree.Length);

                Assert.AreEqual(5L, tree.GetFirstByIndex(5));
                Assert.AreEqual(5, tree.GetFirstBySegment(5));
                Assert.AreEqual(2, tree.GetByIndex(toAdd.First().Item1.Id).Count());
                Assert.AreEqual(5, tree.GetFirstBySegment(5));
            }
        }

        [Test]
        public void PTreePushesLotsOfEntities()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(5000).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());
            var toAdd = objs.Select(o => new Tuple<MockClassA, long>(o, seedSegment.Increment())).ToList();

            using (var tree = new PTree<int, MockClassA, long>("Id", _testName + ".index"))
            {
                tree.Load();

                var ids = tree.AddOrUpdateRange(toAdd);

                Assert.AreEqual(5000, tree.Length);

                Assert.AreEqual(5, tree.GetFirstByIndex(5));
                Assert.AreEqual(5, tree.GetFirstBySegment(5));
                Assert.AreEqual(1, tree.GetByIndex(5).Count());

                tree.AddOrUpdate(new Tuple<MockClassA, long>(TestResourceFactory.CreateRandom().WithId(seedIndex.Increment()), seedSegment.Increment()));

                Assert.AreEqual(5001, tree.Length);
            }
        }

        [Test]
        public void PTreeFetchesByIndexRange()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(5000).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());
            var toAdd = objs.Select(o => new Tuple<MockClassA, long>(o, seedSegment.Increment())).ToList();

            using (var tree = new PTree<int, MockClassA, long>("Id", _testName + ".index"))
            {
                tree.Load();

                var ids = tree.AddOrUpdateRange(toAdd);

                Assert.AreEqual(5000, tree.Length);

                Assert.AreEqual(5, tree.GetFirstByIndex(5));
                Assert.AreEqual(5, tree.GetFirstBySegment(5));
                Assert.AreEqual(1, tree.GetByIndex(5).Count());

                tree.AddOrUpdate(new Tuple<MockClassA, long>(TestResourceFactory.CreateRandom().WithId(seedIndex.Increment()), seedSegment.Increment()));

                Assert.AreEqual(5001, tree.Length);

                long[] loc;
                var range = tree.GetByIndexRangeInclusive(51, 250, out loc);

                Assert.AreEqual(200, range.Length);
            }

            using (var tree = new PTree<int, MockClassA, long>("Id", _testName + ".index"))
            {
                tree.Load();

                Assert.AreEqual(5001, tree.Length);
            }
        }

        [Test]
        public void PTreePushesLotsOfDuplicateEntities()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(5000).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());
            var toAdd = objs.Select(o => new Tuple<MockClassA, long>(o, seedSegment.Increment())).ToList();

            using (var tree = new PTree<int, MockClassA, long>("Id", _testName + ".index"))
            {
                tree.Load();

                var ids = tree.AddOrUpdateRange(toAdd);

                Assert.AreEqual(5000, tree.Length);

                Assert.AreEqual(5, tree.GetFirstByIndex(5));
                Assert.AreEqual(5, tree.GetFirstBySegment(5));
                Assert.AreEqual(1, tree.GetByIndex(5).Count());

                tree.AddOrUpdate(new Tuple<MockClassA, long>(TestResourceFactory.CreateRandom().WithId(seedIndex.Increment()), seedSegment.Increment()));

                Assert.AreEqual(5001, tree.Length);

                tree.AddOrUpdateRange(toAdd.Skip(100).Take(100).ToList());

                Assert.AreEqual(5101, tree.Length);

                Assert.AreEqual(2, tree.GetBySegment(toAdd[101].Item2).Count());
            }
        }

        [Test]
        public void PTreeStreamsData()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(5000).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());
            var toAdd = objs.Select(o => new Tuple<MockClassA, long>(o, seedSegment.Increment())).ToList();

            using (var tree = new PTree<int, MockClassA, long>("Id", _testName + ".index"))
            {
                tree.Load();

                var ids = tree.AddOrUpdateRange(toAdd);

                Assert.AreEqual(5000, tree.Length);

                Assert.AreEqual(5, tree.GetFirstByIndex(5));
                Assert.AreEqual(5, tree.GetFirstBySegment(5));
                Assert.AreEqual(1, tree.GetByIndex(5).Count());

                foreach (var p in tree.AsStreaming())
                {
                    if (p == null)
                        continue;

                    p.Dispose();
                }
            }
        }

        [Test]
        public void PTreeReverseEnumerates()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(5000).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());
            var toAdd = objs.Select(o => new Tuple<MockClassA, long>(o, seedSegment.Increment())).ToList();

            using (var tree = new PTree<int, MockClassA, long>("Id", _testName + ".index"))
            {
                tree.Load();

                var ids = tree.AddOrUpdateRange(toAdd);

                Assert.AreEqual(5000, tree.Length);

                Assert.AreEqual(5, tree.GetFirstByIndex(5));
                Assert.AreEqual(5, tree.GetFirstBySegment(5));
                Assert.AreEqual(1, tree.GetByIndex(5).Count());

                foreach (var p in tree.AsReverseEnumerable())
                {
                    if (p == null)
                        continue;
                }
            }
        }

        [Test]
        public void PTreeReturnsEmptyStreamOnWrongPage()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(5000).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());
            var toAdd = objs.Select(o => new Tuple<MockClassA, long>(o, seedSegment.Increment())).ToList();

            using (var tree = new PTree<int, MockClassA, long>("Id", _testName + ".index"))
            {
                tree.Load();

                var ids = tree.AddOrUpdateRange(toAdd);

                Assert.AreEqual(5000, tree.Length);

                Assert.AreEqual(5, tree.GetFirstByIndex(5));
                Assert.AreEqual(5, tree.GetFirstBySegment(5));
                Assert.AreEqual(1, tree.GetByIndex(5).Count());

                using (var p = tree.GetPageStream(8))
                {
                    Assert.AreEqual(0, p.Length);
                }
            }
        }
    }
}
