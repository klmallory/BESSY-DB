using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Newtonsoft.Json;
using BESSy.Serialization;
using BESSy.Files;
using BESSy.Tests.Mocks;
using System.IO;
using BESSy.Serialization.Converters;
using BESSy.Seeding;
using System.Diagnostics;
using System.Threading;

namespace BESSy.Tests.RepositoryTests
{
    [TestFixture]
    public class RepositoryCRUDTests
    {
        ISafeFormatter _bsonFormatter;
        IBatchFileManager<MockClassA> _zipManager;
        IBatchFileManager<MockClassA> _bsonManager;
        IIndexedEntityMapManager<MockClassA, int> _mapManager;
        IIndexedEntityMapManager<MockClassA, string> _stringMapManager;

        IList<MockClassA> _testEntities;

        [TestFixtureSetUp()]
        public void FixtureSetup()
        {
            _bsonFormatter = TestResourceFactory.CreateBsonFormatter();
            _zipManager = TestResourceFactory.CreateBatchFileManager<MockClassA>(TestResourceFactory.CreateZipFormatter());
            _bsonManager = TestResourceFactory.CreateBatchFileManager<MockClassA>(TestResourceFactory.CreateBsonFormatter());
            _mapManager = TestResourceFactory.CreateIndexedMapManager<MockClassA, int>(_bsonFormatter, new BinConverter32());
            _stringMapManager = TestResourceFactory.CreateIndexedMapManager<MockClassA, string>(_bsonFormatter, new BinConverterString());
            _testEntities = TestResourceFactory.GetMockClassAObjects(3);
        }

        [SetUp]
        public void Setup()
        {
            if (File.Exists("testTypeRepository.scenario"))
                File.Delete("testTypeRepository.scenario");
        }

        [Test]
        public void TestLoadsContentToMappingFile()
        {
            var repo = new Repository<MockClassA, int>
                ("testTypeRepository.scenario"
                , (m => m.Id)
               , ((m, i) => m.Id = i));

            repo.Load();

            foreach (var e in _testEntities)
                repo.Add(e);

            repo.Flush();

            while (repo.FileFlushQueueActive)
                Thread.Sleep(100);

            Assert.AreEqual(3, repo.Count());

            repo.Dispose();
        }

        [Test]
        public void TestTypeRepositoryGeneratesSequentialKeysAbove1000()
        {
            var repo = new Repository<MockClassA, int>
                (-1
                , "testTypeRepository.scenario"
                , true
                , new Seed32(999)
                , new BinConverter32()
                , _zipManager
                , _mapManager
                , (m => m.Id)
               , ((m, i) => m.Id = i));

            repo.Load();

            var testEntity1 = TestResourceFactory.CreateRandom();
            var testEntity2 = TestResourceFactory.CreateRandom();

            repo.Add(testEntity1);
            repo.Add(testEntity2);

            Assert.AreEqual(1000, testEntity1.Id);
            Assert.AreEqual(1001, testEntity2.Id);

            repo.Dispose();
        }

