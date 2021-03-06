﻿/*
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
using BESSy.Parallelization;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using BESSy.Transactions;
using NUnit.Framework;
using BESSy.Synchronization;

namespace BESSy.Tests.AtomicFileManagerTests
{
    [TestFixture]
    public class AtomicFileManagerRebuildTests : FileTest
    {

        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void AfmRebuildsWithNewSeedSize()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();
            var formatter = new BSONFormatter();
            var core = new FileCore<int, long>() { IdSeed = new Seed32(999), SegmentSeed = new Seed64(), MinimumCoreStride = 512 };
            var addEntities = TestResourceFactory.GetMockClassAObjects(20480).ToList();

            foreach (var entity in addEntities)
                entity.Id = core.IdSeed.Increment();

            var updateEntities = new List<MockClassC>();

            IDictionary<int, long> returnSegments = null;
            IDictionary<int, long> deletedSegments = new Dictionary<int, long>();

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", core))
            {
                afm.Load<int>();

                using (var manager = new TransactionManager<int, MockClassA>
                    (new MockTransactionFactory<int, MockClassA>()
                    , new TransactionSynchronizer<int, MockClassA>()))
                {
                    manager.TransactionCommitted += new TransactionCommit<int, MockClassA>(
                        delegate(ITransaction<int, MockClassA> tranny)
                        {
                            returnSegments = afm.CommitTransaction(tranny, new Dictionary<int, long>());

                            while (afm.FileFlushQueueActive)
                                Thread.Sleep(100);

                            tranny.MarkComplete();

                            afm.SaveCore<int>();
                        });

                    using (var tLock = manager.BeginTransaction())
                    {
                        addEntities.ForEach(delegate(MockClassA entity)
                        {
                            tLock.Transaction.Enlist(Action.Create, entity.Id, entity);
                        });

                        Assert.AreEqual(20480, tLock.Transaction.EnlistCount);

                        tLock.Transaction.Commit();

                        Assert.AreEqual(20480, afm.Length);
                    }
                }
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", core))
            {
                afm.Load<int>();

                Assert.AreEqual(20480, afm.Length);

                using (var manager = new TransactionManager<int, MockClassA>
                    (new MockTransactionFactory<int, MockClassA>()
                    , new TransactionSynchronizer<int, MockClassA>()))
                {
                    manager.TransactionCommitted += new TransactionCommit<int, MockClassA>(
                        delegate(ITransaction<int, MockClassA> tranny)
                        {
                            returnSegments = afm.CommitTransaction(tranny, returnSegments);

                            while (afm.FileFlushQueueActive)
                                Thread.Sleep(100);

                            tranny.MarkComplete();

                            afm.SaveCore<int>();

                            core.MinimumCoreStride = (int)formatter.FormatObjStream(core).Length;
                        });

                    using (var tLock = manager.BeginTransaction())
                    {
                        deletedSegments = returnSegments.Take(9000).ToDictionary(k => k.Key, k => k.Value);

                        foreach (var kvp in deletedSegments)
                            updateEntities.Add(afm.LoadSegmentFrom(kvp.Value) as MockClassC);

                        updateEntities.ForEach(u => tLock.Transaction.Enlist(Action.Delete, u.Id, u));

                        tLock.Transaction.Commit();

                        afm.SaveCore<int>();
                    }

                    Assert.AreEqual(512, afm.Stride);

                    //Deleting items from the database adds those idsToDelete to the segmentSeed'aqn open Ids list, increasing it'aqn size. It'aqn an easy way to check for segmentSeed rebuilding.
                    Assert.Greater(afm.Core.MinimumCoreStride, 23000);
                }
            }
        }

        [Test]
        public void AfmRebuildsWithNewStride()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var formatter = new JSONFormatter();
            var core = new FileCore<string, long>() { IdSeed = new SeedString(), SegmentSeed = new Seed64(), MinimumCoreStride = 512 };

            var entity = TestResourceFactory.CreateRandom() as MockClassC;
            var largeEntity = TestResourceFactory.CreateRandom().WithName(new String('a', 2000)) as MockClassC;

            long seg = 0;

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", core, new JSONFormatter()))
            {
                afm.Load<string>();

                afm.Rebuilt += new Rebuild<MockClassA>(delegate(Guid transactionId, int newStride, long newLength, int newSeedStride)
                {
                    afm.SaveCore<string>();
                });

                seg = AtomicFileManagerHelper.SaveSegment(afm, entity, entity.Name);

                afm.SaveCore();
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", core, new JSONFormatter()))
            {
                afm.Load<string>();

                afm.Rebuilt += new Rebuild<MockClassA>(delegate(Guid transactionId, int newStride, long newLength, int newSeedStride)
                    {
                        afm.SaveCore<string>();
                    });

               seg = AtomicFileManagerHelper.SaveSegment(afm, largeEntity, largeEntity.Name);
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", core, formatter))
            {
                afm.Load<string>();

                Assert.Greater(afm.Stride, 500);
                Assert.AreEqual(512, afm.Core.MinimumCoreStride);

                var obj = afm.LoadSegmentFrom(seg) as MockClassC;

                Assert.IsNotNull(obj);
                Assert.AreEqual(largeEntity.Id, obj.Id);
                Assert.AreEqual(largeEntity.Name, obj.Name);
                Assert.AreEqual(largeEntity.GetSomeCheckSum, obj.GetSomeCheckSum);
                Assert.AreEqual(largeEntity.Location.X, obj.Location.X);
                Assert.AreEqual(largeEntity.Location.Y, obj.Location.Y);
                Assert.AreEqual(largeEntity.Location.Z, obj.Location.Z);
                Assert.AreEqual(largeEntity.Location.W, obj.Location.W);
                Assert.AreEqual(largeEntity.ReferenceCode, obj.ReferenceCode);
                Assert.AreEqual(largeEntity.ReplicationID, obj.ReplicationID);
            }
        }

        [Test]
        public void AfmRebuildsOnFailedTransaction()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();

            var formatter = new BSONFormatter();
            var core = new FileCore<int, long>() { IdSeed = new Seed32(999), SegmentSeed = new Seed64(), MinimumCoreStride = 512 };

            var entity = TestResourceFactory.CreateRandom() as MockClassC;
            var largeEntity = TestResourceFactory.CreateRandom().WithName(new String('a', 2000)) as MockClassC;

            IDictionary<int, long> returnSegments = null;
            using (var fLock = new ManagedFileLock(_testName))
            {
                Cleanup();
                using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", core, formatter))
                {
                    afm.Load<int>();

                    using (var manager = new TransactionManager<int, MockClassA>
                        (new MockTransactionFactory<int, MockClassA>()
                        , new TransactionSynchronizer<int, MockClassA>()))
                    {
                        manager.TransactionCommitted += new TransactionCommit<int, MockClassA>(
                            delegate(ITransaction<int, MockClassA> tranny)
                            {
                                returnSegments = afm.CommitTransaction(tranny, new Dictionary<int, long>());

                                while (afm.FileFlushQueueActive)
                                    Thread.Sleep(100);

                                tranny.MarkComplete();

                                afm.SaveCore<int>();
                            });


                        using (var tLock = manager.BeginTransaction())
                        {
                            tLock.Transaction.Enlist(Action.Create, entity.Id, entity);

                            tLock.Transaction.Commit();
                        }
                    }
                }

                using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", formatter))
                {
                    afm.Load<int>();

                    using (var manager = new TransactionManager<int, MockClassA>
                        (new MockTransactionFactory<int, MockClassA>()
                        , new TransactionSynchronizer<int, MockClassA>()))
                    {
                        manager.TransactionCommitted += new TransactionCommit<int, MockClassA>(
                            delegate(ITransaction<int, MockClassA> tranny)
                            {
                                returnSegments = afm.CommitTransaction(tranny, returnSegments);

                                while (afm.FileFlushQueueActive)
                                    Thread.Sleep(100);

                                tranny.MarkComplete();

                                afm.SaveCore<int>();
                            });

                        using (var tLock = manager.BeginTransaction())
                        {
                            tLock.Transaction.Enlist(Action.Update, largeEntity.Id, largeEntity);

                            tLock.Transaction.Commit();
                        }
                    }
                }

                using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", formatter))
                {
                    afm.Load<int>();

                    Assert.Greater(afm.Stride, 2200);
                    Assert.AreEqual(1024, afm.CorePosition);

                    var obj = afm.LoadSegmentFrom(returnSegments.First().Value) as MockClassC;

                    Assert.IsNotNull(obj);
                    Assert.AreEqual(largeEntity.Id, obj.Id);
                    Assert.AreEqual(largeEntity.Name, obj.Name);
                    Assert.AreEqual(largeEntity.GetSomeCheckSum, obj.GetSomeCheckSum);
                    Assert.AreEqual(largeEntity.Location.X, obj.Location.X);
                    Assert.AreEqual(largeEntity.Location.Y, obj.Location.Y);
                    Assert.AreEqual(largeEntity.Location.Z, obj.Location.Z);
                    Assert.AreEqual(largeEntity.Location.W, obj.Location.W);
                    Assert.AreEqual(largeEntity.ReferenceCode, obj.ReferenceCode);
                    Assert.AreEqual(largeEntity.ReplicationID, obj.ReplicationID);
                }
            }
        }

        [Test]
        public void AfmReorganizesWithCorrectNumberOfRows()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();

            var formatter = new BSONFormatter();
            var core = new FileCore<int, long>() { IdSeed = new Seed32(999), SegmentSeed = new Seed64(), MinimumCoreStride = 512, InitialDbSize = 10240 };

            var addEntities = TestResourceFactory.GetMockClassAObjects(10000).ToList();

            foreach (var entity in addEntities)
                entity.Id = core.IdSeed.Increment();

            addEntities.Reverse();

            using (var fLock = new ManagedFileLock(_testName))
            {
                Cleanup();

                using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", 4096, 20000, 4096, core, formatter, new RowSynchronizer<long>(new BinConverter64())))
                {
                    afm.Load<int>();

                    afm.Rebuilt += new Rebuild<MockClassA>(delegate(Guid transactionId, int newStride, long newLength, int newSeedStride)
                    {
                        afm.SaveCore<int>();
                    });

                    AtomicFileManagerHelper.SaveSegments(afm, addEntities.ToDictionary(e => e.Id));
                }

                addEntities.Reverse();

                var seg = 1;

                using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", 4096, 20000, 4096, formatter, new RowSynchronizer<long>(new BinConverter64())))
                {
                    afm.Load<int>();

                    Assert.AreNotEqual(1, afm.LoadSegmentFrom(seg).Id);

                    afm.Reorganized += new Reorganized<MockClassA>(delegate(int recordsWritten)
                    {
                        afm.SaveCore<int>();
                    });

                    afm.Reorganize<int>(new BinConverter32(), j => j.Value<int>("Id"));
                }

                using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", 4096, 20000, 4096, core, formatter, new RowSynchronizer<long>(new BinConverter64())))
                {
                    afm.Load<int>();

                    foreach (var entity in addEntities)
                    {
                        Assert.AreEqual(entity.Id, afm.LoadSegmentFrom(seg).Id, "Id {0} was not found in the right order, Id {1} was found instead.", entity.Id, afm.LoadSegmentFrom(seg).Id);
                        seg++;
                    }
                }
            }
        }
    }
}
