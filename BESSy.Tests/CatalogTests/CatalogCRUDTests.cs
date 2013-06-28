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
using BESSy.Factories;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using Newtonsoft.Json;
using NUnit.Framework;
using BESSy.Cache;

namespace BESSy.Tests.CatalogTests
{
    internal class CatalogCRUDTests : FileTest
    {
        ISeed<int> _seed;
        IBinConverter<int> _idConverter;
        IBinConverter<string> _propertyConverter;
        ISafeFormatter _bsonFormatter;
        IBatchFileManager<IndexPropertyPair<int, string>> _indexBatchManager;
        IBatchFileManager<IndexPropertyPair<string, string>> _stringBatchManager;
        IBatchFileManager<MockClassA> _bsonManager;
        IIndexMapManager<int, string> _mapManager;
        IIndexMapManager<string, string> _stringMapManager;
        IRepositoryCacheFactory _cacheFactory;
        IIndexRepositoryFactory<int, string> _indexFactory;

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

            _cacheFactory = TestResourceFactory.CreateRepositoryCacheFactory();

            _indexFactory = TestResourceFactory.CreateIndexFactory<int, string>(_idConverter, _propertyConverter, _seed);
        }

        protected override void Cleanup()
        {
            base.Cleanup();

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
                , "Id"
               , "CatalogName");

            catalog.Load();

            for (var i = 0; i < _testEntities.Count; i++)
                catalog.Add(_testEntities[i].WithName(Names[i]));

            catalog.Flush();

            while (catalog.FileFlushQueueActive)
                Thread.Sleep(100);

