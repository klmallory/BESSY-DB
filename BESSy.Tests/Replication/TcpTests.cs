using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.IO;
using BESSy.Tests.Mocks;
using System.Net;
using BESSy.Replication;
using System.Diagnostics;
using System.Threading;
using BESSy.Extensions;
using BESSy.Tests.Mocks.Tcp;
using BESSy.Replication.Tcp;

namespace BESSy.Tests.Replication
{
    [TestFixture]
    public class TcpTests : FileTest
    {
        protected override void Cleanup()
        {
            base.Cleanup();

            if (Directory.Exists(Path.Combine(Environment.CurrentDirectory, _testName)))
                Directory.Delete(Path.Combine(Environment.CurrentDirectory, _testName), true);
        }

        [Test]
        public void TcpListenerRecivesPublisher()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var random = new Random((int)(DateTime.Now.Ticks / 7335));

            var port = random.Next(8355, 10000);

            using (var pdb = new Database<int, MockClassA>(_testName + ".database", "Id")
                .WithPublishing("Test", new TcpTransactionPublisher<int, MockClassA>(IPAddress.Parse("127.0.0.1"), port, 1, new TcpSettings())))
            {
                pdb.Load();

                using (var sdb = new Database<int, MockClassA>(_testName + ".subscriber" + ".database", "Id")
                    .WithSubscription("Test", new TcpTransactionSubscriber<int, MockClassA>(port)))
                {
                    sdb.Load();

                    var obj = TestResourceFactory.CreateRandom();

                    using (var t = pdb.BeginTransaction())
                    {
                        obj.Id = pdb.Add(obj);

                        t.Commit();
                    }

                    var sw = new Stopwatch();
                    sw.Start();

                    while (sdb.Fetch(obj.Id) == null && sw.ElapsedMilliseconds < 2000)
                        Thread.Sleep(100);

                    Assert.IsNotNull(sdb.Fetch(obj.Id));

                    sdb.Flush();
                }

                pdb.Flush();
            }
        }

        [Test]
        public void TcpListenerSendsAuthError()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var random = new Random((int)(DateTime.Now.Ticks / 65782));

            var port = random.Next(8355, 10000);

            using (var testSub = new MockTcpSubscriber<int, MockClassA>(port))
            {
                testSub.ThrowAuthError = true;

                using (var pdb = new Database<int, MockClassA>(_testName + ".database", "Id")
                    .WithPublishing("Test", new TcpTransactionPublisher<int, MockClassA>(IPAddress.Parse("127.0.0.1"), port, 1)))
                {
                    pdb.Load();

                    using (var sdb = new Database<int, MockClassA>(_testName + ".subscriber" + ".database", "Id")
                        .WithSubscription("Test", testSub))
                    {
                        sdb.Load();

                        var obj = TestResourceFactory.CreateRandom();

                        using (var t = pdb.BeginTransaction())
                        {
                            obj.Id = pdb.Add(obj);

                            t.Commit();
                        }

                        var sw = new Stopwatch();
                        sw.Start();

                        while (sdb.Fetch(obj.Id) == null && sw.ElapsedMilliseconds < 2000)
                            Thread.Sleep(100);

                        Assert.IsNull(sdb.Fetch(obj.Id));

                        sdb.Flush();
                    }

                    pdb.Flush();
                }
            }
        }

        [Test]
        public void TcpListenerSendsPackageError()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var random = new Random((int)(DateTime.Now.Ticks / 845235));

            var port = random.Next(8355, 10000);

            using (var testSub = new MockTcpSubscriber<int, MockClassA>(port))
            {
                testSub.ThrowReadError = true;

                using (var pdb = new Database<int, MockClassA>(_testName + ".database", "Id")
                    .WithPublishing("Test", new TcpTransactionPublisher<int, MockClassA>(IPAddress.Parse("127.0.0.1"), port, 1)))
                {
                    pdb.Load();

                    using (var sdb = new Database<int, MockClassA>(_testName + ".subscriber" + ".database", "Id")
                        .WithSubscription("Test", testSub))
                    {
                        sdb.Load();

                        var obj = TestResourceFactory.CreateRandom();

                        using (var t = pdb.BeginTransaction())
                        {
                            obj.Id = pdb.Add(obj);

                            t.Commit();
                        }

                        var sw = new Stopwatch();
                        sw.Start();

                        while (sdb.Fetch(obj.Id) == null && sw.ElapsedMilliseconds < 2000)
                            Thread.Sleep(100);

                        Assert.IsNull(sdb.Fetch(obj.Id));

                        sdb.Flush();
                    }

                    pdb.Flush();
                }
            }
        }

        [Test]
        public void TcpListenerThrowsReadException()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var random = new Random((int)(DateTime.Now.Ticks / 114589));

            var port = random.Next(8355, 10000);

            using (var testSub = new MockTcpSubscriber<int, MockClassA>(port))
            {
                testSub.ThrowReadException = true;

                using (var pdb = new Database<int, MockClassA>(_testName + ".database", "Id")
                    .WithPublishing("Test", new TcpTransactionPublisher<int, MockClassA>(IPAddress.Parse("127.0.0.1"), port, 1)))
                {
                    pdb.Load();

                    using (var sdb = new Database<int, MockClassA>(_testName + ".subscriber" + ".database", "Id")
                        .WithSubscription("Test", testSub))
                    {
                        sdb.Load();

                        var obj = TestResourceFactory.CreateRandom();

                        using (var t = pdb.BeginTransaction())
                        {
                            obj.Id = pdb.Add(obj);

                            t.Commit();
                        }

                        var sw = new Stopwatch();
                        sw.Start();

                        while (sdb.Fetch(obj.Id) == null && sw.ElapsedMilliseconds < 2000)
                            Thread.Sleep(100);

                        Assert.IsNull(sdb.Fetch(obj.Id));

                        sdb.Flush();
                    }

                    pdb.Flush();
                }
            }
        }
    }
}
