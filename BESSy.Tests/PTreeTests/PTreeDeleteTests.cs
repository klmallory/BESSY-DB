using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using BESSy.Seeding;
using BESSy.Cache;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using System.Reflection;

namespace BESSy.Tests.PTreeTests
{
    [TestFixture]
    public class PTreeDeleteTests : FileTest
    {
        [Test]
        public void PTreeDeletesByIndexIndividualUnique()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(50).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());

            using (var tree = new PTree<int, MockClassA, long>("Id", _testName + ".index", true))
            {
                tree.Load();

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
        public void PTreeDeletesByIndexIndividualNonUnique()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(50).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());

            using (var tree = new PTree<int, MockClassA, long>("Id", _testName + ".index", false))
            {
                tree.Load();

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
        public void PTreeDeletesByLocationIndividualUnique()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(50).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());

            using (var tree = new PTree<int, MockClassA, long>("Id", _testName + ".index", true))
            {
                tree.Load();

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
        public void PTreeDeletesManyUnique()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(50).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());

            using (var tree = new PTree<int, MockClassA, long>("Id", _testName + ".index", true))
            {
                tree.Load();

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
        public void PTreeDeletesSingleEntity()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(50).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());

            using (var tree = new PTree<int, MockClassA, long>("Id", _testName + ".index", true))
            {
                tree.Load();

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
        public void PTreeDeletesBySegment()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(50).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());

            using (var tree = new PTree<int, MockClassA, long>("Id", _testName + ".index", true))
            {
                tree.Load();

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
        public void PTreeDeletesNonUniqueBySegment()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(50).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());

            using (var tree = new PTree<int, MockClassA, long>("Id", _testName + ".index", false))
            {
                tree.Load();

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
        public void PTreePassiveOnDeleteNullEntity()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(50).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());

            using (var tree = new PTree<int, MockClassA, long>("Id", _testName + ".index", true))
            {
                tree.Load();

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
        public void PTreeDeletesManyEntities()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(50).ToList();

            var seedIndex = new Seed32();
            var seedSegment = new Seed64();

            objs.ForEach(o => o.Id = seedIndex.Increment());

            using (var tree = new PTree<int, MockClassA, long>("Id", _testName + ".index", true))
            {
                tree.Load();

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
