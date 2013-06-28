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
using BESSy.Extensions;
using BESSy.Files;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using BESSy.Transactions;
using NUnit.Framework;

namespace BESSy.Tests.AtomicFileManagerTests
{
    [TestFixture]
    public class AtomicFileManagerCRUDTests
    {
        string _testName;
        ISeed<int> _seed;
        IQueryableFormatter _formatter;

        [SetUp]
        public void Setup()
        {
            _seed = new Seed32(999);
            _formatter = TestResourceFactory.CreateJsonFormatterWithoutArrayFormatting();
        }

        void Cleanup()
        {
            var fi = new FileInfo(_testName + ".database");
            if (fi.Exists)
                while (fi.IsFileLocked())
                    Thread.Sleep(100);

            fi.Delete();
        }

        [Test]
        public void AfmInitializes()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", _formatter))
            {
                afm.Load();
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", _formatter))
            {
                afm.Load();
                Assert.AreEqual(512, afm.Stride);
                Assert.AreEqual(10240, afm.SeedPosition);
            }
        }

        [Test]
        public void AfmSavesNewObjects()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var entity = TestResourceFactory.CreateRandom() as MockClassC;
            int seg = 0;

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", _formatter))
            {
                afm.Load();

                seg = afm.SaveSegment(entity);
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", _formatter))
            {
                afm.Load();

                Assert.AreEqual(512, afm.Stride);
                Assert.AreEqual(10240, afm.SeedPosition);

                var obj = afm.LoadSegmentFrom(seg) as MockClassC;

                Assert.IsNotNull(obj);
                Assert.AreEqual(entity.Id, obj.Id);
                Assert.AreEqual(entity.Name, obj.Name);
                Assert.AreEqual(entity.GetSomeCheckSum, obj.GetSomeCheckSum);
                Assert.AreEqual(entity.Location.X, obj.Location.X);
                Assert.AreEqual(entity.Location.Y, obj.Location.Y);
                Assert.AreEqual(entity.Location.Z, obj.Location.Z);
                Assert.AreEqual(entity.Location.W, obj.Location.W);
                Assert.AreEqual(entity.ReferenceCode, obj.ReferenceCode);
                Assert.AreEqual(entity.ReplicationID, obj.ReplicationID);
            }
        }

