﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using BESSy.Extensions;
using BESSy.Tests.Mocks;
using NUnit.Framework;

namespace BESSy.Tests.Replication
{
    [TestFixture]
    public class SubscriberTests : FileTest
    {
        protected override void Cleanup()
        {
            base.Cleanup();

            if (Directory.Exists(Path.Combine(Environment.CurrentDirectory, _testName)))
                Directory.Delete(Path.Combine(Environment.CurrentDirectory, _testName), true);
        }

        [Test]
        public void SubscriberInititializes()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            using (var pdb = new Database<int, MockClassA>(_testName + ".publisher" + ".database", "Id")
                .WithPublishing(Path.Combine(Environment.CurrentDirectory, _testName)))
            {
                pdb.Load();

                Assert.IsTrue(Directory.Exists(Path.Combine(Environment.CurrentDirectory, _testName)));

                using (var sdb = new Database<int, MockClassA>(_testName + ".subscriber" + ".database", "Id")
                    .WithSubscription(Path.Combine(Environment.CurrentDirectory, _testName), new TimeSpan(0, 0, 0, 0, 500)))
                {
                    sdb.Load();

                    Assert.IsTrue(Directory.Exists(Path.Combine(Environment.CurrentDirectory, _testName)));

                    sdb.Flush();
                }

                pdb.Flush();
            }
        }

        [Test]
        public void SubscriberPicksUpTransaction()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            using (var pdb = new Database<Guid, MockClassA>(_testName + ".publisher" + ".database", "ReplicationID")
                .WithPublishing(Path.Combine(Environment.CurrentDirectory, _testName)))
            {
                pdb.Load();

                Assert.IsTrue(Directory.Exists(Path.Combine(Environment.CurrentDirectory, _testName)));

                using (var sdb = new Database<Guid, MockClassA>(_testName + ".subscriber" + ".database", "ReplicationID")
                    .WithSubscription(Path.Combine(Environment.CurrentDirectory, _testName), new TimeSpan(0, 0, 0, 0, 500)))
                {
                    sdb.Load();

                    Assert.IsTrue(Directory.Exists(Path.Combine(Environment.CurrentDirectory, _testName)));

                    var objects = TestResourceFactory.GetMockClassAObjects(25).OfType<MockClassC>().ToList();

                    using (var tran = pdb.BeginTransaction())
                    {
                        objects.ForEach(o => o.ReplicationID = pdb.Add(o));

                        tran.Commit();
                    }

                    var sw = new Stopwatch();
                    sw.Start();

                    while (sdb.Fetch(objects.First().ReplicationID) == null && sw.ElapsedMilliseconds < 6000)
                        Thread.Sleep(750);


                    Assert.IsNotNull(sdb.Fetch(objects.First().ReplicationID));

                    sdb.Flush();
                }

                pdb.Flush();
            }
        }

        [Test]
        public void SubscriberUnInititializes()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            using (var pdb = new Database<int, MockClassA>(_testName + ".publisher" + ".database", "Id")
                .WithPublishing(Path.Combine(Environment.CurrentDirectory, _testName)))
            {
                pdb.Load();

                Assert.IsTrue(Directory.Exists(Path.Combine(Environment.CurrentDirectory, _testName)));

                using (var sdb = new Database<int, MockClassA>(_testName + ".subscriber" + ".database", "Id")
                    .WithSubscription(Path.Combine(Environment.CurrentDirectory, _testName), new TimeSpan(0, 0, 0, 0, 500)))
                {
                    sdb.Load();
                    sdb.WithoutSubscription();

                    Assert.IsTrue(Directory.Exists(Path.Combine(Environment.CurrentDirectory, _testName)));

                    sdb.Flush();
                }

                pdb.Flush();
            }
        }
    }
}
