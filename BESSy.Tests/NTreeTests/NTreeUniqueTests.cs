using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using BESSy.Tests.Mocks;
using BESSy.Cache;
using BESSy.Seeding;
using BESSy.Serialization.Converters;

namespace BESSy.Tests.NTreeTests
{
    [TestFixture]
    public class NTreeUniqueTests : FileTest
    {
        [Test]
        public void NTreePushesIndividualEntities()
        {
            var objs = TestResourceFactory.GetMockClassAObjects(50).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());

            using (var tree = new NTree<int, MockClassA, long>("Id", true))
            {
                foreach (var o in objs)
                    tree.AddOrUpdate(new Tuple<MockClassA, long>(o, seedSegment.Increment()));

                Assert.AreEqual(50, tree.Length);

                Assert.AreEqual(5, tree.GetFirstByIndex(5));
                Assert.AreEqual(5, tree.GetFirstBySegment(5));
                Assert.AreEqual(1, tree.GetByIndex(5).Count());

                tree[1] = new NTreeItem<int, long>(1, 8);
                Assert.AreEqual(1, tree[1].Index);
                Assert.AreEqual(8, tree[1].Segment);
            }
        }

        [Test]
        public void NTreePushesEntities()
        {
            var objs = TestResourceFactory.GetMockClassAObjects(50).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());
            var toAdd = objs.Select(o => new Tuple<MockClassA, long>(o, seedSegment.Increment())).ToList();

            using (var tree = new NTree<int, MockClassA, long>("Id", true))
            {
                var ids = tree.AddOrUpdateRange(toAdd);
                tree.AddOrUpdate(toAdd.First());

                Assert.AreEqual(50, tree.Length);

                Assert.AreEqual(5L, tree.GetFirstByIndex(5));
                Assert.AreEqual(5, tree.GetFirstBySegment(5));
                Assert.AreEqual(1, tree.GetByIndex(1).Count());
                Assert.AreEqual(5, tree.GetFirstBySegment(5));
            }
        }

        [Test]
        public void NTreeReturns0ForNullEntity()
        {
            var objs = TestResourceFactory.GetMockClassAObjects(50).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());
            var toAdd = objs.Select(o => new Tuple<MockClassA, long>(o, seedSegment.Increment())).ToList();

            using (var tree = new NTree<int, MockClassA, long>("Id", true))
            {
                var ids = tree.AddOrUpdateRange(toAdd);
                tree.AddOrUpdate(toAdd.First());

                Assert.AreEqual(50, tree.Length);

                Assert.AreEqual(0, tree.AddOrUpdate(null));
            }
        }

        [Test]
        public void NTreeReturnsEmptyPageWithWrongPageId()
        {
            var objs = TestResourceFactory.GetMockClassAObjects(50).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());
            var toAdd = objs.Select(o => new Tuple<MockClassA, long>(o, seedSegment.Increment())).ToList();

            using (var tree = new NTree<int, MockClassA, long>("Id", true))
            {
                var ids = tree.AddOrUpdateRange(toAdd);
                tree.AddOrUpdate(toAdd.First());

                Assert.AreEqual(50, tree.Length);

                var p = tree.GetPage(8);

                Assert.AreEqual(0, p.Length);
            }
        }

        [Test]
        public void NTreePushesLotsOfEntities()
        {
            var objs = TestResourceFactory.GetMockClassAObjects(5000).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());
            var toAdd = objs.Select(o => new Tuple<MockClassA, long>(o, seedSegment.Increment())).ToList();

            using (var tree = new NTree<int, MockClassA, long>("Id", true))
            {
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
        public void NTreePushesLotsOfDuplicateEntities()
        {
            var objs = TestResourceFactory.GetMockClassAObjects(20480).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());
            var toAdd = objs.Select(o => new Tuple<MockClassA, long>(o, seedSegment.Increment())).ToList();

            using (var tree = new NTree<int, MockClassA, long>("Id", true))
            {
                var ids = tree.AddOrUpdateRange(toAdd);

                Assert.AreEqual(20480, tree.Length);

                Assert.AreEqual(5, tree.GetFirstByIndex(5));
                Assert.AreEqual(5, tree.GetFirstBySegment(5));
                Assert.AreEqual(1, tree.GetByIndex(5).Count());

                tree.AddOrUpdate(new Tuple<MockClassA, long>(TestResourceFactory.CreateRandom().WithId(seedIndex.Increment()), seedSegment.Increment()));

                Assert.AreEqual(20481, tree.Length);

                tree.AddOrUpdateRange(toAdd.Skip(100).Take(100).ToList());

                Assert.AreEqual(20481, tree.Length);
            }
        }

        [Test]
        public void NTreeChecksAllWithLargeCount()
        {
            using (var tree = new NTree<int, MockClassA, long>("Id", true))
            {
                var objs = new List<MockClassA>();
                var seedIndex = new Seed32();
                var seedSegment = new Seed64();

                for (var i = 0; i < 5; i++)
                {
                    var additions = TestResourceFactory.GetMockClassAObjects(100000).ToList();

                    additions.ForEach(o => o.Id = seedIndex.Increment());

                    tree.AddOrUpdateRange(additions.Select(o => new Tuple<MockClassA, long>(o, seedSegment.Increment())).ToList());
                }

                Assert.AreEqual(500000, tree.Length);

                Assert.AreEqual(5, tree.GetFirstByIndex(5));
                Assert.AreEqual(5, tree.GetFirstBySegment(5));
                Assert.AreEqual(1, tree.GetByIndex(5).Count());

                tree.AddOrUpdate(new Tuple<MockClassA, long>(TestResourceFactory.CreateRandom().WithId(seedIndex.Increment()), seedSegment.Increment()));

                Assert.AreEqual(500001, tree.Length);

                tree.AddOrUpdateRange(
                    tree.AsEnumerable().First().Take(100)
                    .Select(s => new NTreeItem<int, long>(s.Item2.Index, s.Item2.Segment))
                    .ToList());

                Assert.AreEqual(500001, tree.Length);
            }
        }

    }
}