            Assert.AreEqual(3, catalog.Length);

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
                , 8192
                , _idConverter
                , _propertyConverter
                , "Id"
               , "CatalogName"
               , _bsonFormatter
               , TestResourceFactory.CreateBatchFileManager<MockClassA>(_bsonFormatter)
               , _cacheFactory
               , _indexFactory);

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
                , 8192
                , _bsonFormatter
                , _bsonManager
               , _cacheFactory
               , _indexFactory);

            catalog.Load();

            Assert.AreEqual(5, catalog.Length);

            Assert.AreEqual(1003, testEntity1.Id);
            Assert.AreEqual(1004, testEntity2.Id);

            catalog.Dispose();
        }

        [Test]
        public void TestCatalogCache()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            _cacheFactory.DefaultCacheSize = 3;

            var catalog = new Catalog<MockClassA, int, string>
                (_testName + ".catalog.index"
                , Path.Combine(Environment.CurrentDirectory, _testName)
                , 8192
                , _idConverter
                , _propertyConverter
                , "Id"
               , "CatalogName"
               , _bsonFormatter
               , TestResourceFactory.CreateBatchFileManager<MockClassA>(_bsonFormatter)
               , _cacheFactory
               , _indexFactory);

            catalog.Load();

            for (var i = 0; i < _testEntities.Count; i++)
                catalog.Add(_testEntities[i].WithName(Names[i]));

            Assert.AreEqual(1003, catalog.Add(TestResourceFactory.CreateRandom().WithName("RockMaterial")));
            Assert.AreEqual(1004, catalog.Add(TestResourceFactory.CreateRandom().WithName("Angel")));

            var cache = catalog.GetCache();

            Assert.IsFalse(cache.ContainsKey(1000));
            Assert.IsFalse(cache.ContainsKey(1001));
            Assert.IsFalse(cache.ContainsKey(1002));
            Assert.IsTrue(cache.ContainsKey(1003));
            Assert.IsTrue(cache.ContainsKey(1004));

            catalog.Flush();

            while (catalog.FileFlushQueueActive)
                Thread.Sleep(100);

            Assert.AreEqual(5, catalog.Length);

            catalog.DetachCatalog("S");

            cache = catalog.GetCache();

            Assert.IsFalse(cache.ContainsKey(1001));

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
                , 8192
                , _idConverter
                , _propertyConverter
                , "Id"
               , "CatalogName"
               , _bsonFormatter
               , TestResourceFactory.CreateBatchFileManager<MockClassA>(_bsonFormatter)
               , _cacheFactory
               , _indexFactory);


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
                , "Id"
                , "CatalogNameNull");

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
                , 8192
                , _idConverter
                , _propertyConverter
                , "Id"
               , "CatalogName"
               , _bsonFormatter
               , TestResourceFactory.CreateBatchFileManager<MockClassA>(_bsonFormatter)
               , _cacheFactory
               , _indexFactory);

            catalog.Load();

            for (var i = 0; i < _testEntities.Count; i++)
                catalog.AddOrUpdate(_testEntities[i].WithName(Names[i]), 0);

            Assert.AreEqual(1003, catalog.Add(TestResourceFactory.CreateRandom().WithName("RockMaterial")));
            Assert.AreEqual(1004, catalog.Add(TestResourceFactory.CreateRandom().WithName("Angel")));

            var cache = catalog.GetCache();

            Assert.IsTrue(cache.ContainsKey(1000));
            Assert.IsNotNull(cache.ContainsKey(1000));
            Assert.IsNotNull(cache.ContainsKey(1001));
            Assert.IsNotNull(cache.ContainsKey(1002));
            Assert.IsNotNull(cache.ContainsKey(1003));
            Assert.IsNotNull(cache.ContainsKey(1004));

            catalog.Flush();

            while (catalog.FileFlushQueueActive)
                Thread.Sleep(100);

            catalog.Dispose();

            catalog = new Catalog<MockClassA, int, string>
                (_testName + ".catalog.index"
                , Path.Combine(Environment.CurrentDirectory, _testName)
                , 8192
                , _bsonFormatter
                , _bsonManager
               , _cacheFactory
               , _indexFactory);

            var sw = new Stopwatch();
            sw.Start();

            catalog.Load();

            sw.Stop();

            Console.WriteLine("repository loaded in {0} milliseconds", sw.ElapsedMilliseconds);

            var entity = (MockClassC)catalog.Fetch(1003);

            Assert.IsNotNull(entity);
            Assert.AreEqual(5, catalog.Length);
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

            var indexFactory = TestResourceFactory.CreateIndexFactory<string, string>(_propertyConverter, _propertyConverter, new SeedString());

            var catalog = new Catalog<MockClassA, string, string>
                (_testName + ".catalog.index"
                , Path.Combine(Environment.CurrentDirectory, _testName)
                , 8192
                , new BinConverterString()
                , _propertyConverter
                , "Name"
               , "CatalogName"
               , _bsonFormatter
               , TestResourceFactory.CreateBatchFileManager<MockClassA>(_bsonFormatter)
               , _cacheFactory
               , indexFactory);

            catalog.Load();

            for (var i = 0; i < _testEntities.Count; i++)
                catalog.Add(_testEntities[i].WithName(Names[i]));

            Assert.AreEqual("RockMaterial", catalog.Add(TestResourceFactory.CreateRandom().WithName("RockMaterial")));
            Assert.AreEqual("Angel", catalog.Add(TestResourceFactory.CreateRandom().WithName("Angel")));

            var cache = catalog.GetCache();

            //Auto Cache should be on.
            Assert.IsNotNull(cache["Hello"]);
            Assert.IsNotNull(cache["Sneakers"]);
            Assert.IsNotNull(cache["0Submarine"]);
            Assert.IsNotNull(cache["RockMaterial"]);
            Assert.IsNotNull(cache["Angel"]);

            catalog.Flush();

            //Dispose should wait for the flush operation to complete.
            catalog.Dispose();

            catalog = new Catalog<MockClassA, string, string>
                (_testName + ".catalog.index"
                , Path.Combine(Environment.CurrentDirectory, _testName)
                , 8192
                , _bsonFormatter
                , _bsonManager
               , _cacheFactory
               , indexFactory);

            var sw = new Stopwatch();
            sw.Start();

            catalog.Load();

            sw.Stop();

            Console.WriteLine("repository loaded in {0} milliseconds", sw.ElapsedMilliseconds);

            var entity = (MockClassC)catalog.Fetch("RockMaterial");

            Assert.IsNotNull(entity);
            Assert.AreEqual(5, catalog.Length);
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
