/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
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

namespace BESSy.Tests.CatalogTests
{
    internal class CatalogCRUDTests
    {
        string _testName;
        ISeed<int> _seed;
        IBinConverter<int> _idConverter;
        IBinConverter<string> _propertyConverter;
        ISafeFormatter _bsonFormatter;
        IBatchFileManager<IndexPropertyPair<int, string>> _indexBatchManager;
        IBatchFileManager<IndexPropertyPair<string, string>> _stringBatchManager;
        IBatchFileManager<MockClassA> _bsonManager;
        IIndexMapManager<int, string> _mapManager;
        IIndexMapManager<string, string> _stringMapManager;
        
        IList<MockClassA> _testEntities;

        IList<string> Names = new List<string>() { "Hello", "Sneakers", "0Submarine", "Angel" };

        [TestFixtureSetUp]
        public void FixtureSetup()
        {

        }

        [SetUp]
        public void Setup()
        {
            _seed = new Seed32(999);
            _idConverter = new BinConverter32();
            _propertyConverter = new BinConverterString(1);
            _bsonFormatter = TestResourceFactory.CreateBsonFormatter();
            _bsonManager = TestResourceFactory.CreateBatchFileManager<MockClassA>(_bsonFormatter);
            _indexBatchManager = TestResourceFactory.CreateBatchFileManager<IndexPropertyPair<int, string>>(_bsonFormatter);
            _stringBatchManager = TestResourceFactory.CreateBatchFileManager<IndexPropertyPair<string, string>>(_bsonFormatter);

            _mapManager = TestResourceFactory.CreateIndexMapManager<int, string>("testTypeRepository.catalog.index", _idConverter, _propertyConverter);
            _stringMapManager = TestResourceFactory.CreateIndexMapManager<string, string>("testTypeRepository.catalog.index", new BinConverterString(), _propertyConverter);
            _testEntities = TestResourceFactory.GetMockClassAObjects(3);
        }

        void Cleanup()
        {
            if (File.Exists(_testName + ".catalog.index"))
                File.Delete(_testName + ".catalog.index");

            if (Directory.Exists(Path.Combine(Environment.CurrentDirectory, _testName)))
                Directory.Delete(Path.Combine(Environment.CurrentDirectory, _testName), true);
        }

        [Test]
        public void TestCatalogLoadsContentToMappingFile()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var catalog = new Catalog<MockClassA, int, string>
                (_testName + ".catalog.index"
                , Path.Combine(Environment.CurrentDirectory, _testName)
                , _propertyConverter
                , "GetId"
               , "SetId"
               , "GetCatalogId");

            catalog.Load();

            for (var i = 0; i < _testEntities.Count; i++)
                catalog.Add(_testEntities[i].WithName(Names[i]));

            catalog.Flush();

            while (catalog.FileFlushQueueActive)
                Thread.Sleep(100);

            Assert.AreEqual(3, catalog.Count());

            catalog.Dispose();

            catalog = new Catalog<MockClassA, int, string>
                (_testName + ".catalog.index"
                , Path.Combine(Environment.CurrentDirectory, _testName));

            catalog.Load();

            for (var i = 0; i < _testEntities.Count; i++)
                Assert.IsNotNull(catalog.Fetch(_testEntities[i].Id));

            catalog.Dispose();
        }

        [Test]
        public void TestCatalogGeneratesSequentialKeysAbove1000()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var catalog = new Catalog<MockClassA, int, string>
                (_testName + ".catalog.index"
                , Path.Combine(Environment.CurrentDirectory, _testName)
                , 2048
                , 8192
                , _idConverter
                , _propertyConverter
                , _seed
                , "GetId"
               , "SetId"
               , "GetCatalogId"
               , _bsonFormatter
               , _bsonManager
               , _indexBatchManager);

            catalog.Load();

            for (var i = 0; i < _testEntities.Count; i++)
                catalog.Add(_testEntities[i].WithName(Names[i]));

            var testEntity1 = TestResourceFactory.CreateRandom().WithName("Snuggie");
            var testEntity2 = TestResourceFactory.CreateRandom().WithName("Turtle");

            catalog.Add(testEntity1);
            catalog.Add(testEntity2);

            Assert.AreEqual(1003, testEntity1.Id);
            Assert.AreEqual(1004, testEntity2.Id);

            catalog.Flush();

            catalog.Dispose();

            catalog = new Catalog<MockClassA, int, string>
                (_testName + ".catalog.index"
                , Path.Combine(Environment.CurrentDirectory, _testName)
                , 2048
                , 8192
                , _bsonFormatter
                , _bsonManager
                , _indexBatchManager);

