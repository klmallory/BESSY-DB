using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BESSy.Files;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using BESSy.Tests.Mocks;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BESSy.Tests.SynchronizationTests
{
    [TestFixture]
    public class RowSynchronizerTests
    {
        Random random = new Random((int)(DateTime.Now.Ticks % int.MaxValue));

        [SetUp]
        public void Setup()
        {

        }

        [Test]
        public void RowSynchronizesSameThreadBypassesLock()
        {
            var sync = new RowSynchronizer<int>(new BinConverter32());
            using (var lock1 = sync.Lock(10))
            {
                Assert.IsNotNull(lock1);

                using (var lock2 = sync.Lock(10, 100))
                { }

                Assert.IsTrue(sync.HasLocks());
            }

            Assert.IsFalse(sync.HasLocks());
        }

        [Test]
        public void RowSynchronizesSinleRowLocks()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            bool exceptionThrown = false;

            var sync = new RowSynchronizer<int>(new BinConverter32());

            using (var lock1 = sync.Lock(10))
            {
                Parallel.For(1, 3, delegate(int i)
                {
                    try
                    {
                        if (Thread.CurrentThread.ManagedThreadId != threadId)
                            using (var lock2 = sync.Lock(10, 100))
                            { }
                        else
                            Thread.Sleep(500);
                    }
                    catch (RowLockTimeoutException) { exceptionThrown = true; }
                });
            }

            Assert.IsTrue(exceptionThrown);
        }

        [Test]
        public void RowSynchronizesStartRangeRowLocks()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            bool exceptionThrown = false;

            var sync = new RowSynchronizer<int>(new BinConverter32());

            using (var lock1 = sync.Lock(new Range<int>(10, 20)))
            {
                Parallel.For(1, 3, delegate(int i)
                {
                    try
                    {
                        if (Thread.CurrentThread.ManagedThreadId != threadId)
                            using (var lock2 = sync.Lock(new Range<int>(5, 11), 100))
                            { }
                        else
                            Thread.Sleep(500);
                    }
                    catch (RowLockTimeoutException) { exceptionThrown = true; }
                });
            }

            Assert.IsTrue(exceptionThrown);
        }

        [Test]
        public void RowSynchronizesEndRangeRowLocks()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            bool exceptionThrown = false;

            var sync = new RowSynchronizer<int>(new BinConverter32());

            using (var lock1 = sync.Lock(new Range<int>(10, 20)))
            {
                Parallel.For(1, 3, delegate(int i)
                {
                    try
                    {
                        if (Thread.CurrentThread.ManagedThreadId != threadId)
                            using (var lock2 = sync.Lock(new Range<int>(12, 22), 100))
                            { }
                        else
                            Thread.Sleep(500);
                    }
                    catch (RowLockTimeoutException) { exceptionThrown = true; }
                });
            }

            Assert.IsTrue(exceptionThrown);
        }


        [Test]
        public void RowSynchronizesSingleRowTryLockReturnsFalse()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            bool exceptionThrown = false;

            var sync = new RowSynchronizer<int>(new BinConverter32());
            RowLock<int> rowLock;

            using (var lock1 = sync.Lock(new Range<int>(10, 20)))
            {
                Parallel.For(1, 3, delegate(int i)
                {
                    try
                    {
                        if (Thread.CurrentThread.ManagedThreadId != threadId)
                            Assert.IsFalse(sync.TryLock(15, 100, out rowLock));

                        else
                            Thread.Sleep(500);
                    }
                    catch (RowLockTimeoutException) { exceptionThrown = true; }
                });
            }

            Assert.IsFalse(exceptionThrown);
        }

        [Test]
        public void RowSynchronizesLockAll()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            bool exceptionThrown = true;

            var sync = new RowSynchronizer<int>(new BinConverter32());

            using (var lock1 = sync.LockAll())
            {
                Parallel.For(1, 12, delegate(int i)
                {
                    try
                    {
                        if (Thread.CurrentThread.ManagedThreadId != threadId)
                            using (var lock2 = sync.Lock(random.Next(), 100))
                            {
                                exceptionThrown = false;
                            }
                        else
                            Thread.Sleep(500);
                    }
                    catch (RowLockTimeoutException) { exceptionThrown &= true; }
                });
            }

            Assert.IsTrue(exceptionThrown);
        }
    }
}
