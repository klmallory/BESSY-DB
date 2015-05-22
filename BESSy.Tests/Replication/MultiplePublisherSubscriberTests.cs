using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using BESSy.Extensions;
using BESSy.Tests.Mocks;
using NUnit.Framework;
using BESSy.Transactions;
using BESSy.Serialization;
using BESSy.Replication;
using BESSy.Seeding;
using BESSy.Files;

namespace BESSy.Tests.Replication
{
    [TestFixture]
    public class MultiplePublisherSubscriberTests : FileTest
    {
        protected override void Cleanup()
        {
            base.Cleanup();

            if (Directory.Exists(Path.Combine(Environment.CurrentDirectory, _testName)))
                Directory.Delete(Path.Combine(Environment.CurrentDirectory, _testName), true);
        }

        [Test]
        public void PubSubNeverPicksUpOwnTransactions()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();

            using (var fLock = new ManagedFileLock(_testName))
            {
                Cleanup();

                using (var pdb1 = new Database<Guid, MockClassA>(_testName + ".subscriber" + ".database", "ReplicationID", new FileCore<Guid, long>())
                    .WithPublishing("Test", new FilePublisher<Guid, MockClassA>(Path.Combine(Environment.CurrentDirectory, _testName)))
                    .WithSubscription("Test", new FileSubscriber<Guid, MockClassA>(Path.Combine(Environment.CurrentDirectory, _testName), new TimeSpan(0, 0, 0, 0, 500))))
                {
                    pdb1.Load();
                    pdb1.Clear();

                    using (var t = pdb1.BeginTransaction())
                    {
                        pdb1.Delete(o => o != null);

                        t.Commit();
                    }

                    Assert.IsTrue(Directory.Exists(Path.Combine(Environment.CurrentDirectory, _testName)));

                    var objects = TestResourceFactory.GetMockClassAObjects(25).OfType<MockClassC>().ToList();
                    var negativeObjects = TestResourceFactory.GetMockClassAObjects(25).OfType<MockClassC>().ToList();

                    using (var t = pdb1.BeginTransaction())
                    {
                        objects.ForEach(o => pdb1.Add(o));

                        t.Commit();
                    }

                    var testTran = new MockTransaction<Guid, MockClassA>(
                        new TransactionManager<Guid, MockClassA>(
                            new MockTransactionFactory<Guid, MockClassA>()
                            , new TransactionSynchronizer<Guid, MockClassA>()));

                    testTran.Source = pdb1.TransactionSource;

                    foreach (var no in negativeObjects)
                        testTran.Enlist(Action.Create, no.ReplicationID, (MockClassA)no);

                    testTran.Commit();

                    var formatter = new BSONFormatter();

                    using (var fs = new FileStream(
                        Path.Combine(Environment.CurrentDirectory, _testName, testTran.Id.ToString() + ".trans")
                        , FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 4096, false))
                    {
                        var s = formatter.FormatObjStream(testTran);

                        s.WriteAllTo(fs);

                        fs.Flush();
                        fs.Close();
                    }

                    Thread.Sleep(750);

                    var sw = new Stopwatch();
                    sw.Start();

                    while (pdb1.FileFlushQueueActive && sw.ElapsedMilliseconds < 3000)
                        Thread.Sleep(100);

                    Assert.AreEqual(25, pdb1.Length);
                }
            }
        }

        [Test]
        public void PubSubPicksUpOtherTransactions()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();

            using (var fLock = new ManagedFileLock(_testName))
            {
                Cleanup();

                using (var pdb1 = new Database<Guid, MockClassA>(_testName + ".publisher" + ".database", "ReplicationID", new FileCore<Guid, long>())
                    .WithPublishing("Test", new FilePublisher<Guid, MockClassA>(Path.Combine(Environment.CurrentDirectory, _testName)))
                    .WithSubscription("Test", new FileSubscriber<Guid, MockClassA>(Path.Combine(Environment.CurrentDirectory, _testName), new TimeSpan(0, 0, 0, 0, 500))))
                {
                    pdb1.Load();

                    Assert.IsTrue(Directory.Exists(Path.Combine(Environment.CurrentDirectory, _testName)));

                    using (var pdb2 = new Database<Guid, MockClassA>(_testName + ".subscriber" + ".database", "ReplicationID", new FileCore<Guid, long>())
                        .WithPublishing("Test", new FilePublisher<Guid, MockClassA>(Path.Combine(Environment.CurrentDirectory, _testName)))
                        .WithSubscription("Test", new FileSubscriber<Guid, MockClassA>(Path.Combine(Environment.CurrentDirectory, _testName), new TimeSpan(0, 0, 0, 0, 500))))
                    {
                        pdb2.Load();

                        Assert.IsTrue(Directory.Exists(Path.Combine(Environment.CurrentDirectory, _testName)));

                        var objects1 = TestResourceFactory.GetMockClassAObjects(25).OfType<MockClassC>().ToList();

                        var objects2 = TestResourceFactory.GetMockClassAObjects(25).OfType<MockClassC>().ToList();

                        using (var tran = pdb1.BeginTransaction())
                        {
                            objects1.ForEach(o => o.ReplicationID = pdb1.Add(o));

                            tran.Commit();
                        }

                        using (var tran = pdb2.BeginTransaction())
                        {
                            objects2.ForEach(o => o.ReplicationID = pdb2.Add(o));

                            tran.Commit();
                        }

                        var sw = new Stopwatch();
                        sw.Start();

                        while (pdb2.Fetch(objects1.First().ReplicationID) == null && sw.ElapsedMilliseconds < 6000)
                            Thread.Sleep(750);

                        Assert.IsNotNull(pdb2.Fetch(objects1.First().ReplicationID));

                        sw.Reset();

                        while (pdb1.Fetch(objects2.First().ReplicationID) == null && sw.ElapsedMilliseconds < 6000)
                            Thread.Sleep(750);

                        Assert.IsNotNull(pdb1.Fetch(objects2.First().ReplicationID));

                        sw.Stop();

                        pdb2.Flush();
                    }

                    pdb1.Flush();
                }

            }
        }
    }
}

