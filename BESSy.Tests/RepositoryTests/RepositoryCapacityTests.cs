/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using BESSy.Files;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using NUnit.Framework;

namespace BESSy.Tests.RepositoryTests
{
    [TestFixture]
    public class RepositoryCapacityTests : FileTest
    {
        IQueryableFormatter _bsonFormatter;
        IBatchFileManager<MockClassA> _zipManager;
        IBatchFileManager<MockClassA> _bsonManager;
        IIndexedEntityMapManager<MockClassA, int> _mapManager;

        IList<MockClassA> _testEntities;

        [SetUp]
        public void Setup()
        {
            _bsonFormatter = TestResourceFactory.CreateBsonFormatter();
            _zipManager = TestResourceFactory.CreateBatchFileManager<MockClassA>(TestResourceFactory.CreateZipFormatter());
            _bsonManager = TestResourceFactory.CreateBatchFileManager<MockClassA>(TestResourceFactory.CreateBsonFormatter());
            _mapManager = TestResourceFactory.CreateIndexedMapManager<MockClassA, int>(_bsonFormatter, new BinConverter32());

            _testEntities = TestResourceFactory.GetMockClassAObjects(3);
        }

        [Test]
        public void TypeRepositoryAddsOneHundredThousandRecords()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var testManager = new MockBatchFileManager<MockClassA>(0);

            var repo = new Repository<MockClassA, int>
                (-1
                , _testName + ".scenario"
                , true
                , new Seed32(999)
                , new BinConverter32()
                , _bsonFormatter
                , testManager
                , "Id");

            repo.Load();

            int i = -1;

            while (i <= 65536)
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

            while (repo.FileFlushQueueActive && sw.Elapsed.TotalSeconds < 90)
                Thread.Sleep(100);

            sw.Stop();

            Assert.IsFalse(repo.FileFlushQueueActive, "To much Time taken to flush db file.");

            Console.WriteLine(string.Format("Flush took {0} seconds for {1} entites", sw.Elapsed.TotalSeconds, i));

            sw.Reset();

            sw.Start();

            var entity = repo.Fetch(45536);

            sw.Stop();

            Console.WriteLine(string.Format("Fetch took {0} seconds for entity with prop {1}", sw.Elapsed.TotalSeconds, 45536));

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


            repo.Sweep();

            repo.Dispose();
        }

        [Test]
        [Category("Performance")]
        public void TypeRepositoryAddsOneHundredThousandRecordsInNinetySeconds()
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
                , _bsonManager
                , "Id");

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

            Console.WriteLine(string.Format("Fetch took {0} seconds for entity with prop {1}", sw.Elapsed.TotalSeconds, 99999));

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
        [Category("Performance")]
        public void TypeRepositoryAddsOneMillionRecordsInEightMinutes()
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
                , _bsonManager
                , "Id");

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

            while (repo.FileFlushQueueActive && sw.Elapsed.TotalSeconds < 900)
                Thread.Sleep(100);

            sw.Stop();

            Assert.IsFalse(repo.FileFlushQueueActive, "To much Time taken to flush db file.");

            Console.WriteLine(string.Format("Flush took {0} seconds for {1} entites", sw.Elapsed.TotalSeconds, i));

            sw.Reset();

            sw.Start();

            var entity = repo.Fetch(99999);

            sw.Stop();

            Console.WriteLine(string.Format("Fetch took {0} seconds for entity with prop {1}", sw.Elapsed.TotalSeconds, 99999));

            Assert.IsNotNull(entity);

            repo.Dispose();
        }
    }
}
