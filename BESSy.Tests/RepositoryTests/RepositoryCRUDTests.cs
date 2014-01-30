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
using BESSy.Json;
using NUnit.Framework;

namespace BESSy.Tests.RepositoryTests
{
    [TestFixture]
    public class RepositoryCRUDTests : FileTest
    {
        [Test]
        public void TestLoadsContentToMappingFile()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var testEntities = TestResourceFactory.GetMockClassAObjects(3);

            var repo = new Repository<MockClassA, int>
                (_testName + ".scenario"
                , "Id");

            repo.Load();

            foreach (var e in testEntities)
                repo.Add(e);

            repo.Flush();

            while (repo.FileFlushQueueActive)
                Thread.Sleep(100);

            Assert.AreEqual(3, repo.Length);

            repo.Dispose();
        }

        [Test]
        public void TestTypeRepositoryGeneratesSequentialKeysAbove1000()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var testEntities = TestResourceFactory.GetMockClassAObjects(3);

            var repo = new Repository<MockClassA, int>
                (-1
                , _testName + ".scenario"
                , true
                , new Seed32(999)
                , new BinConverter32()
                , TestResourceFactory.CreateBsonFormatter()
                , TestResourceFactory.CreateBatchFileManager<MockClassA>(TestResourceFactory.CreateZipFormatter())
                , "Id");

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
        public void TestRepositoryCacheSweepRemovesExcessStorage()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var testEntities = TestResourceFactory.GetMockClassAObjects(3);

            var repo = new Repository<MockClassA, int>
                (12
                , _testName + ".scenario"
                , true
                , new Seed32(0)
                , new BinConverter32()
                , TestResourceFactory.CreateBsonFormatter()
                , TestResourceFactory.CreateBatchFileManager<MockClassA>(TestResourceFactory.CreateZipFormatter())
                , "Id");

            repo.Load();

            for (var i = 1; i < 22; i++)
                repo.Add(TestResourceFactory.CreateRandom().WithName(i.ToString()));

            repo.Flush();

            while (repo.FileFlushQueueActive)
                Thread.Sleep(100);
                        
            for (var i = 1; i < 22; i++)
                repo.Fetch(i);

            Assert.IsFalse(repo.Contains(1));
            Assert.IsFalse(repo.Contains(2));
            Assert.IsFalse(repo.Contains(3));
            Assert.IsFalse(repo.Contains(4));
            Assert.IsFalse(repo.Contains(5));
            Assert.IsFalse(repo.Contains(6));
            Assert.IsFalse(repo.Contains(7));
            Assert.IsFalse(repo.Contains(8));
            Assert.IsFalse(repo.Contains(9));
            Assert.IsFalse(repo.Contains(10));
            Assert.IsFalse(repo.Contains(11));
            Assert.IsFalse(repo.Contains(12));
        }
    
        [Test]
        public void TestLoadsInfoFromExistingZipFileAndUpdatesAndDeletes()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var testEntities = TestResourceFactory.GetMockClassAObjects(3);

            var repo = new Repository<MockClassA, int>
                (-1
                , _testName + ".scenario"
                , true
                , new Seed32(999)
                , new BinConverter32()
                , TestResourceFactory.CreateBsonFormatter()
                , TestResourceFactory.CreateBatchFileManager<MockClassA>(TestResourceFactory.CreateZipFormatter())
                , "Id");

            repo.Load();

            foreach (var e in testEntities)
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

            //Dispose should wait for the flush operation to complete.
            repo.Dispose();

            repo = new Repository<MockClassA, int>
                (-1
                , _testName + ".scenario"
                , true
                , new Seed32(999)
                , new BinConverter32()
                , TestResourceFactory.CreateBsonFormatter()
                , TestResourceFactory.CreateBatchFileManager<MockClassA>(TestResourceFactory.CreateZipFormatter())
                , "Id");

            var sw = new Stopwatch();
            sw.Start();

            repo.Load();

            sw.Stop();

            Console.WriteLine("repository loaded in {0} milliseconds", sw.ElapsedMilliseconds);

            var entity = (MockClassC)repo.Fetch(1003);

            Assert.IsNotNull(entity);
            Assert.AreEqual(5, repo.Length);
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

            //this fetch should retrieve from the staging cache until the flush is complete.
            entity = repo.Fetch(1003) as MockClassC;
            Assert.IsNull(entity);

            //Dispose should wait for the flush operation to complete.
            repo.Dispose();
        }

        [Test]
        public void TestLoadsInfoFromExistingZipFileAndUpdatesAndDeletesWithStringSeed()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var testEntities = TestResourceFactory.GetMockClassAObjects(3);

            var repo = new Repository<MockClassA, string>
                (-1
                , _testName + ".scenario"
                , true
                , new SeedString()
                , new BinConverterString()
                , TestResourceFactory.CreateJsonFormatter()
                , TestResourceFactory.CreateBatchFileManager<MockClassA>(TestResourceFactory.CreateZipFormatter())
                , "Name");

            repo.Load();

            foreach (var e in testEntities)
                repo.Add(e);

            var testItem = TestResourceFactory.CreateRandom();
            testItem.Name = "RockMaterial";

            Assert.AreEqual("RockMaterial", repo.Add(testItem));

            Assert.IsNotNull(repo.GetFromCache("RockMaterial"));

            foreach (var e in testEntities)
                Assert.IsNotNull(repo.GetFromCache(e.Name));

            repo.Flush();

            while (repo.FileFlushQueueActive)
                Thread.Sleep(100);

            //Dispose should wait for the flush operation to complete.
            repo.Dispose();

            repo = new Repository<MockClassA, string>
                (-1
                , _testName + ".scenario"
                , true
                , new SeedString()
                , new BinConverterString()
                , TestResourceFactory.CreateJsonFormatter()
                , TestResourceFactory.CreateBatchFileManager<MockClassA>(TestResourceFactory.CreateZipFormatter())
                , "Id");

            var sw = new Stopwatch();
            sw.Start();

            repo.Load();

            sw.Stop();

            Console.WriteLine("repository loaded in {0} milliseconds", sw.ElapsedMilliseconds);

            var entity = (MockClassC)repo.Fetch("RockMaterial");

            Assert.IsNotNull(entity);
            Assert.AreEqual(4, repo.Length);
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
