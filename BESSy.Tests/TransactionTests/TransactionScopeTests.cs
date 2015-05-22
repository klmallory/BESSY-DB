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
using System.Transactions;

namespace BESSy.Tests.TransactionTests
{
    [TestFixture]
    public class TransactionScopeTests : FileTest
    {
        [Test]
        public void SinglePhaseScopeCommitsTest()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();

            var objects = TestResourceFactory.GetMockClassAObjects(12);

            ITransaction trans = null;

            using (var fLock = new ManagedFileLock(_testName))
            {
                Cleanup();
                using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>(), new BinConverter32(), new BSONFormatter(),
                    new TransactionManager<int, MockClassA>() { DistributedScopeEnlistment = TransactionEnlistmentType.SinglePhasePromotable }))
                {
                    db.Load();

                    using (var scope = new System.Transactions.TransactionScope(TransactionScopeOption.RequiresNew))
                    {
                        trans = db.BeginTransaction();

                        foreach (var obj in objects)
                            obj.Id = db.AddOrUpdate(obj, 0);

                        var update = db.Fetch(3);

                        update.Name = "Updated " + update.Id;

                        db.AddOrUpdate(update, update.Id);

                        db.Delete(objects.Last().Id);

                        scope.Complete();
                    }

                    while (!trans.IsComplete)
                        Thread.Sleep(100);
                }

