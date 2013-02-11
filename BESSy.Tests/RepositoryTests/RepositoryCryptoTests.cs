using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using BESSy.Files;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BESSy.Tests.RepositoryTests
{
    [TestFixture]
    public class RepositoryCryptoTests
    {
        ISafeFormatter _bsonFormatter;
        IBatchFileManager<MockClassA> _cryptoFormatter;
        IBatchFileManager<MockClassA> _bsonManager;
        IIndexedEntityMapManager<MockClassA, int> _mapManager;
        IIndexedEntityMapManager<MockClassA, string> _stringMapManager;
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
            _cryptoFormatter = TestResourceFactory.CreateBatchFileManager<MockClassA>(TestResourceFactory.CreateCryptoFormatter());
            _bsonManager = TestResourceFactory.CreateBatchFileManager<MockClassA>(TestResourceFactory.CreateBsonFormatter());
            _mapManager = TestResourceFactory.CreateIndexedMapManager<MockClassA, int>(_bsonFormatter, new BinConverter32());
            _stringMapManager = TestResourceFactory.CreateIndexedMapManager<MockClassA, string>(_bsonFormatter, new BinConverterString());
            _testEntities = TestResourceFactory.GetMockClassAObjects(3);
        }

        void Cleanup()
        {
            if (File.Exists(_testName + ".scenario"))
                File.Delete(_testName + ".scenario");
        }

        [Test]
        public void TestCryptoLoadsInfoFromExistingZipFileAndUpdatesAndDeletes()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();


            var repo = new Repository<MockClassA, int>
                (-1
                , _testName + ".scenario"
                , true
                , new Seed32(999)
                , new BinConverter32()
                , _bsonFormatter
                , _cryptoFormatter
                , "GetId"
               , "SetId");

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
                , _testName + ".scenario"
                , true
                , new Seed32(999)
                , new BinConverter32()
                , _bsonFormatter
                , _cryptoFormatter
                , "GetId"
               , "SetId");

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
        public void TestCryptoLoadsInfoFromExistingZipFileAndUpdatesAndDeletesWithStringSeed()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var repo = new Repository<MockClassA, string>
                (-1
                , _testName + ".scenario"
                , true
                , new SeedString()
                , new BinConverterString()
                , _bsonFormatter
                , _cryptoFormatter
                , "GetName"
               , "SetName");

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
                , _testName + ".scenario"
                , true
                , new SeedString()
                , new BinConverterString()
                , _bsonFormatter
                , _cryptoFormatter
                , "GetId"
               , "SetId");

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
