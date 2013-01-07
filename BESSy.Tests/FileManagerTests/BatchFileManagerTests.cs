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

        [TestFixtureSetUp()]
        public void FixtureSetup()
        {
            _bsonFormatter = TestResourceFactory.CreateBsonFormatter();
            _testEntities = TestResourceFactory.GetMockClassAObjects(2048);
        }

        [SetUp]
        public void Setup()
        {
            if (File.Exists("testTypeRepository.scenario"))
                FileManager.Delete("testTypeRepository.scenario");
        }

        [Test]
        public void BatchSavesAndLoads()
        {
            var batch = new BatchFileManager<MockClassA>(2048, _bsonFormatter);

            IList<MockClassA> loaded = null;

            using (var write = batch.GetWritableFileStream("testTypeRepository.scenario"))
            {
                batch.SaveBatch(write, _testEntities, 0);
                write.Flush();
            }

            using (var read = batch.GetReadableFileStream("testTypeRepository.scenario"))
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
        }

        [Test]
        public void BatchSavesAndLoadsWithSeed()
        {
            var batch = new BatchFileManager<MockClassA>(2048, _bsonFormatter);

            IList<MockClassA> loaded = null;
            ISeed<int> seed = new Seed32(9999);

            Enumerable.Range(1, 9999).ToList().ForEach(delegate(int i)
            {
                seed.Open(i);
            });

            using (var write = batch.GetWritableFileStream("testTypeRepository.scenario"))
            {
                var pos = batch.SaveSeed<int>(write, seed);

                batch.SaveBatch(write, _testEntities, pos);

                write.Flush();
            }

            using (var read = batch.GetReadableFileStream("testTypeRepository.scenario"))
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
        }
    }
}
