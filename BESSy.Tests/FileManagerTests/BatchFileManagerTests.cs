using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using BESSy.Serialization;
using BESSy.Files;
using BESSy.Tests.Mocks;
using System.IO;
using BESSy.Serialization.Converters;
using BESSy.Seeding;

namespace BESSy.Tests.FileManagerTests
{
    [TestFixture]
    public class BatchFileManagerTests
    {
        ISafeFormatter _bsonFormatter;
        IList<MockClassA> _testEntities;
        string _testName;

        [TestFixtureSetUp()]
        public void FixtureSetup()
        {

        }

        [SetUp]
        public void Setup()
        {
            _bsonFormatter = TestResourceFactory.CreateBsonFormatter();
            _testEntities = TestResourceFactory.GetMockClassAObjects(4096);
        }

        void Cleanup()
        {
            if (File.Exists(_testName + ".scenario"))
                File.Delete(_testName + ".scenario");
        }

        [Test]
        public void BatchSavesAndLoads()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var batch = new BatchFileManager<MockClassA>(_bsonFormatter);

            IList<MockClassA> loaded = null;

            using (var write = batch.GetWritableFileStream(_testName + ".scenario"))
            {
                batch.SaveBatch(write, _testEntities, 0);
                write.Flush();
            }

            using (var read = batch.GetReadableFileStream(_testName + ".scenario"))
            {
                var count = batch.GetBatchedSegmentCount(read);
                Assert.AreEqual(_testEntities.Count, count);

                read.Position = 0;
                loaded = batch.LoadBatchFrom(read);
            }

            Assert.IsNotNull(loaded);
            Assert.AreEqual(_testEntities.Count, loaded.Count);

            for (var i = 0; i < _testEntities.Count; i++)
                Assert.AreEqual(loaded[0].Name, _testEntities[0].Name, string.Format("Items did not match at index {0}.", i));

            loaded.Clear();
            batch.Dispose();

            Cleanup();
        }

        [Test]
        public void BatchSavesAndLoadsWithSeed()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var batch = new BatchFileManager<MockClassA>(_bsonFormatter);

            IList<MockClassA> loaded = null;
            ISeed<int> seed = new Seed32(9999);

            Enumerable.Range(1, 9999).ToList().ForEach(delegate(int i)
            {
                seed.Open(i);
            });

            using (var write = batch.GetWritableFileStream(_testName + ".scenario"))
            {
                var pos = batch.SaveSeed<int>(write, seed);

                batch.SaveBatch(write, _testEntities, pos);

                write.Flush();
            }

            using (var read = batch.GetReadableFileStream(_testName + ".scenario"))
            {
                var count = batch.GetBatchedSegmentCount(read);
                Assert.AreEqual(_testEntities.Count, count);
                read.Position = 0;

                seed = batch.LoadSeedFrom<int>(read);
                loaded = batch.LoadBatchFrom(read);
            }

            Assert.IsNotNull(seed);
            Assert.AreEqual(9999, seed.LastSeed);
            Assert.AreEqual(1, seed.Peek());

            Assert.IsNotNull(loaded);
            Assert.AreEqual(_testEntities.Count, loaded.Count);

            for (var i = 0; i < _testEntities.Count; i++)
                Assert.AreEqual(loaded[0].Name, _testEntities[0].Name, string.Format("Items did not match at index {0}.", i));

            loaded.Clear();
            batch.Dispose();

            Cleanup();
        }

        [Test]
        public void BatchSavesAndLoadsLargeCapacityWithSeed()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var entities = TestResourceFactory.GetMockClassAObjects(102400);
            var batch = new BatchFileManager<MockClassA>(_bsonFormatter);

            List<MockClassA> loaded = new List<MockClassA>();
            ISeed<int> seed = new Seed32(9999);

            Enumerable.Range(1, 9999).ToList().ForEach(delegate(int i)
            {
                seed.Open(i);
            });

            using (var write = batch.GetWritableFileStream(_testName + ".scenario"))
            {
                var pos = batch.SaveSeed<int>(write, seed);

                var taken = 0;
                while (taken < entities.Count)
                {
                    pos = batch.SaveBatch(write, entities.Skip(taken).Take(batch.BatchSize).ToList(), pos);

                    write.Flush();

                    taken += batch.BatchSize;
                }
            }

            using (var read = batch.GetReadableFileStream(_testName + ".scenario"))
            {
                var count = batch.GetBatchedSegmentCount(read);

                Assert.AreEqual(entities.Count, count);
                read.Position = 0;
                seed = batch.LoadSeedFrom<int>(read);
                int readCount = 1;

                while (readCount > 0)
                {
                    var reading = batch.LoadBatchFrom(read);

                    loaded.AddRange(reading);

                    readCount = reading.Count;
                }
            }

            Assert.IsNotNull(seed);
            Assert.AreEqual(9999, seed.LastSeed);
            Assert.AreEqual(1, seed.Peek());

            Assert.IsNotNull(loaded);
            Assert.AreEqual(entities.Count, loaded.Count);

            for (var i = 0; i < entities.Count; i++)
                Assert.AreEqual(loaded[0].Name, entities[0].Name, string.Format("Items did not match at index {0}.", i));

            loaded.Clear();
            batch.Dispose();

            Cleanup();
        }
    }
}