            catalog.Load();

            Assert.AreEqual(5, catalog.Count());

            Assert.AreEqual(1003, testEntity1.Id);
            Assert.AreEqual(1004, testEntity2.Id);

            catalog.Dispose();
        }

        [Test]
        public void TestCatalogCache()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var catalog = new Catalog<MockClassA, int, string>
                (_testName + ".catalog.index"
                , Path.Combine(Environment.CurrentDirectory, _testName)
                , 3
                , 8192
                , _idConverter
                , _propertyConverter
                , new Seed32(999)
                , "GetId"
               , "SetId"
               , "GetCatalogId"
               , _bsonFormatter
               , _bsonManager
               , _indexBatchManager);


            catalog.Load();

            for (var i = 0; i < _testEntities.Count; i++)
                catalog.Add(_testEntities[i].WithName(Names[i]));

            Assert.AreEqual(1003, catalog.Add(TestResourceFactory.CreateRandom().WithName("RockMaterial")));
            Assert.AreEqual(1004, catalog.Add(TestResourceFactory.CreateRandom().WithName("Angel")));

            Assert.IsFalse(catalog.Contains(1000));
            Assert.IsTrue(catalog.Contains(1001));
            Assert.IsTrue(catalog.Contains(1002));
            Assert.IsTrue(catalog.Contains(1003));
            Assert.IsTrue(catalog.Contains(1004));

            catalog.Flush();

            while (catalog.FileFlushQueueActive)
                Thread.Sleep(100);

            Assert.AreEqual(5, catalog.Count());

            catalog.Detach(1001);

            Assert.IsFalse(catalog.Contains(1001));

            ((ICache<IMappedRepository<MockClassA, int>, string>)catalog).Detach("S");

            Thread.Sleep(100);

            while(catalog.FileFlushQueueActive)
                Thread.Sleep(100);

            Assert.IsFalse(((ICache<IMappedRepository<MockClassA, int>, string>)catalog).Contains("S"));

            catalog.Dispose();
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestCatalogEntityAddedCanNotBeNull()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var catalog = new Catalog<MockClassA, int, string>
                (_testName + ".catalog.index"
                , Path.Combine(Environment.CurrentDirectory, _testName)
                , 2048
                , 8192
                , _idConverter
                , _propertyConverter
                , new Seed32(999)
                , "GetId"
               , "SetId"
               , "GetCatalogId"
               , _bsonFormatter
               , _bsonManager
               , _indexBatchManager);


            catalog.Load();

            for (var i = 0; i < _testEntities.Count; i++)
                catalog.Add(_testEntities[i].WithName(Names[i]));

            try
            {
                Assert.AreEqual(1003, catalog.Add(null));
            }
            finally
            {
                catalog.Clear();

                catalog.Dispose();
            }
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestEntityAddedCanNotHaveNullCatalogedProperty()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var catalog = new Catalog<MockClassA, int, string>
                (_testName + ".catalog.index"
                , Path.Combine(Environment.CurrentDirectory, _testName)
                , _propertyConverter
                , "GetId"
               , "SetId"
               , "GetCatalogNull");

            catalog.Load();

            try
            {
                for (var i = 0; i < _testEntities.Count; i++)
                    catalog.Add(_testEntities[i].WithName(Names[i]));
            }
            finally
            {
                catalog.Clear();

                catalog.Dispose();
            }
        }

        [Test]
        public void TestCatalogLoadsInfoFromExistingFileAndUpdatesAndDeletes()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var catalog = new Catalog<MockClassA, int, string>
                (_testName + ".catalog.index"
                , Path.Combine(Environment.CurrentDirectory, _testName)
                , 2048
                , 8192
                , _idConverter
                , _propertyConverter
                , new Seed32(999)
                , "GetId"
               , "SetId"
               , "GetCatalogId"
               , _bsonFormatter
               , _bsonManager
               , _indexBatchManager);

            catalog.Load();

            for (var i = 0; i < _testEntities.Count; i++)
                catalog.AddOrUpdate(_testEntities[i].WithName(Names[i]), 0);

            Assert.AreEqual(1003, catalog.Add(TestResourceFactory.CreateRandom().WithName("RockMaterial")));
            Assert.AreEqual(1004, catalog.Add(TestResourceFactory.CreateRandom().WithName("Angel")));

            Assert.IsTrue(catalog.Contains(1000));
            Assert.IsNotNull(catalog.GetFromCache(1000));
            Assert.IsNotNull(catalog.GetFromCache(1001));
            Assert.IsNotNull(catalog.GetFromCache(1002));
            Assert.IsNotNull(catalog.GetFromCache(1003));
            Assert.IsNotNull(catalog.GetFromCache(1004));

            catalog.Flush();

            while (catalog.FileFlushQueueActive)
                Thread.Sleep(100);

            catalog.Dispose();

            catalog = new Catalog<MockClassA, int, string>
                (_testName + ".catalog.index"
                , Path.Combine(Environment.CurrentDirectory, _testName)
                , 2048
                , 8192
                , _bsonFormatter
                , _bsonManager
                , _indexBatchManager);

            var sw = new Stopwatch();
            sw.Start();

            catalog.Load();

            sw.Stop();

            Console.WriteLine("repository loaded in {0} milliseconds", sw.ElapsedMilliseconds);

            var entity = (MockClassC)catalog.Fetch(1003);

            Assert.IsNotNull(entity);
            Assert.AreEqual(5, catalog.Count());
            Assert.AreEqual("RockMaterial", entity.Name);

            entity.Name = "RockMaterialUpdated";

            catalog.Update(entity, 1003);

            sw.Reset();

            sw.Start();

            catalog.Flush();

            sw.Stop();

            Console.WriteLine("repository flushed in {0} milliseconds", sw.ElapsedMilliseconds);

            entity = (MockClassC)catalog.Fetch(1003);

            Assert.IsNotNull(entity);
            Assert.AreEqual("RockMaterialUpdated", entity.Name);

            catalog.Delete(1003);

            catalog.Flush();

            entity = catalog.Fetch(1003) as MockClassC;
            Assert.IsNull(entity);

            catalog.Dispose();
        }

        [Test]
        public void TestCatalogLoadsInfoFromExistingFileAndUpdatesAndDeletesWithStringSeed()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString().GetHashCode().ToString();
            Cleanup();

            var catalog = new Catalog<MockClassA, string, string>
                (_testName + ".catalog.index"
                , Path.Combine(Environment.CurrentDirectory, _testName)
                , 2048
                , 8192
                , new BinConverterString()
                , _propertyConverter
                , new SeedString()
                , "GetName"
               , "SetName"
               , "GetCatalogId"
               , _bsonFormatter
               , _bsonManager
               , _stringBatchManager);

            catalog.Load();

            for (var i = 0; i < _testEntities.Count; i++)
                catalog.Add(_testEntities[i].WithName(Names[i]));

            Assert.AreEqual("RockMaterial", catalog.Add(TestResourceFactory.CreateRandom().WithName("RockMaterial")));
            Assert.AreEqual("Angel", catalog.Add(TestResourceFactory.CreateRandom().WithName("Angel")));
            
            //Auto Cache should be on.
            Assert.IsNotNull(catalog.GetFromCache("Hello"));
            Assert.IsNotNull(catalog.GetFromCache("Sneakers"));
            Assert.IsNotNull(catalog.GetFromCache("0Submarine"));
            Assert.IsNotNull(catalog.GetFromCache("RockMaterial"));
            Assert.IsNotNull(catalog.GetFromCache("Angel"));

            catalog.Flush();

            //Dispose should wait for the flush operation to complete.
            catalog.Dispose();

            catalog = new Catalog<MockClassA, string, string>
                (_testName + ".catalog.index"
                , Path.Combine(Environment.CurrentDirectory, _testName)
                , 2048
                , 8192
                , _bsonFormatter
                , _bsonManager
                , _stringBatchManager);

            var sw = new Stopwatch();
            sw.Start();

            catalog.Load();

            sw.Stop();

            Console.WriteLine("repository loaded in {0} milliseconds", sw.ElapsedMilliseconds);

            var entity = (MockClassC)catalog.Fetch("RockMaterial");

            Assert.IsNotNull(entity);
            Assert.AreEqual(5, catalog.Count());
            Assert.AreEqual("RockMaterial", entity.Name);

            entity.Name = "RockMaterialUpdated";

            catalog.Update(entity, "RockMaterial");

            catalog.Flush();

            entity = (MockClassC)catalog.Fetch("RockMaterialUpdated");

            Assert.IsNotNull(entity);
            Assert.AreEqual("RockMaterialUpdated", entity.Name);

            catalog.Delete("RockMaterialUpdated");

            catalog.Flush();

            entity = catalog.Fetch("RockMaterialUpdated") as MockClassC;
            Assert.IsNull(entity);

            entity = catalog.Fetch("RockMaterial") as MockClassC;
            Assert.IsNull(entity);

            catalog.Dispose();
        }
    }
}
