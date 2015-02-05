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
using BESSy.Json.Linq;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using BESSy.Transactions;
using NUnit.Framework;

namespace BESSy.Tests.AtomicFileManagerTests
{
    [TestFixture]
    public class AtomicFileManagerCRUDTests : FileTest
    {
        [Test]
        public void AfmInitializes()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var formatter = TestResourceFactory.CreateJsonFormatterWithoutArrayFormatting();
            var core = new FileCore<int, long>() { IdSeed = new Seed32(0), SegmentSeed = new Seed64(), MinimumCoreStride = 512 };

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", new FileCore<int, long>(), formatter))
            {
                afm.Load<int>();
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", formatter))
            {
                afm.Load<int>();
                Assert.AreEqual(512, afm.Stride);
                Assert.AreEqual(10240, afm.CorePosition);
            }
        }

        [Test]
        public void AfmSavesNewObjects()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var formatter = TestResourceFactory.CreateJsonFormatterWithoutArrayFormatting();
            var core = new FileCore<int, long>() { IdSeed = new Seed32(999), SegmentSeed = new Seed64(), MinimumCoreStride = 512 };

            var entity = TestResourceFactory.CreateRandom() as MockClassC;
            long seg = -1;

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", core))
            {
                afm.Load<int>();

                entity.Id = core.IdSeed.Increment();

                afm.Rebuilt += new Rebuild<MockClassA>(delegate(Guid transactionId, int newStride, long newLength, int newSeedStride)
                {
                    afm.SaveCore<int>();
                });

                seg = AtomicFileManagerHelper.SaveSegment(afm, entity, entity.Id);

                afm.SaveCore<int>();
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database"))
            {
                afm.Load<int>();

                Assert.AreEqual(512, afm.Stride);
                Assert.AreEqual(1024, afm.CorePosition);

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
        public void AfmLoadsJObjectsObjects()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var formatter = TestResourceFactory.CreateJsonFormatterWithoutArrayFormatting();
            var core = new FileCore<int, long>() { IdSeed = new Seed32(999), SegmentSeed = new Seed64(), MinimumCoreStride = 512 };

            var entity = TestResourceFactory.CreateRandom() as MockClassC;
            long seg = -1;

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", core))
            {
                afm.Load<int>();

                entity.Id = core.IdSeed.Increment();

                afm.Rebuilt += new Rebuild<MockClassA>(delegate(Guid transactionId, int newStride, long newLength, int newSeedStride)
                {
                    afm.SaveCore<int>();
                });

                seg = AtomicFileManagerHelper.SaveSegment(afm, entity, entity.Id);

                afm.SaveCore<int>();
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database"))
            {
                afm.Load<int>();

                Assert.AreEqual(512, afm.Stride);
                Assert.AreEqual(1024, afm.CorePosition);

                var obj = afm.LoadJObjectFrom(seg);

                Assert.IsNotNull(obj);
                Assert.AreEqual(entity.Id, obj.Value<int>("Id"));
                Assert.AreEqual(entity.Name, obj.Value<string>("Name"));
                Assert.AreEqual(entity.GetSomeCheckSum, obj.GetAsTypedArray<double>("GetSomeCheckSum"));
                Assert.AreEqual(entity.Location.X, obj.SelectToken("Location.X").Value<float>());
                Assert.AreEqual(entity.Location.Y, obj.SelectToken("Location.Y").Value<float>());
                Assert.AreEqual(entity.Location.Z, obj.SelectToken("Location.Z").Value<float>());
                Assert.AreEqual(entity.Location.W, obj.SelectToken("Location.W").Value<float>());
                Assert.AreEqual(entity.ReferenceCode, obj.Value<string>("ReferenceCode"));
                Assert.AreEqual(entity.ReplicationID, obj.Value<Guid>("ReplicationID"));
            }
        }

        void afm_Rebuilt(Guid transactionId, int newStride, int newLength, int newSeedStride)
        {
            throw new NotImplementedException();
        }

        [Test]
        public void AfmUpdatesAndDeletesExistingObjects()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var formatter = TestResourceFactory.CreateJsonFormatterWithoutArrayFormatting();
            var core = new FileCore<int, long>() { IdSeed = new Seed32(999), SegmentSeed = new Seed64(), MinimumCoreStride = 512 };

          
            var entity1 = TestResourceFactory.CreateRandom() as MockClassC;
            var entity2 = TestResourceFactory.CreateRandom() as MockClassC;
            long seg = -1;

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", core, formatter))
            {
                afm.Load<int>();

               seg = AtomicFileManagerHelper.SaveSegment(afm, entity1, entity1.Id);
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", formatter))
            {
                afm.Load<int>();

                AtomicFileManagerHelper.SaveSegment(afm, entity2, entity2.Id, seg);
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", formatter))
            {
                afm.Load<int>();

                Assert.Less(512, afm.Stride);
                Assert.Greater(1024, afm.CorePosition);

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

                AtomicFileManagerHelper.DeleteSegment(afm, obj.Id, seg);

                obj = afm.LoadSegmentFrom(seg) as MockClassC;

                Assert.IsNull(obj);
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", formatter))
            {
                afm.Load<int>();

                var obj = afm.LoadSegmentFrom(seg) as MockClassC;

                Assert.IsNull(obj);
            }
        }

        [Test]
        public void AfmCommitsSimpleTransaction()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var formatter = TestResourceFactory.CreateJsonFormatterWithoutArrayFormatting();
            var core = new FileCore<int, long>() { IdSeed = new Seed32(999), SegmentSeed = new Seed64(), Stride = 512 };

            var entity1 = TestResourceFactory.CreateRandom() as MockClassC;
            
            MockClassC entity2;

            long seg = -1;

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", core, formatter))
            {
                afm.Load<int>();
                entity1.Id = 1001;
                seg = AtomicFileManagerHelper.SaveSegment(afm, entity1, entity1.Id);
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", formatter))
            {
                afm.Load<int>();

                entity2 = entity1.WithName("No Name Joe") as MockClassC;

                AtomicFileManagerHelper.SaveSegment(afm, entity2, entity2.Id, seg);

            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", formatter))
            {
                afm.Load<int>();

                Assert.Less(512, afm.Stride);
                Assert.AreEqual(10240, afm.CorePosition);

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
            }
        }

        [Test]
        public void AfmCommitsComplexTransaction()
        {

            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var formatter = new BSONFormatter();
            var core = new FileCore<int, long>() { IdSeed = new Seed32(999), SegmentSeed = new Seed64(), Stride = 32 };

            var addEntities = TestResourceFactory.GetMockClassAObjects(25).ToList();
            
            foreach (var entity in addEntities)
                entity.Id = core.IdSeed.Increment();

            var updateEntities = new List<MockClassC>();

            IDictionary<int, long> returnSegments = null;

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", core, formatter))
            {
                afm.Load<int>();

                using (var manager = new TransactionManager<int, MockClassA>
                     (new MockTransactionFactory<int, MockClassA>()
                     , new TransactionSynchronizer<int, MockClassA>()))
                {
                    afm.Rebuilt += new Rebuild<MockClassA>(delegate(Guid transactionId, int newStride, long newLength, int newSeedStride)
                    {
                        var c = (IFileCore<int, long>)afm.Core;
                        c.Stride = newStride;
                        c.MinimumCoreStride = newSeedStride;

                        afm.SaveCore<int>();
                    });

                    manager.TransactionCommitted += new TransactionCommit<int, MockClassA>(
                        delegate(ITransaction<int, MockClassA> tranny)
                        {
                            returnSegments = afm.CommitTransaction(tranny, new Dictionary<int, long>());
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

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", formatter))
            {
                afm.Load<int>();

                Assert.AreEqual(25, afm.Length);

                using (var manager = new TransactionManager<int, MockClassA>
                     (new MockTransactionFactory<int, MockClassA>()
                     , new TransactionSynchronizer<int, MockClassA>()))
                {
                    afm.Rebuilt += new Rebuild<MockClassA>(delegate(Guid transactionId, int newStride, long newLength, int newSeedStride)
                    {
                        var c = (IFileCore<int, long>)afm.Core;
                        c.Stride = newStride;
                        c.MinimumCoreStride = newSeedStride;

                        afm.SaveCore<int>();
                    });

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
                        insert.Id = core.IdSeed.Increment();

                        updateEntities.Add(insert);
                        tLock.Transaction.Enlist(Action.Create, insert.Id, insert);

                        tLock.Transaction.Commit();

                        Assert.AreEqual(26, afm.Length);
                    }
                }
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", formatter))
            {
                afm.Load<int>();

                Assert.LessOrEqual(461, afm.Stride);
                Assert.AreEqual(10240, afm.CorePosition);

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
