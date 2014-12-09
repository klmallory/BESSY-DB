/*
Copyright (c) 2011,2012,2013 Kristen Mallory dba Klink

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BESSy.Extensions;
using BESSy.Files;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using BESSy.Transactions;
using BESSy.Json.Linq;
using NUnit.Framework;

namespace BESSy.Tests.TransactionTests
{
    [TestFixture]
    public class TransactionManagerTests
    {
        [Test]
        public void TransactionLockAutoCommitsTransactions()
        {
            var seed = new Seed32();
            int committed = 0;

            using (var manager = new TransactionManager<int, MockClassA>() )
            {
                manager.TransactionCommitted += new TransactionCommit<int, MockClassA>
                    (delegate(ITransaction<int, MockClassA> transaction)
                    {
                        Assert.AreEqual(3, transaction.GetEnlistedActions().Count());
                        committed += transaction.GetEnlistedActions().Count();

                        transaction.MarkComplete();
                    });

                var testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                testEntities.ForEach(e => e.Id = seed.Increment());

                using (var tLock1 = manager.BeginTransaction())
                {
                    foreach (var entity in testEntities)
                        tLock1.Transaction.Enlist(Action.Create, entity.Id, entity);

                    using (var tLock2 = manager.BeginTransaction())
                    {
                        testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                        testEntities.ForEach(e => e.Id = seed.Increment());

                        foreach (var entity in testEntities)
                            tLock2.Transaction.Enlist(Action.Create, entity.Id, entity);

                        using (var tLock3 = manager.BeginTransaction())
                        {
                            testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                            testEntities.ForEach(e => e.Id = seed.Increment());

                            foreach (var entity in testEntities)
                                tLock3.Transaction.Enlist(Action.Create, entity.Id, entity);

                            using (var tLock4 = manager.BeginTransaction())
                            {
                                testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                                testEntities.ForEach(e => e.Id = seed.Increment());

                                foreach (var entity in testEntities)
                                    tLock4.Transaction.Enlist(Action.Create, entity.Id, entity);

                                using (var tLock5 = manager.BeginTransaction())
                                {
                                    testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                                    testEntities.ForEach(e => e.Id = seed.Increment());

                                    foreach (var entity in testEntities)
                                        tLock5.Transaction.Enlist(Action.Create, entity.Id, entity);
                                }
                            }
                        }
                    }

                    tLock1.Transaction.Commit();

                    var sw = new Stopwatch();

                    while (!tLock1.Transaction.IsComplete && sw.ElapsedMilliseconds < 1000)
                        Thread.Sleep(100);

                    sw.Stop();

                    Assert.AreEqual(15, committed);
                    Assert.IsTrue(tLock1.Transaction.IsComplete);
                }
            }
        }

        [Test]
        public void TransactionManagerCommitsChildTransactions()
        {
            var seed = new Seed32();
            var hits = 0;

            using (var manager = new TransactionManager<int, MockClassA>() )
            {
                manager.TransactionCommitted += new TransactionCommit<int, MockClassA>
                    (delegate(ITransaction<int, MockClassA> transaction)
                    {
                        Assert.AreEqual(3, transaction.GetEnlistedActions().Count());
                        hits++;

                        transaction.MarkComplete();
                    });

                var testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                testEntities.ForEach(e => e.Id = seed.Increment());

                using (var tLock1 = manager.BeginTransaction())
                {
                    foreach (var entity in testEntities)
                        tLock1.Transaction.Enlist(Action.Create, entity.Id, entity);

                    using (var tLock2 = manager.BeginTransaction())
                    {
                        testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                        testEntities.ForEach(e => e.Id = seed.Increment());

                        foreach (var entity in testEntities)
                            tLock2.Transaction.Enlist(Action.Create, entity.Id, entity);

                        using (var tLock3 = manager.BeginTransaction())
                        {
                            testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                            testEntities.ForEach(e => e.Id = seed.Increment());

                            foreach (var entity in testEntities)
                                tLock3.Transaction.Enlist(Action.Create, entity.Id, entity);

                            using (var tLock4 = manager.BeginTransaction())
                            {
                                testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                                testEntities.ForEach(e => e.Id = seed.Increment());

                                foreach (var entity in testEntities)
                                    tLock4.Transaction.Enlist(Action.Create, entity.Id, entity);

                                using (var tLock5 = manager.BeginTransaction())
                                {
                                    testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                                    testEntities.ForEach(e => e.Id = seed.Increment());

                                    foreach (var entity in testEntities)
                                        tLock5.Transaction.Enlist(Action.Create, entity.Id, entity);

                                    tLock1.Transaction.Commit();
                                }
                            }
                        }
                    }
                }
            }

            Assert.AreEqual(5, hits);
        }

        [Test]
        public void TransactionManagerCommitAllCommitsAllTransactions()
        {
            var sync = new object();
            var seed = new Seed32();
            var hits = 0;

            using (var manager = new TransactionManager<int, MockClassA>())
            {
                manager.TransactionCommitted += new TransactionCommit<int, MockClassA>
                    (delegate(ITransaction<int, MockClassA> transaction)
                    {
                        Assert.AreEqual(3, transaction.GetEnlistedActions().Count());
                        hits++;

                        transaction.MarkComplete();
                    });

                var transList = new List<TransactionLock<int, MockClassA>>();

                Parallel.For(0, 5, delegate(int i)
                {
                    var testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                    testEntities.ForEach(e => e.Id = seed.Increment());

                    var trans = manager.BeginTransaction();

                    foreach (var entity in testEntities)
                        trans.Transaction.Enlist(Action.Create, entity.Id, entity);

                    if (i == 1)
                    {
                        lock (sync)
                            transList.Add(trans);

                        Assert.AreEqual(3, manager.GetCached().Count());
                    }
                    else
                        trans.Transaction.Commit();

                });

                manager.CommitAll(true);

                Assert.AreEqual(0, manager.GetCached().Count());
            }

            Assert.AreEqual(5, hits);
        }

        [Test]
        public void TransactionManagerRollsbackAll()
        {
            var seed = new Seed32();

            using (var manager = new TransactionManager<int, MockClassA>() )
            {
                manager.TransactionCommitted += new TransactionCommit<int, MockClassA>
                    (delegate(ITransaction<int, MockClassA> transaction)
                    {
                        transaction.MarkComplete();

                        Assert.Fail();
                    });

                var testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                testEntities.ForEach(e => e.Id = seed.Increment());

                using (var tLock1 = manager.BeginTransaction())
                {
                    foreach (var entity in testEntities)
                        tLock1.Transaction.Enlist(Action.Create, entity.Id, entity);

                    using (var tLock2 = manager.BeginTransaction())
                    {
                        testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                        testEntities.ForEach(e => e.Id = seed.Increment());

                        foreach (var entity in testEntities)
                            tLock2.Transaction.Enlist(Action.Create, entity.Id, entity);

                        using (var tLock3 = manager.BeginTransaction())
                        {
                            testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                            testEntities.ForEach(e => e.Id = seed.Increment());

                            foreach (var entity in testEntities)
                                tLock3.Transaction.Enlist(Action.Create, entity.Id, entity);

                            using (var tLock4 = manager.BeginTransaction())
                            {
                                testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                                testEntities.ForEach(e => e.Id = seed.Increment());

                                foreach (var entity in testEntities)
                                    tLock4.Transaction.Enlist(Action.Create, entity.Id, entity);

                                using (var tLock5 = manager.BeginTransaction())
                                {
                                    testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                                    testEntities.ForEach(e => e.Id = seed.Increment());

                                    foreach (var entity in testEntities)
                                        tLock5.Transaction.Enlist(Action.Create, entity.Id, entity);

                                    manager.RollBackAll(true);

                                    while (manager.HasActiveTransactions)
                                        Thread.Sleep(100);

                                    Assert.IsTrue(tLock5.Transaction.IsComplete);
                                }

                                Assert.IsTrue(tLock4.Transaction.IsComplete);
                            }

                            Assert.IsTrue(tLock3.Transaction.IsComplete);
                        }

                        Assert.IsTrue(tLock2.Transaction.IsComplete);
                    }

                    Assert.IsTrue(tLock1.Transaction.IsComplete);
                }
            }
        }

        [Test]
        public void TransactionManagerRollsbackOnDispose()
        {
            var seed = new Seed32();

            using (var manager = new TransactionManager<int, MockClassA>() )
            {
                manager.TransactionCommitted += new TransactionCommit<int, MockClassA>
                    (delegate(ITransaction<int, MockClassA> transaction)
                    {
                        transaction.MarkComplete();

                        Assert.Fail();
                    });

                var testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                testEntities.ForEach(e => e.Id = seed.Increment());

                using (var tLock1 = manager.BeginTransaction())
                {
                    foreach (var entity in testEntities)
                        tLock1.Transaction.Enlist(Action.Create, entity.Id, entity);

                    tLock1.Transaction.Dispose();

                    Assert.IsTrue(tLock1.Transaction.IsComplete);
                }
            }
        }


        [Test]
        public void TransactionManagerRollsbackAmbientTransactions()
        {
            var seed = new Seed32();

            using (var manager = new TransactionManager<int, MockClassA>() )
            {
                manager.TransactionCommitted += new TransactionCommit<int, MockClassA>
                    (delegate(ITransaction<int, MockClassA> transaction)
                    {
                        transaction.MarkComplete();

                        Assert.Fail();
                    });

                var testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                testEntities.ForEach(e => e.Id = seed.Increment());

                using (var tLock1 = manager.GetActiveTransaction(false))
                {
                    foreach (var entity in testEntities)
                        tLock1.Transaction.Enlist(Action.Create, entity.Id, entity);

                    using (var tLock2 = manager.GetActiveTransaction(false))
                    {
                        testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                        testEntities.ForEach(e => e.Id = seed.Increment());

                        foreach (var entity in testEntities)
                            tLock2.Transaction.Enlist(Action.Create, entity.Id, entity);


                        manager.RollBackAll(false);
                    }
                }

                Assert.AreEqual(manager.GetCached().Count(), 0);
            }
        }

        [Test]
        public void TransactionManagerRollsbackAmbientTransactionsOnAllThreads()
        {
            var seed = new Seed32();

            using (var manager = new TransactionManager<int, MockClassA>() )
            {
                manager.TransactionCommitted += new TransactionCommit<int, MockClassA>
                    (delegate(ITransaction<int, MockClassA> transaction)
                    {
                        transaction.MarkComplete();

                        Assert.Fail();
                    });

                var testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                testEntities.ForEach(e => e.Id = seed.Increment());

                using (var tLock1 = manager.GetActiveTransaction(false))
                {
                    foreach (var entity in testEntities)
                        tLock1.Transaction.Enlist(Action.Create, entity.Id, entity);

                    using (var tLock2 = manager.GetActiveTransaction(false))
                    {
                        testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                        testEntities.ForEach(e => e.Id = seed.Increment());

                        foreach (var entity in testEntities)
                            tLock2.Transaction.Enlist(Action.Create, entity.Id, entity);


                        manager.RollBackAll(true);
                    }
                }

                Assert.AreEqual(manager.GetCached().Count(), 0);
            }
        }

        [Test]
        public void TransactionManagerRollsbackChildren()
        {
            var seed = new Seed32();

            using (var manager = new TransactionManager<int, MockClassA>() )
            {
                manager.TransactionCommitted += new TransactionCommit<int, MockClassA>
                    (delegate(ITransaction<int, MockClassA> transaction)
                    {
                        transaction.MarkComplete();

                        Assert.Fail();
                    });

                var testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                testEntities.ForEach(e => e.Id = seed.Increment());

                using (var tLock1 = manager.BeginTransaction())
                {
                    foreach (var entity in testEntities)
                        tLock1.Transaction.Enlist(Action.Create, entity.Id, entity);

                    using (var tLock2 = manager.BeginTransaction())
                    {
                        testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                        testEntities.ForEach(e => e.Id = seed.Increment());

                        foreach (var entity in testEntities)
                            tLock2.Transaction.Enlist(Action.Create, entity.Id, entity);

                        using (var tLock3 = manager.BeginTransaction())
                        {
                            testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                            testEntities.ForEach(e => e.Id = seed.Increment());

                            foreach (var entity in testEntities)
                                tLock3.Transaction.Enlist(Action.Create, entity.Id, entity);

                            using (var tLock4 = manager.BeginTransaction())
                            {
                                testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                                testEntities.ForEach(e => e.Id = seed.Increment());

                                foreach (var entity in testEntities)
                                    tLock4.Transaction.Enlist(Action.Create, entity.Id, entity);

                                using (var tLock5 = manager.BeginTransaction())
                                {
                                    testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                                    testEntities.ForEach(e => e.Id = seed.Increment());

                                    foreach (var entity in testEntities)
                                        tLock5.Transaction.Enlist(Action.Create, entity.Id, entity);

                                    tLock1.Transaction.Rollback();

                                    Assert.IsTrue(tLock5.Transaction.IsComplete);
                                }
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void TransactionManagerCommitsAll()
        {
            var seed = new Seed32();
            var hits = 0;

            using (var manager = new TransactionManager<int, MockClassA>() )
            {
                manager.TransactionCommitted += new TransactionCommit<int, MockClassA>
                    (delegate(ITransaction<int, MockClassA> transaction)
                    {
                        Assert.AreEqual(3, transaction.GetEnlistedActions().Count());
                        hits++;

                        transaction.MarkComplete();
                    });

                var testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                testEntities.ForEach(e => e.Id = seed.Increment());

                using (var tLock1 = manager.BeginTransaction())
                {
                    foreach (var entity in testEntities)
                        tLock1.Transaction.Enlist(Action.Create, entity.Id, entity);

                    using (var tLock2 = manager.BeginTransaction())
                    {
                        testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                        testEntities.ForEach(e => e.Id = seed.Increment());

                        foreach (var entity in testEntities)
                            tLock2.Transaction.Enlist(Action.Create, entity.Id, entity);

                        using (var tLock3 = manager.BeginTransaction())
                        {
                            testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                            testEntities.ForEach(e => e.Id = seed.Increment());

                            foreach (var entity in testEntities)
                                tLock3.Transaction.Enlist(Action.Create, entity.Id, entity);

                            using (var tLock4 = manager.BeginTransaction())
                            {
                                testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                                testEntities.ForEach(e => e.Id = seed.Increment());

                                foreach (var entity in testEntities)
                                    tLock4.Transaction.Enlist(Action.Create, entity.Id, entity);

                                using (var tLock5 = manager.BeginTransaction())
                                {
                                    testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                                    testEntities.ForEach(e => e.Id = seed.Increment());

                                    foreach (var entity in testEntities)
                                        tLock5.Transaction.Enlist(Action.Create, entity.Id, entity);

                                    Assert.AreEqual(3, manager.GetActiveItems().Count);

                                    manager.CommitAll(true);

                                    Assert.IsTrue(tLock5.Transaction.IsComplete);
                                }
                            }
                        }
                    }
                }

                Assert.AreEqual(0, manager.GetActiveItems().Count);
            }
        }

        [Test]
        public void AmbientTransactionCommitsIteself()
        {
            var seed = new Seed32();
            var hits = 0;

            using (var manager = new TransactionManager<int, MockClassA>() )
            {
                manager.TransactionCommitted += new TransactionCommit<int, MockClassA>
                    (delegate(ITransaction<int, MockClassA> transaction)
                    {
                        Assert.AreEqual(3, transaction.GetEnlistedActions().Count());
                        hits++;

                        transaction.MarkComplete();

                        hits = 1;
                    });

                var testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                testEntities.ForEach(e => e.Id = seed.Increment());

                using (var tLock1 = manager.GetActiveTransaction(false))
                {
                    foreach (var entity in testEntities)
                        tLock1.Transaction.Enlist(Action.Create, entity.Id, entity);

                    var sw = new Stopwatch();
                    sw.Start();

                    while (sw.ElapsedMilliseconds < 5500)
                        Thread.Sleep(10);
                }

                Thread.Sleep(500);

                Assert.AreEqual(1, hits);
            }
        }

        [Test]
        public void AmbientTransactionForceCommit()
        {
            var seed = new Seed32();
            var hits = 0;

            using (var manager = new TransactionManager<int, MockClassA>() )
            {
                manager.TransactionCommitted += new TransactionCommit<int, MockClassA>
                    (delegate(ITransaction<int, MockClassA> transaction)
                    {
                        Assert.AreEqual(3, transaction.GetEnlistedActions().Count());
                        hits++;

                        transaction.MarkComplete();

                        hits = 1;
                    });

                var testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                testEntities.ForEach(e => e.Id = seed.Increment());

                using (var tLock1 = manager.GetActiveTransaction(false))
                {
                    foreach (var entity in testEntities)
                        tLock1.Transaction.Enlist(Action.Create, entity.Id, entity);

                    var sw = new Stopwatch();
                    sw.Start();

                    while (sw.ElapsedMilliseconds < 5500)
                        Thread.Sleep(10);

                    manager.CommitAmbientTransactions();

                    tLock1.Transaction.Commit();
                }

                Thread.Sleep(500);

                Assert.AreEqual(1, hits);
            }
        }
    }
}