                using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>(), new BinConverter32(), new BSONFormatter(),
                    new TransactionManager<int, MockClassA>() { DistributedScopeEnlistment = TransactionEnlistmentType.SinglePhasePromotable }))
                {
                    db.Load();

                    using (var scope = new System.Transactions.TransactionScope(TransactionScopeOption.RequiresNew))
                    {
                        trans = db.BeginTransaction();

                        db.Update<MockClassA>(u => !u.Value<string>("Name").Contains("Updated"), m => m.Name = "batch " + m.Id);

                        var old = db.Select(s => s.Value<string>("Name").Contains("Updated"));

                        Assert.AreEqual(1, old.Count);
                        Assert.AreEqual("Updated 3", old.Single().Name);

                        var updates = db.SelectFirst(s => s.Value<string>("Name").Contains("batch"), 11);

                        Assert.AreEqual(10, updates.Count);
                        Assert.AreEqual(1, updates.First().Id);
                        Assert.AreEqual(11, updates.Last().Id);

                        scope.Complete();
                    }

                    while (!trans.IsComplete)
                        Thread.Sleep(100);
                }
            }
        }

        [Test]
        public void SinglePhaseScopeRollsBackTest()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();

            var objects = TestResourceFactory.GetMockClassAObjects(12);

            ITransaction trans = null;

            using (var fLock = new ManagedFileLock(_testName))
            {
                Cleanup();

                using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>(), new BinConverter32(), new BSONFormatter(),
                    new TransactionManager<int, MockClassA>() { DistributedScopeEnlistment = TransactionEnlistmentType.SinglePhasePromotable }))
                {
                    db.Load();

                    using (var scope = new System.Transactions.TransactionScope(TransactionScopeOption.RequiresNew))
                    {
                        trans = db.BeginTransaction();

                        foreach (var obj in objects)
                            obj.Id = db.AddOrUpdate(obj, 0);

                        var update = db.Fetch(3);

                        update.Name = "Updated " + update.Id;

                        db.AddOrUpdate(update, update.Id);

                        db.Delete(objects.Last().Id);
                    }

                    while (!trans.IsComplete)
                        Thread.Sleep(100);
                }

                using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>(), new BinConverter32(), new BSONFormatter(),
                    new TransactionManager<int, MockClassA>() { DistributedScopeEnlistment = TransactionEnlistmentType.SinglePhasePromotable }))
                {
                    db.Load();

                    Assert.AreEqual(0, db.Length);
                }

            }
        }

        [Test]
        public void FullEnlistmentScopeCommitsTest()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();

            var objects = TestResourceFactory.GetMockClassAObjects(12);

            ITransaction trans = null;

            using (var fLock = new ManagedFileLock(_testName))
            {
                Cleanup();

                using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>(), new BinConverter32(), new BSONFormatter(),
                    new TransactionManager<int, MockClassA>() { DistributedScopeEnlistment = TransactionEnlistmentType.FullEnlistmentNotification }))
                {
                    db.Load();

                    using (var scope = new System.Transactions.TransactionScope(TransactionScopeOption.RequiresNew))
                    {
                        trans = db.BeginTransaction();

                        foreach (var obj in objects)
                            obj.Id = db.AddOrUpdate(obj, 0);

                        var update = db.Fetch(3);

                        update.Name = "Updated " + update.Id;

                        db.AddOrUpdate(update, update.Id);

                        db.Delete(objects.Last().Id);

                        scope.Complete();
                    }

                    while (!trans.IsComplete)
                        Thread.Sleep(100);
                }

                using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>(), new BinConverter32(), new BSONFormatter(),
                    new TransactionManager<int, MockClassA>() { DistributedScopeEnlistment = TransactionEnlistmentType.FullEnlistmentNotification }))
                {
                    db.Load();

                    using (var scope = new System.Transactions.TransactionScope(TransactionScopeOption.RequiresNew))
                    {
                        trans = db.BeginTransaction();

                        db.Update<MockClassA>(u => !u.Value<string>("Name").Contains("Updated"), m => m.Name = "batch " + m.Id);

                        var old = db.Select(s => s.Value<string>("Name").Contains("Updated"));

                        Assert.AreEqual(1, old.Count);
                        Assert.AreEqual("Updated 3", old.Single().Name);

                        var updates = db.SelectFirst(s => s.Value<string>("Name").Contains("batch"), 11);

                        Assert.AreEqual(10, updates.Count);
                        Assert.AreEqual(1, updates.First().Id);
                        Assert.AreEqual(11, updates.Last().Id);

                        scope.Complete();
                    }

                    while (!trans.IsComplete)
                        Thread.Sleep(100);
                }
            }
        }

        [Test]
        public void FullEnlistmentScopeRollsBackTest()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();

            var objects = TestResourceFactory.GetMockClassAObjects(12);

            ITransaction trans = null;

            using (var fLock = new ManagedFileLock(_testName))
            {
                Cleanup();

                using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>(), new BinConverter32(), new BSONFormatter(),
                    new TransactionManager<int, MockClassA>() { DistributedScopeEnlistment = TransactionEnlistmentType.FullEnlistmentNotification }))
                {
                    db.Load();

                    using (var scope = new System.Transactions.TransactionScope(TransactionScopeOption.RequiresNew))
                    {
                        using (trans = db.BeginTransaction())
                        {

                            foreach (var obj in objects)
                                obj.Id = db.AddOrUpdate(obj, 0);

                            var update = db.Fetch(3);

                            update.Name = "Updated " + update.Id;

                            db.AddOrUpdate(update, update.Id);

                            db.Delete(objects.Last().Id);
                        }
                    }
                }

                using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>(), new BinConverter32(), new BSONFormatter(),
                    new TransactionManager<int, MockClassA>() { DistributedScopeEnlistment = TransactionEnlistmentType.FullEnlistmentNotification }))
                {
                    db.Load();

                    Assert.AreEqual(0, db.Length);
                }
            }
        }

        [Test]
        public void FullEnlistmentScopeRollsBackOnExceptionTest()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();

            var objects = TestResourceFactory.GetMockClassAObjects(12);

            ITransaction trans = null;

            using (var fLock = new ManagedFileLock(_testName))
            {
                Cleanup();

                using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>(), new BinConverter32(), new BSONFormatter(),
                    new TransactionManager<int, MockClassA>() { DistributedScopeEnlistment = TransactionEnlistmentType.FullEnlistmentNotification }))
                {
                    db.Load();

                    try
                    {
                        using (var scope = new System.Transactions.TransactionScope(TransactionScopeOption.RequiresNew))
                        {
                            using (trans = db.BeginTransaction())
                            {

                                foreach (var obj in objects)
                                    obj.Id = db.AddOrUpdate(obj, 0);

                                var update = db.Fetch(3);

                                update.Name = "Updated " + update.Id;

                                db.AddOrUpdate(update, update.Id);

                                db.Delete(objects.Last().Id);

                                throw new Exception();
                            }
                        }
                    }
                    catch (Exception) { }
                }

                using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>(), new BinConverter32(), new BSONFormatter(),
                    new TransactionManager<int, MockClassA>() { DistributedScopeEnlistment = TransactionEnlistmentType.FullEnlistmentNotification }))
                {
                    db.Load();

                    Assert.AreEqual(0, db.Length);
                }
            }
        }
    }
}