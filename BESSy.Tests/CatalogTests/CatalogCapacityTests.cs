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
    internal class CatalogCapacityTests
    {
        ISeed<int> _seed;
        IBinConverter<int> _idConverter;
        IBinConverter<string> _propertyConverter;
        ISafeFormatter _bsonFormatter;
        IBatchFileManager<IndexPropertyPair<int, string>> _indexBatchManager;
        IBatchFileManager<MockClassA> _bsonManager;
        IIndexMapManager<int, string> _mapManager;
        IIndexMapManager<string, string> _stringMapManager;
        IList<MockClassA> _testEntities;

        IList<string> Names = new List<string>() { "Hello World", "Sneakers", "0Submarine", "Angel", "Farside", "Pumpkin Eater", "Turd Biscuit", "Bunny Pants", "Crap on a stick", "Zork", "_Sneaky", "Dork", "Nark" };

        string _testName;

        [SetUp]
        public void Setup()
        {
            _seed = new Seed32(999);
            _idConverter = new BinConverter32();
            _propertyConverter = new BinConverterString(1);
            _bsonFormatter = TestResourceFactory.CreateBsonFormatter();
            _bsonManager = TestResourceFactory.CreateBatchFileManager<MockClassA>(_bsonFormatter);
            _indexBatchManager = TestResourceFactory.CreateBatchFileManager<IndexPropertyPair<int, string>>(_bsonFormatter);
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
        [Category("Performance")]
        public void CatalogAddOneHundredThousandRecords()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var rnd = new Random();

            var catalog = new Catalog<MockClassA, int, string>
                (_testName + ".catalog.index"
                , Path.Combine(Environment.CurrentDirectory, "Catalogs")
                ,_propertyConverter
                , "GetId"
               , "SetId"
               , "GetCatalogId");

            catalog.Load();

            int i = -1;

            while (i <= 102400)
            {
                i++;

                catalog.Add(TestResourceFactory.CreateRandom().WithName(Names[rnd.Next(0, Names.Count - 1)] + " " + i));

                if (i % 1024 == 0 && i > 0)
                {
                    Console.WriteLine(string.Format("Added ids {0} through {1}", i - 1024, i));
                }
            }

            var sw = new Stopwatch();

            sw.Start();

            catalog.Flush();

            while (catalog.FileFlushQueueActive && sw.Elapsed.TotalSeconds < 50000)
            {
                Thread.Sleep(100);
            }

            sw.Stop();

            Assert.IsFalse(catalog.FileFlushQueueActive, "To much Time taken to flush db file at {0} seconds.", sw.Elapsed.TotalSeconds);

            Console.WriteLine(string.Format("Flush took {0} seconds for {1} entites", sw.Elapsed.TotalSeconds, i));

            sw.Reset();

            sw.Start();

            var entity = catalog.Fetch(99999);

            sw.Stop();

            Console.WriteLine(string.Format("Fetch took {0} seconds for entity with id {1}", sw.Elapsed.TotalSeconds, 99999));

            Assert.IsNotNull(entity);

            catalog.Dispose();
        }
    }
}
