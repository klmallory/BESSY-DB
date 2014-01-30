/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using BESSy.Tests.Mocks;
using BESSy.Json;
using BESSy.Serialization;
using BESSy.Files;
using BESSy.Serialization.Converters;
using BESSy.Seeding;
using System.Threading;
using System.Diagnostics;
using System.IO;

namespace BESSy.Tests.RepositoryTests
{
    [TestFixture]
    public class RepositoryLinqTests : FileTest
    {
        IQueryableFormatter _bsonFormatter;
        IBatchFileManager<MockClassA> _bsonManager;
        IIndexedEntityMapManager<MockClassA, int> _mapManager;

        IList<MockClassA> _testEntities;

        [TestFixtureSetUp()]
        public void FixtureSetup()
        {

        }

        [SetUp]
        public void Setup()
        {
            _bsonFormatter = TestResourceFactory.CreateBsonFormatter();
            _bsonManager = TestResourceFactory.CreateBatchFileManager<MockClassA>(_bsonFormatter);
            _mapManager = TestResourceFactory.CreateIndexedMapManager<MockClassA, int>(_bsonFormatter, new BinConverter32());

            _testEntities = TestResourceFactory.GetMockClassAObjects(3);
        }

        [Test]
        //[Category("Performance")]
        public void LinqTests()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var seed = new Seed32(999);

            var repo = new Repository<MockClassA, int>
                (-1
                , _testName + ".scenario"
                , true
                , seed
                , (IBinConverter<int>)new BinConverter32()
                , _bsonFormatter
                , _bsonManager
                , "Id");

            repo.Load();

            int i = 0;

            while (i <= 10240)
            {
                var r = TestResourceFactory.CreateRandom();
                r.Name = "Class " + i;

                repo.Add(r);

                if (i % 1024 == 0 && i > 0)
                {
                    Console.WriteLine(string.Format("Added ids {0} through {1}", i - 1024, i));

                    repo.Sweep();
                }

                i++;
            }

            repo.Flush();

            while (repo.FileFlushQueueActive)
                Thread.Sleep(10);

            repo.ClearCache();

            var entity = repo.Fetch(1783);
            Assert.IsNotNull(entity);

            entity = repo.Fetch(1784);
            Assert.IsNotNull(entity);

            entity = repo.Fetch(1785);
            Assert.IsNotNull(entity);

            entity = repo.Fetch(1786);
            Assert.IsNotNull(entity);

            entity = repo.Fetch(9999);
            Assert.IsNotNull(entity);

            entity = repo.Fetch(3399);
            Assert.IsNotNull(entity);

            entity = repo.Fetch(3400);
            Assert.IsNotNull(entity);

            entity = repo.Fetch(11240);
            Assert.IsNull(entity);

            Assert.AreEqual(0, repo.Select(s => true).Where(e => e == null).Count());

            Stopwatch sw = new Stopwatch();

            var list = repo.Select(e => e.Value<int>("Id") > 5000 && e.Value<int>("Id") < 6000);

            sw.Start();

            Assert.AreEqual(999, list.Count());

            sw.Stop();

            Console.WriteLine(string.Format("Linq Where clause for ids {0} to {1} in {2} milliseconds.", 5000, 6000, sw.Elapsed.TotalMilliseconds));

            /* First Test */
            sw.Reset();

            sw.Start();

            var first = repo.SelectFirst(e => e.Value<int>("Id") == 6053, 1).FirstOrDefault();

            sw.Stop();

            Console.WriteLine(string.Format("Linq First clause for prop {0} in {1} milliseconds.", 6053, sw.Elapsed.TotalMilliseconds));

            Assert.IsNotNull(first);
            Assert.AreEqual(6053, first.Id);

            /* FirstOrDefault Test */
            sw.Reset();

            sw.Start();

            first = repo.SelectFirst(e => e.Value<string>("Name") == "Super Stud Muffin", 1).FirstOrDefault();

            sw.Stop();

            Console.WriteLine(string.Format("Linq FirstOrDefault clause for name {0} in {1} milliseconds.", "Super Stud Muffin", sw.Elapsed.TotalMilliseconds));

            Assert.IsNull(first);

            /* Last Test */
            sw.Reset();

            sw.Start();

            var last = repo.SelectLast(e => e.Value<int>("Id") == 3400, 1).LastOrDefault();

            sw.Stop();

            Console.WriteLine(string.Format("Linq Last clause for prop {0} in {1} milliseconds.", 3400, sw.Elapsed.TotalMilliseconds));

            Assert.IsNotNull(last);

            /* Last Test */
            sw.Reset();

            sw.Start();

            var type = typeof(MockClassC).FullName;
            
            last = repo.Select(s => s.Value<string>("$type").Contains(type) && s.SelectToken("Location").Value<int>("X") == 2.0f).LastOrDefault();

            sw.Stop();

            Console.WriteLine(string.Format("Linq LastOrDefault clause for prop {0} in {1} milliseconds.", 3400, sw.Elapsed.TotalMilliseconds));

            Assert.IsNull(last);

        }
    }
}