        [Test]
        public void AfmUpdatesAndDeletesExistingObjects()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var entity1 = TestResourceFactory.CreateRandom() as MockClassC;
            var entity2 = TestResourceFactory.CreateRandom() as MockClassC;
            int seg = 0;

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", _formatter))
            {
                afm.Load();

                seg = afm.SaveSegment(entity1);
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", _formatter))
            {
                afm.Load();

                afm.SaveSegment(entity2, seg);
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", _formatter))
            {
                afm.Load();

                Assert.AreEqual(512, afm.Stride);
                Assert.AreEqual(10240, afm.SeedPosition);

                var obj = afm.LoadSegmentFrom(seg) as MockClassC;

                Assert.IsNotNull(obj);
                Assert.AreEqual(entity2.Id, obj.Id);
                Assert.AreEqual(entity2.Name, obj.Name);
                Assert.AreEqual(entity2.GetSomeCheckSum, obj.GetSomeCheckSum);
                Assert.AreEqual(entity2.Location.X, obj.Location.X);
                Assert.AreEqual(entity2.Location.Y, obj.Location.Y);
                Assert.AreEqual(entity2.Location.Z, obj.Location.Z);
                Assert.AreEqual(entity2.Location.W, obj.Location.W);
                Assert.AreEqual(entity2.ReferenceCode, obj.ReferenceCode);
                Assert.AreEqual(entity2.ReplicationID, obj.ReplicationID);

                afm.DeleteSegment(seg);

                obj = afm.LoadSegmentFrom(seg) as MockClassC;

                Assert.IsNull(obj);
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", _formatter))
            {
                afm.Load();

                var obj = afm.LoadSegmentFrom(seg) as MockClassC;

                Assert.IsNull(obj);
            }
        }

        [Test]
        public void AfmCommitsSimpleTransaction()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var entity1 = TestResourceFactory.CreateRandom() as MockClassC;
            entity1.Id = 1001;
            MockClassC entity2;

            IDictionary<int, int> returnSegments = null;

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", _formatter))
            {
                afm.Load();

                using (var manager = new TransactionManager<int, MockClassA>
                     (new MockTransactionFactory<int, MockClassA>()
                     , new TransactionSynchronizer<int, MockClassA>()))
                {
                    manager.TransactionCommitted += new TransactionCommit<int, MockClassA>(
                        delegate(ITransaction<int, MockClassA> tranny)
                        {
                            returnSegments = afm.CommitTransaction(tranny, new Dictionary<int, int>());

                            tranny.MarkComplete();
                        });

                    using (var tLock = manager.BeginTransaction())
                    {
                        tLock.Transaction.Enlist(Action.Create, entity1.Id, entity1);

                        tLock.Transaction.Commit();
                    }
                }
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", _formatter))
            {
                afm.Load();

                using (var manager = new TransactionManager<int, MockClassA>
                     (new MockTransactionFactory<int, MockClassA>()
                     , new TransactionSynchronizer<int, MockClassA>()))
                {
                    manager.TransactionCommitted += new TransactionCommit<int, MockClassA>(
                        delegate(ITransaction<int, MockClassA> tranny)
                        {
                            returnSegments = afm.CommitTransaction(tranny, returnSegments);

                            tranny.MarkComplete();
                        });

                    using (var tLock = manager.BeginTransaction())
                    {
                        var entity = afm.LoadSegmentFrom(returnSegments.First().Value);

                        entity2 = entity.WithName("No Name Joe") as MockClassC;

                        tLock.Transaction.Enlist(Action.Update, entity2.Id, entity2);

                        tLock.Transaction.Commit();
                    }
                }
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", _formatter))
            {
                afm.Load();

                Assert.AreEqual(512, afm.Stride);
                Assert.AreEqual(10240, afm.SeedPosition);

                var obj = afm.LoadSegmentFrom(returnSegments.First().Value) as MockClassC;

                Assert.IsNotNull(obj);
                Assert.AreEqual(entity2.Id, obj.Id);
                Assert.AreEqual(entity2.Name, obj.Name);
                Assert.AreEqual(entity2.GetSomeCheckSum, obj.GetSomeCheckSum);
                Assert.AreEqual(entity2.Location.X, obj.Location.X);
                Assert.AreEqual(entity2.Location.Y, obj.Location.Y);
                Assert.AreEqual(entity2.Location.Z, obj.Location.Z);
                Assert.AreEqual(entity2.Location.W, obj.Location.W);
                Assert.AreEqual(entity2.ReferenceCode, obj.ReferenceCode);
                Assert.AreEqual(entity2.ReplicationID, obj.ReplicationID);
            }
        }

        [Test]
        public void AfmCommitsComplexTransaction()
        {

            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var addEntities = TestResourceFactory.GetMockClassAObjects(25).ToList();
            
            foreach (var entity in addEntities)
                entity.Id = _seed.Increment();

            var updateEntities = new List<MockClassC>();

            IDictionary<int, int> returnSegments = null;

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", _formatter))
            {
                afm.Load();

                using (var manager = new TransactionManager<int, MockClassA>
                     (new MockTransactionFactory<int, MockClassA>()
                     , new TransactionSynchronizer<int, MockClassA>()))
                {
                    manager.TransactionCommitted += new TransactionCommit<int, MockClassA>(
                        delegate(ITransaction<int, MockClassA> tranny)
                        {
                            returnSegments = afm.CommitTransaction(tranny, new Dictionary<int, int>());
                            tranny.MarkComplete();
                        });

                    using (var tLock = manager.BeginTransaction())
                    {
                        addEntities.ForEach(delegate(MockClassA entity)
                        {
                            tLock.Transaction.Enlist(Action.Create, entity.Id, entity);
                        });

                        tLock.Transaction.Commit();

                        Assert.AreEqual(25, afm.Length);
                    }
                }
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", _formatter))
            {
                afm.Load();

                Assert.AreEqual(25, afm.Length);

                using (var manager = new TransactionManager<int, MockClassA>
                     (new MockTransactionFactory<int, MockClassA>()
                     , new TransactionSynchronizer<int, MockClassA>()))
                {
                    manager.TransactionCommitted += new TransactionCommit<int, MockClassA>(
                        delegate(ITransaction<int, MockClassA> tranny)
                        {
                            returnSegments = afm.CommitTransaction(tranny, returnSegments);

                            tranny.MarkComplete();
                        });

                    using (var tLock = manager.BeginTransaction())
                    {
                        foreach (var kvp in returnSegments)
                            updateEntities.Add(afm.LoadSegmentFrom(kvp.Value) as MockClassC);

                        updateEntities.ForEach(u => u.Name = (u.Location.X + u.Location.Y).ToString() + u.Name);

                        updateEntities.ForEach(u => tLock.Transaction.Enlist(Action.Update, u.Id, u));

                        var insert = TestResourceFactory.CreateRandom().WithName("numb nugget") as MockClassC;
                        insert.Id = _seed.Increment();

                        updateEntities.Add(insert);
                        tLock.Transaction.Enlist(Action.Create, insert.Id, insert);

                        tLock.Transaction.Commit();

                        Assert.AreEqual(26, afm.Length);
                    }
                }
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", _formatter))
            {
                afm.Load();

                Assert.AreEqual(512, afm.Stride);
                Assert.AreEqual(10240, afm.SeedPosition);

                foreach (var entity in updateEntities)
                {
                    var obj = afm.LoadSegmentFrom(returnSegments[entity.Id]) as MockClassC;

                    Assert.IsNotNull(obj);
                    Assert.AreEqual(entity.Id, obj.Id);
                    Assert.AreEqual(entity.Name, obj.Name);
                    Assert.AreEqual(entity.GetSomeCheckSum, obj.GetSomeCheckSum);
                    Assert.AreEqual(entity.Location.X, obj.Location.X);
                    Assert.AreEqual(entity.Location.Y, obj.Location.Y);
                    Assert.AreEqual(entity.Location.Z, obj.Location.Z);
                    Assert.AreEqual(entity.Location.W, obj.Location.W);
                    Assert.AreEqual(entity.ReferenceCode, obj.ReferenceCode);
                    Assert.AreEqual(entity.ReplicationID, obj.ReplicationID);
                }
            }

        }
    }
}
