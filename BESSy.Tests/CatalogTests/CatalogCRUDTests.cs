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
        ISeed<int> _seed;
        IBinConverter<int> _idConverter;
        IBinConverter<string> _propertyConverter;
        ISafeFormatter _bsonFormatter;
        IBatchFileManager<IndexPropertyPair<int, string>> _indexBatchManager;
        IBatchFileManager<MockClassA> _bsonManager;
        IIndexMapManager<int, string> _mapManager;
        IIndexMapManager<string, string> _stringMapManager;
        IIndexRepository<int, string> _index;

        IList<MockClassA> _testEntities;

        IList<string> Names = new List<string>() { "Hello", "Sneakers", "0Submarine", "Angel" };

        [SetUp]
        public void Setup()
        {
            if (Directory.Exists(Path.Combine(Environment.CurrentDirectory, "Catalogs")))
                Directory.Delete(Path.Combine(Environment.CurrentDirectory, "Catalogs"), true);

            if (File.Exists("testTypeRepository.catalog.index"))
                File.Delete("testTypeRepository.catalog.index");

            _seed = new Seed32(999);
            _idConverter = new BinConverter32();
            _propertyConverter = new BinConverterString(1);
            _bsonFormatter = TestResourceFactory.CreateBsonFormatter();
            _bsonManager = TestResourceFactory.CreateBatchFileManager<MockClassA>(_bsonFormatter);
            _indexBatchManager = TestResourceFactory.CreateBatchFileManager<IndexPropertyPair<int, string>>(_bsonFormatter);

            _mapManager = TestResourceFactory.CreateIndexMapManager<int, string>("testTypeRepository.catalog.index", _idConverter, _propertyConverter);
            _stringMapManager = TestResourceFactory.CreateIndexMapManager<string, string>("testTypeRepository.catalog.index", new BinConverterString(), _propertyConverter);
            _testEntities = TestResourceFactory.GetMockClassAObjects(3);

            _index = TestResourceFactory.CreateIndexRepository<int, string>("Catalogs", "testTypeRepository.catalog.index", _seed, _idConverter, _indexBatchManager, _mapManager);
        }

        [Test]
        public void TestLoadsContentToMappingFile()
        {
            var catalog = new Catalog<MockClassA, int, string>
                ("testTypeRepository.catalog.index"
                , Path.Combine(Environment.CurrentDirectory, "Catalogs")
                , (m => m.Id)
               , ((m, i) => m.Id = i)
               , (m => m == null || string.IsNullOrWhiteSpace(m.Name) ? "Null" : m.Name.Substring(0, 1).ToUpper()));

            catalog.Load();

            for (var i = 0; i <_testEntities.Count; i++)
                catalog.Add(_testEntities[i].WithName(Names[i]));

            catalog.Flush();

            Thread.Sleep(100);

            while (catalog.FileFlushQueueActive)
                Thread.Sleep(100);

            Assert.AreEqual(3, catalog.Count());

            catalog.Dispose();
        }

        [Test]
        public void TestTypeRepositoryGeneratesSequentialKeysAbove1000()
        {
            var catalog = new Catalog<MockClassA, int, string>
                (Path.Combine(Environment.CurrentDirectory, "Catalogs")
                , 8192
                , _bsonFormatter
                , _idConverter
                , _propertyConverter
                , _index
                , _bsonManager
                , (m => m.Id)
               , ((m, i) => m.Id = i)
               , (m => m == null || string.IsNullOrWhiteSpace(m.Name) ? "Null" : m.Name.Substring(0, 1).ToUpper()));

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
                (Environment.CurrentDirectory
                , 8192
                , _bsonFormatter
                , _idConverter
                , _propertyConverter
                , TestResourceFactory.CreateIndexRepository<int, string>("Catalogs", "testTypeRepository.catalog.index", _seed, _idConverter, _indexBatchManager, _mapManager)
                , _bsonManager
                , (m => m.Id)
               , ((m, i) => m.Id = i)
               , (m => m == null || string.IsNullOrWhiteSpace(m.Name) ? "Null" : m.Name.Substring(0, 1).ToUpper()));

            catalog.Load();

            Assert.AreEqual(5, catalog.Count());

            Assert.AreEqual(1003, testEntity1.Id);
            Assert.AreEqual(1004, testEntity2.Id);

            catalog.Dispose();
        }

        [Test]
        public void TestCache()
        {
            var catalog = new Catalog<MockClassA, int, string>
                (Path.Combine(Environment.CurrentDirectory, "Catalogs")
                , 8192
                , _bsonFormatter
                , _idConverter
                , _propertyConverter
                , _index
                , _bsonManager
                , (m => m.Id)
               , ((m, i) => m.Id = i)
               , (m => m == null || string.IsNullOrWhiteSpace(m.Name) ? "Null" : m.Name.Substring(0, 1).ToUpper()));

            catalog.Load();

            for (var i = 0; i < _testEntities.Count; i++)
                catalog.Add(_testEntities[i].WithName(Names[i]));

            Assert.AreEqual(1003, catalog.Add(TestResourceFactory.CreateRandom().WithName("RockMaterial")));
            Assert.AreEqual(1004, catalog.Add(TestResourceFactory.CreateRandom().WithName("Angel")));

            Assert.IsTrue(catalog.Contains(1000));
            Assert.IsTrue(catalog.Contains(1001));
            Assert.IsTrue(catalog.Contains(1002));
            Assert.IsTrue(catalog.Contains(1003));
            Assert.IsTrue(catalog.Contains(1004));

            catalog.Detach(1000);

            Assert.IsFalse(catalog.Contains(1000));

            catalog.Flush();

            while (catalog.FileFlushQueueActive)
                Thread.Sleep(100);

            Assert.AreEqual(5, catalog.Count());

            catalog.Dispose();
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestEntityAddedCanNotBeNull()
        {
            var catalog = new Catalog<MockClassA, int, string>
                (Path.Combine(Environment.CurrentDirectory, "Catalogs")
                , 8192
                , _bsonFormatter
                , _idConverter
                , _propertyConverter
                , _index
                , _bsonManager
                , (m => m.Id)
               , ((m, i) => m.Id = i)
               , (m => m == null || string.IsNullOrWhiteSpace(m.Name) ? "Null" : m.Name.Substring(0, 1).ToUpper()));

            catalog.Load();

            for (var i = 0; i < _testEntities.Count; i++)
                catalog.Add(_testEntities[i].WithName(Names[i]));

            Assert.AreEqual(1003, catalog.Add(null));

            catalog.Clear();

            catalog.Dispose();
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestEntityAddedCanNotHaveNullCatalogedProperty()
        {
            var catalog = new Catalog<MockClassA, int, string>
                (Path.Combine(Environment.CurrentDirectory, "Catalogs")
                , 8192
                , _bsonFormatter
                , _idConverter
                , _propertyConverter
                , _index
                , _bsonManager
                , (m => m.Id)
               , ((m, i) => m.Id = i)
               , (m => m == null || string.IsNullOrWhiteSpace(m.Name) ? null : m.Name.Substring(0, 1).ToUpper()));

            catalog.Load();

            for (var i = 0; i < _testEntities.Count; i++)
                catalog.Add(_testEntities[i].WithName(Names[i]));

            catalog.Add(TestResourceFactory.CreateRandom().WithName(null));

            catalog.Clear();

            catalog.Dispose();
        }

        [Test]
        public void TestLoadsInfoFromExistingZipFileAndUpdatesAndDeletes()
        {
            var catalog = new Catalog<MockClassA, int, string>
                (Path.Combine(Environment.CurrentDirectory, "Catalogs")
                , 8192
                , _bsonFormatter
                , _idConverter
                , _propertyConverter
                , _index
                , _bsonManager
                , (m => m.Id)
               , ((m, i) => m.Id = i)
               , (m => m == null || string.IsNullOrWhiteSpace(m.Name) ? "Null" : m.Name.Substring(0, 1).ToUpper()));

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

            catalog.Dispose();

            catalog = new Catalog<MockClassA, int, string>
                (Path.Combine(Environment.CurrentDirectory, "Catalogs")
                , 8192
                , _bsonFormatter
                , _idConverter
                , _propertyConverter
                ,  TestResourceFactory.CreateIndexRepository<int, string>("Catalogs", "testTypeRepository.catalog.index", _seed, _idConverter, _indexBatchManager, _mapManager)
                , _bsonManager
                , (m => m.Id)
               , ((m, i) => m.Id = i)
               , (m => m == null || string.IsNullOrWhiteSpace(m.Name) ? "Null" : m.Name.Substring(0, 1).ToUpper()));

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
        public void TestLoadsInfoFromExistingZipFileAndUpdatesAndDeletesWithStringSeed()
        {
            var catalog = new Catalog<MockClassA, string, string>
              (Path.Combine(Environment.CurrentDirectory, "Catalogs")
              , 8192
              , _bsonFormatter
              , new BinConverterString()
              , _propertyConverter
              , TestResourceFactory.CreateIndexRepository<string, string>
                    ("Catalogs", "testTypeRepository.catalog.index"
                    , new SeedString(50)
                    , new BinConverterString()
                    , TestResourceFactory.CreateBatchFileManager<IndexPropertyPair<string, string>>
                        (_bsonFormatter)
                        , TestResourceFactory.CreateIndexMapManager<string, string>
                        ("testTypeRepository.catalog.index"
                            , new BinConverterString()
                            , _propertyConverter))
              , _bsonManager
              , (m => m.Name)
             , ((m, id) => m.Name = id)
             , (m => m == null || string.IsNullOrWhiteSpace(m.Name) ? "Null" : m.Name.Substring(0, 1).ToUpper()));

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

            catalog.Dispose();

            catalog = new Catalog<MockClassA, string, string>
              (Path.Combine(Environment.CurrentDirectory, "Catalogs")
              , 8192
              , _bsonFormatter
              , new BinConverterString()
              , _propertyConverter
              , TestResourceFactory.CreateIndexRepository<string, string>
                    ("Catalogs", "testTypeRepository.catalog.index"
                    , new SeedString(50)
                    , new BinConverterString()
                    , TestResourceFactory.CreateBatchFileManager<IndexPropertyPair<string, string>>
                        (_bsonFormatter)
                        , TestResourceFactory.CreateIndexMapManager<string, string>
                        ("testTypeRepository.catalog.index"
                            , new BinConverterString(50)
                            , _propertyConverter))
              , _bsonManager
              , (m => m.Name)
             , ((m, id) => m.Name = id)
             , (m => m == null || string.IsNullOrWhiteSpace(m.Name) ? "Null" : m.Name.Substring(0, 1).ToUpper()));

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
