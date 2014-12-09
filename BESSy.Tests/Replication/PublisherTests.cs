using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using BESSy.Extensions;
using BESSy.Tests.Mocks;
using NUnit.Framework;
using BESSy.Replication;

namespace BESSy.Tests.Replication
{
    [TestFixture]
    public class PublisherTests : FileTest
    {
        protected override void Cleanup()
        {
            base.Cleanup();

            if (Directory.Exists(Path.Combine(Environment.CurrentDirectory, _testName)))
                Directory.Delete(Path.Combine(Environment.CurrentDirectory, _testName), true);

        }

        [Test]
        public void PublisherInititializes()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id")
                .WithPublishing("Test", new FilePublisher<int, MockClassA>(_testName)))
            {
                db.Load();

                Assert.IsTrue(Directory.Exists(Path.Combine(Environment.CurrentDirectory, _testName)));

                db.Flush();
            }
        }


        [Test]
        public void PublisherUnInititializes()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id")
                .WithPublishing("Test", new FilePublisher<int, MockClassA>(_testName)))
            {
                db.Load();

                Assert.IsTrue(Directory.Exists(Path.Combine(Environment.CurrentDirectory, _testName)));

                db.Flush();
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database")
                .WithPublishing("Test", new FilePublisher<int, MockClassA>(_testName)))
            {
                db.Load();
                db.WithoutPublishing("Test");

                Assert.IsTrue(Directory.Exists(Path.Combine(Environment.CurrentDirectory, _testName)));

                using (var tran = db.BeginTransaction())
                {
                    TestResourceFactory.GetMockClassAObjects(25).ToList().ForEach(m => db.Add(m));

                    tran.Commit();
                }

                Thread.Sleep(100);

                Assert.AreEqual(0, Directory.GetFiles(Path.Combine(Environment.CurrentDirectory, _testName), "*.tLock").Count());
            }
        }

        [Test]
        public void PublisherWritesTransactionOnCommit()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id")
                .WithPublishing("Test", new FilePublisher<int, MockClassA>(_testName)))
            {
                db.Load();

                Assert.IsTrue(Directory.Exists(Path.Combine(Environment.CurrentDirectory, _testName)));
                db.Add(TestResourceFactory.CreateRandom());
                db.Flush();
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database")
                .WithPublishing("Test", new FilePublisher<int, MockClassA>(_testName)))
            {
                db.Load();

                Assert.IsTrue(Directory.Exists(Path.Combine(Environment.CurrentDirectory, _testName)));

                using (var tran = db.BeginTransaction())
                {
                    TestResourceFactory.GetMockClassAObjects(25).ToList().ForEach(m => db.Add(m));

                    tran.Commit();
                }

                while (db.FileFlushQueueActive)
                    Thread.Sleep(100);

                Assert.Greater(Directory.GetFiles(Path.Combine(Environment.CurrentDirectory, _testName), "*.trans").Count(), 0);
            }
        }

        [Test]
        public void PublisherQueuesTransactionsOnPause()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                db.Load();

                db.Add(TestResourceFactory.CreateRandom());

                db.FlushAll();
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database")
                .WithPublishing("Test", new FilePublisher<int, MockClassA>(_testName)))
            {
                db.Load();

                Assert.IsTrue(Directory.Exists(Path.Combine(Environment.CurrentDirectory, _testName)));

                using (var fs = File.Create(Path.Combine(Environment.CurrentDirectory, _testName, "test.pause")))
                {
                    fs.Flush();
                }

                using (var tran = db.BeginTransaction())
                {
                    TestResourceFactory.GetMockClassAObjects(25).ToList().ForEach(m => db.Add(m));

                    tran.Commit();
                }

                Thread.Sleep(100);

                Assert.AreEqual(0, Directory.GetFiles(Path.Combine(Environment.CurrentDirectory, _testName), "*.trans").Count());

                File.Delete(Path.Combine(Environment.CurrentDirectory, _testName, "test.pause"));

                Thread.Sleep(3000);

                Assert.Greater(Directory.GetFiles(Path.Combine(Environment.CurrentDirectory, _testName), "*.trans").Count(), 0);

                db.FlushAll();
            }
        }
    }
}
