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
    public class RepositoryMethodTests
    {
        ISafeFormatter _bsonFormatter;
        IBatchFileManager<MockClassA> _zipManager;
        IBatchFileManager<MockClassA> _bsonManager;
        IIndexedEntityMapManager<MockClassA, int> _mapManager;

        IList<MockClassA> _testEntities;
        string _testName;

        [SetUp]
        public void Setup()
        {
            _bsonFormatter = TestResourceFactory.CreateBsonFormatter();
            _zipManager = TestResourceFactory.CreateBatchFileManager<MockClassA>(TestResourceFactory.CreateZipFormatter());
            _bsonManager = TestResourceFactory.CreateBatchFileManager<MockClassA>(TestResourceFactory.CreateBsonFormatter());
            _mapManager = TestResourceFactory.CreateIndexedMapManager<MockClassA, int>(_bsonFormatter, new BinConverter32());

            _testEntities = TestResourceFactory.GetMockClassAObjects(3);
        }

        void Cleanup()
        {
            if (File.Exists(_testName + ".scenario"))
                File.Delete(_testName + ".scenario");
        }

        [Test]
        [Category("Performance")]
        public void TestReflectionMethod()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var repo = new Repository<MockClassA, int>
                (_testName + ".scenario"
                , "Id");

            repo.Load();

            int i = 0;

            while (i < 102400)
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

            repo = new Repository<MockClassA, int>(_testName + ".scenario");

            repo.Load();

            Assert.AreEqual(102400, repo.Length);
        }
    }
}
