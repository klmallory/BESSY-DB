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

            using (var manager = new TransactionManager<int, MockClassA>())
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

            using (var manager = new TransactionManager<int, MockClassA>())
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

                Parallel.For(0, 5, delegate(int i)
                {
                    var testEntities = TestResourceFactory.GetMockClassAObjects(3).ToList();
                    testEntities.ForEach(e => e.Id = seed.Increment());

                    using (var trans = manager.BeginTransaction())
                    {
                        foreach (var entity in testEntities)
                            trans.Transaction.Enlist(Action.Create, entity.Id, entity);
                    }
                });

                manager.CommitAll(true);
            }

            Assert.AreEqual(5, hits);
        }


        [Test]
        public void TransactionManagerRollsbackAll()
        {
            var seed = new Seed32();

            using (var manager = new TransactionManager<int, MockClassA>())
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

                                    Assert.AreEqual(0, tLock5.Transaction.GetEnlistedItems().Count());
                                }

                                Assert.AreEqual(0, tLock4.Transaction.GetEnlistedItems().Count());
                            }

                            Assert.AreEqual(0, tLock3.Transaction.GetEnlistedItems().Count());
                        }

                        Assert.AreEqual(0, tLock2.Transaction.GetEnlistedItems().Count());
                    }

                    Assert.AreEqual(0, tLock1.Transaction.GetEnlistedItems().Count());
                }
            }
        }

        [Test]
        public void TransactionManagerRollsbackChildren()
        {
            var seed = new Seed32();

            using (var manager = new TransactionManager<int, MockClassA>())
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

                                    Assert.AreEqual(0, tLock5.Transaction.GetEnlistedItems().Count());
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

            using (var manager = new TransactionManager<int, MockClassA>())
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

                                    manager.CommitAll(true);

                                    Assert.AreEqual(0, tLock5.Transaction.GetEnlistedActions().Count());
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
