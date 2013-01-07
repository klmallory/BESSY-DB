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

namespace BESSy.Tests.RepositoryTests
{
    [TestFixture]
    public class RepositoryCapacityTests
    {
        ISafeFormatter _bsonFormatter;
        IBatchFileManager<MockClassA> _zipManager;
        IBatchFileManager<MockClassA> _bsonManager;
        IIndexedEntityMapManager<MockClassA, int> _mapManager;

        IList<MockClassA> _testEntities;

        [TestFixtureSetUp()]
        public void FixtureSetup()
        {
            //_bsonFormatter = TestResourceFactory.CreateBsonFormatter();
            //_zipManager = TestResourceFactory.CreateBatchFileManager<MockClassA>(TestResourceFactory.CreateZipFormatter());
            //_bsonManager = TestResourceFactory.CreateBatchFileManager<MockClassA>(TestResourceFactory.CreateBsonFormatter());
            //_mapManager = TestResourceFactory.CreateIndexedMapManager<MockClassA, int>(_bsonFormatter, new BinConverter32());
            ////_optimizedManager = TestResourceFactory.CreateOptimizedMapManager<MockClassA, int>(_bsonFormatter, new BinConverter32());

            //_testEntities = TestResourceFactory.GetMockClassAObjects(3);
        }

        [SetUp]
        public void Setup()
        {
            if (File.Exists("testTypeRepository.scenario"))
                File.Delete("testTypeRepository.scenario");

            _bsonFormatter = TestResourceFactory.CreateBsonFormatter();
            _zipManager = TestResourceFactory.CreateBatchFileManager<MockClassA>(TestResourceFactory.CreateZipFormatter());
            _bsonManager = TestResourceFactory.CreateBatchFileManager<MockClassA>(TestResourceFactory.CreateBsonFormatter());
            _mapManager = TestResourceFactory.CreateIndexedMapManager<MockClassA, int>(_bsonFormatter, new BinConverter32());

            _testEntities = TestResourceFactory.GetMockClassAObjects(3);
        }

        [Test]
        public void TypeRepositoryAddsOneHundredThousandRecordsInNinetySeconds()
        {
            var repo = new Repository<MockClassA, int>
                (-1
                , "testTypeRepository.scenario"
                , true
                , new Seed32(999)
                , new BinConverter32()
                , _bsonManager
                , _mapManager
                , (m => m.Id)
               , ((m, id) => m.Id = id));

            repo.Load();

            int i = -1;

            while (i <= 102400)
            {
                i++;

                var item = TestResourceFactory.CreateRandom();
                item.Name = i.ToString();

                repo.Add(item);

                if (i % 1024 == 0 && i > 0)
                {
                    Console.WriteLine(string.Format("Added ids {0} through {1}", i - 1024, i));
                }
            }

            Stopwatch sw = new Stopwatch();

            sw.Start();

            repo.Flush();

            while (repo.FileFlushQueueActive && sw.Elapsed.TotalSeconds <  90)
                Thread.Sleep(100);

            sw.Stop();

            Assert.IsFalse(repo.FileFlushQueueActive, "To much Time taken to flush db file.");

            Console.WriteLine(string.Format("Flush took {0} seconds for {1} entites", sw.Elapsed.TotalSeconds, i));

            sw.Reset();

            sw.Start();

            var entity = repo.Fetch(99999);

            sw.Stop();

            Console.WriteLine(string.Format("Fetch took {0} seconds for entity with id {1}", sw.Elapsed.TotalSeconds, 99999));

            Assert.IsNotNull(entity);
            entity = repo.Fetch(32317);
            Assert.IsNotNull(entity);
            entity = repo.Fetch(32851);
            Assert.IsNotNull(entity);
            entity = repo.Fetch(33385);
            Assert.IsNotNull(entity);
            entity = repo.Fetch(34508);
            Assert.IsNotNull(entity);
            entity = repo.Fetch(34509);
            Assert.IsNotNull(entity);
            entity = repo.Fetch(34510);
            Assert.IsNotNull(entity);

            repo.Dispose();
        }

        [Test]
        public void TypeRepositoryAddsOneMillionRecordsInEightMinutes()
        {
            var repo = new Repository<MockClassA, int>
                (-1
                , "testTypeRepository.scenario"
                , true
                , new Seed32(999)
                , new BinConverter32()
                , _bsonManager
                , _mapManager
                , (m => m.Id)
               , ((m, id) => m.Id = id));

            repo.Load();

            int i = -1;

            while (i <= 1024000)
            {
                i++;

                var item = TestResourceFactory.CreateRandom();
                item.Name = i.ToString();

                repo.Add(item);

                if (i % 2048 == 0 && i > 0)
                {
                    Console.WriteLine(string.Format("Added ids {0} through {1}", i - 2048, i));

                    repo.Sweep();
                }
            }

            Stopwatch sw = new Stopwatch();

            sw.Start();

            repo.Flush();

            while (repo.FileFlushQueueActive && sw.Elapsed.TotalSeconds < 500)
                Thread.Sleep(100);

            sw.Stop();

            Assert.IsFalse(repo.FileFlushQueueActive, "To much Time taken to flush db file.");

            Console.WriteLine(string.Format("Flush took {0} seconds for {1} entites", sw.Elapsed.TotalSeconds, i));

            sw.Reset();

            sw.Start();

            var entity = repo.Fetch(99999);

            sw.Stop();

            Console.WriteLine(string.Format("Fetch took {0} seconds for entity with id {1}", sw.Elapsed.TotalSeconds, 99999));

            Assert.IsNotNull(entity);

            repo.Dispose();
        }
    }
}