        [Test]
        public void TestLoadsInfoFromExistingZipFileAndUpdatesAndDeletes()
        {
            var repo = new Repository<MockClassA, int>
                (-1
                , "testTypeRepository.scenario"
                , true
                , new Seed32(999)
                , new BinConverter32()
                , _zipManager
                , _mapManager
                , (m => m != null ? m.Id : 0)
               , ((m, i) => m.Id = i));

            repo.Load();

            foreach (var e in _testEntities)
                repo.Add(e);

            var testItem = TestResourceFactory.CreateRandom();
            testItem.Name = "RockMaterial";

            Assert.AreEqual(1003, repo.Add(testItem));
            Assert.AreEqual(1004, repo.Add(TestResourceFactory.CreateRandom()));

            Assert.IsNotNull(repo.GetFromCache(1000));
            Assert.IsNotNull(repo.GetFromCache(1001));
            Assert.IsNotNull(repo.GetFromCache(1002));
            Assert.IsNotNull(repo.GetFromCache(1003));
            Assert.IsNotNull(repo.GetFromCache(1004));
            
            repo.Flush();

            repo.Dispose();

            repo = new Repository<MockClassA, int>
                (-1
                , "testTypeRepository.scenario"
                , true
                , new Seed32(999)
                , new BinConverter32()
                , _zipManager
                , _mapManager
                , (m => m != null ? m.Id : 0)
               , ((m, i) => m.Id = i));

            var sw = new Stopwatch();
            sw.Start();

            repo.Load();

            sw.Stop();

            Console.WriteLine("repository loaded in {0} milliseconds", sw.ElapsedMilliseconds);

            var entity = (MockClassC)repo.Fetch(1003);

            Assert.IsNotNull(entity);
            Assert.AreEqual(5, repo.Count());
            Assert.AreEqual("RockMaterial", entity.Name);

            entity.Name = "RockMaterialUpdated";

            repo.Update(entity, 1003);

            sw.Reset();

            sw.Start();

            repo.Flush();

            sw.Stop();

            Console.WriteLine("repository flushed in {0} milliseconds", sw.ElapsedMilliseconds);

            entity = (MockClassC)repo.Fetch(1003);

            Assert.IsNotNull(entity);
            Assert.AreEqual("RockMaterialUpdated", entity.Name);

            repo.Delete(1003);

            repo.Flush();

            entity = repo.Fetch(1003) as MockClassC;
            Assert.IsNull(entity);

            repo.Dispose();
        }

        [Test]
        public void TestLoadsInfoFromExistingZipFileAndUpdatesAndDeletesWithStringSeed()
        {
            var repo = new Repository<MockClassA, string>
                (-1
                , "testTypeRepository.scenario"
                , true
                , new SeedString()
                , new BinConverterString()
                , _zipManager
                , _stringMapManager
                , (m => m != null ? m.Name : null)
               , ((m, id) => m.Name = id));

            repo.Load();

            foreach (var e in _testEntities)
                repo.Add(e);

            var testItem = TestResourceFactory.CreateRandom();
            testItem.Name = "RockMaterial";

            Assert.AreEqual("RockMaterial", repo.Add(testItem));

            Assert.IsNotNull(repo.GetFromCache("RockMaterial"));

            foreach (var e in _testEntities)
                Assert.IsNotNull(repo.GetFromCache(e.Name));

            repo.Flush();

            repo.Dispose();

            repo = new Repository<MockClassA, string>
                (-1
                , "testTypeRepository.scenario"
                , true
                , new SeedString()
                , new BinConverterString()
                , _zipManager
                , _stringMapManager
                , (m => m != null ? m.Name : null)
               , ((m, id) => m.Name = id));

            var sw = new Stopwatch();
            sw.Start();

            repo.Load();

            sw.Stop();

            Console.WriteLine("repository loaded in {0} milliseconds", sw.ElapsedMilliseconds);

            var entity = (MockClassC)repo.Fetch("RockMaterial");

            Assert.IsNotNull(entity);
            Assert.AreEqual(4, repo.Count());
            Assert.AreEqual("RockMaterial", entity.Name);

            entity.Name = "RockMaterialUpdated";

            repo.Update(entity, "RockMaterial");

            repo.Flush();

            entity = (MockClassC)repo.Fetch("RockMaterialUpdated");

            Assert.IsNotNull(entity);
            Assert.AreEqual("RockMaterialUpdated", entity.Name);

            repo.Delete("RockMaterialUpdated");

            repo.Flush();

            //Check if it is null before the flush is finished
            entity = repo.Fetch("RockMaterialUpdated") as MockClassC;
            Assert.IsNull(entity);
            entity = repo.Fetch("RockMaterial") as MockClassC;
            Assert.IsNull(entity);

            while (repo.FileFlushQueueActive)
                Thread.Sleep(100);

            //Check if it is null after the flush is finished
            entity = repo.Fetch("RockMaterialUpdated") as MockClassC;
            Assert.IsNull(entity);
            entity = repo.Fetch("RockMaterial") as MockClassC;
            Assert.IsNull(entity);

            repo.Dispose();
        }
    }
}
