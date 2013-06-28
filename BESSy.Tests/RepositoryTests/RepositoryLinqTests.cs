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
using Newtonsoft.Json;
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
        ISafeFormatter _bsonFormatter;
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
        [Category("Performance")]
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

            while (i <= 102400)
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

            var entity = repo.Fetch(31783);
            Assert.IsNotNull(entity);

            entity = repo.Fetch(31784);
            Assert.IsNotNull(entity);

            entity = repo.Fetch(31785);
            Assert.IsNotNull(entity);

            entity = repo.Fetch(31786);
            Assert.IsNotNull(entity);

            entity = repo.Fetch(99999);
            Assert.IsNotNull(entity);

            entity = repo.Fetch(103399);
            Assert.IsNotNull(entity);

            entity = repo.Fetch(103400);
            Assert.IsNotNull(entity);

            entity = repo.Fetch(103401);
            Assert.IsNull(entity);

            Assert.AreEqual(0, repo.Where(e => e == null).Count());

            Stopwatch sw = new Stopwatch();

            var list = repo.Where(e => e.Id > 50000 && e.Id < 60000);

            sw.Start();

            Assert.AreEqual(9999, list.Count());

            sw.Stop();

            Console.WriteLine(string.Format("Linq Where clause for ids {0} to {1} in {2} milliseconds.", 50000, 60000, sw.Elapsed.TotalMilliseconds));

            /* First Test */
            sw.Reset();

            sw.Start();

            var first = repo.First(e => e.Id == 76053);

            sw.Stop();

            Console.WriteLine(string.Format("Linq First clause for prop {0} in {1} milliseconds.", 76053, sw.Elapsed.TotalMilliseconds));

            Assert.IsNotNull(first);
            Assert.AreEqual(76053, first.Id);

            /* FirstOrDefault Test */
            sw.Reset();

            sw.Start();

            first = repo.FirstOrDefault(e => e.Name == "Super Stud Muffin");

            sw.Stop();

            Console.WriteLine(string.Format("Linq FirstOrDefault clause for name {0} in {1} milliseconds.", "Super Stud Muffin", sw.Elapsed.TotalMilliseconds));

            Assert.IsNull(first);

            /* Last Test */
            sw.Reset();

            sw.Start();

            var last = repo.Last(e => e.Id == 103400);

            sw.Stop();

            Console.WriteLine(string.Format("Linq Last clause for prop {0} in {1} milliseconds.", 103400, sw.Elapsed.TotalMilliseconds));

            Assert.IsNotNull(last);

            /* Last Test */
            sw.Reset();

            sw.Start();

            last = repo.OfType<MockClassC>().LastOrDefault(e => e.Location.X == 2.0f);

            sw.Stop();

            Console.WriteLine(string.Format("Linq LastOrDefault clause for prop {0} in {1} milliseconds.", 103400, sw.Elapsed.TotalMilliseconds));

            Assert.IsNull(last);

        }
    }
}
