using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using BESSy.Seeding;
using BESSy.Cache;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;

namespace BESSy.Tests.NTreeTests
{
    [TestFixture]
    public class NTreeDeleteTests
    {
        [Test]
        public void NTreeDeletesByIndexIndividualUnique()
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

                Assert.AreEqual(1, tree.Delete(5).Length);

                Assert.AreEqual(50, tree.Length);

                Assert.AreEqual(0, tree.GetFirstByIndex(5));
            }
        }

        [Test]
        public void NTreeDeletesByIndexIndividualNonUnique()
        {
            var objs = TestResourceFactory.GetMockClassAObjects(50).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());

            using (var tree = new NTree<int, MockClassA, long>("Id", false))
            {
                foreach (var o in objs)
                    tree.AddOrUpdate(new Tuple<MockClassA, long>(o, seedSegment.Increment()));

                Assert.AreEqual(50, tree.Length);

                Assert.AreEqual(5, tree.GetFirstByIndex(5));
                Assert.AreEqual(5, tree.GetFirstBySegment(5));
                Assert.AreEqual(1, tree.GetByIndex(5).Count());

                Assert.AreEqual(1, tree.Delete(5).Length);

                Assert.AreEqual(50, tree.Length);

                Assert.AreEqual(0, tree.GetFirstByIndex(5));
            }
        }


        [Test]
        public void NTreeDeletesByLocationIndividualUnique()
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

                tree.DeleteAt(5L);

                Assert.AreEqual(50, tree.Length);

                Assert.AreEqual(0, tree[5].Index);
            }
        }

        [Test]
        public void NTreeDeletesManyUnique()
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

                tree.DeleteMany(new int[] { 1, 2, 3, 4, 5, 48, 49 });

                Assert.AreEqual(50, tree.Length);

                Assert.AreEqual(0, tree.GetFirstByIndex(1));
                Assert.AreEqual(0, tree.GetFirstByIndex(2));
                Assert.AreEqual(0, tree.GetFirstByIndex(3));
                Assert.AreEqual(0, tree.GetFirstByIndex(4));
                Assert.AreEqual(0, tree.GetFirstByIndex(5));
                Assert.AreEqual(0, tree.GetFirstByIndex(48));
                Assert.AreEqual(0, tree.GetFirstByIndex(49));
            }
        }

        [Test]
        public void NTreeDeletesSingleEntity()
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

                tree.Delete(objs[10]);

                Assert.AreEqual(50, tree.Length);

                Assert.AreEqual(0, tree.GetFirstByIndex(objs[10].Id));
            }
        }

        [Test]
        public void NTreeDeletesBySegment()
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

                tree.DeleteBySegment(11);

                Assert.AreEqual(50, tree.Length);

                Assert.AreEqual(0, tree.GetFirstByIndex(objs[10].Id));
            }
        }

        [Test]
        public void NTreeDeletesNonUniqueBySegment()
        {
            var objs = TestResourceFactory.GetMockClassAObjects(50).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());

            using (var tree = new NTree<int, MockClassA, long>("Id", false))
            {
                foreach (var o in objs)
                    tree.AddOrUpdate(new Tuple<MockClassA, long>(o, seedSegment.Increment()));

                Assert.AreEqual(50, tree.Length);

                Assert.AreEqual(5, tree.GetFirstByIndex(5));
                Assert.AreEqual(5, tree.GetFirstBySegment(5));
                Assert.AreEqual(1, tree.GetByIndex(5).Count());

                tree.DeleteBySegment(11);

                Assert.AreEqual(50, tree.Length);

                Assert.AreEqual(0, tree.GetFirstByIndex(objs[10].Id));
            }
        }

        [Test]
        public void NTreePassiveOnDeleteNullEntity()
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

                Assert.AreEqual(0, tree.Delete(null).Length);
            }
        }

        [Test]
        public void NTreeDeletesManyEntities()
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

                tree.DeleteMany(objs.Take(15));

                Assert.AreEqual(50, tree.Length);

                foreach (var o in objs.Take(15))
                    Assert.AreEqual(0, tree.GetFirstByIndex(o.Id));

                Assert.AreEqual(0, tree.DeleteMany(new List<MockClassA>() { null }).Length);
            }
        }
    }
}
