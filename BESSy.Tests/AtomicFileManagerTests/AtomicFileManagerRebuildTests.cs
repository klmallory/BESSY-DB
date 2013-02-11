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
using BESSy.Parallelization;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using BESSy.Transactions;
using NUnit.Framework;

namespace BESSy.Tests.AtomicFileManagerTests
{
    [TestFixture]
    public class AtomicFileManagerRebuildTests
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
        public void AfmRebuildsWithNewSeedSize()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var addEntities = TestResourceFactory.GetMockClassAObjects(10000).ToList();

            foreach (var entity in addEntities)
                entity.Id = _seed.Increment();

            var updateEntities = new List<MockClassC>();

            IDictionary<int, int> returnSegments = null;
            IDictionary<int, int> deletedSegments = new Dictionary<int, int>();

            using (var afm = new AtomicFileManager<int, MockClassA>(_testName + ".database", _formatter))
            {
                afm.Load();

                using (var manager = new TransactionManager<int, MockClassA>(new MockTransactionFactory<int, MockClassA>()))
                {
                    manager.TransactionCommitted += new TransactionCommit<int, MockClassA>(
                        delegate(ITransaction<int, MockClassA> tranny, IList<ITransaction<int, MockClassA>> childTransactions)
                        {
                            returnSegments = afm.CommitTransaction(tranny, new Dictionary<int, int>());
                        });

                    using (var transaction = manager.BeginTransaction() as MockTransaction<int, MockClassA>)
                    {
                        addEntities.ForEach(delegate(MockClassA entity)
                        {
                            transaction.Enlist(Action.Create, entity.Id, entity);
                        });

                        transaction.Commit();

                        Assert.AreEqual(10000, afm.Length);
                    }
                }
            }

            using (var afm = new AtomicFileManager<int, MockClassA>(_testName + ".database", _formatter))
            {
                afm.Load();

                Assert.AreEqual(10000, afm.Length);

                using (var manager = new TransactionManager<int, MockClassA>(new MockTransactionFactory<int, MockClassA>()))
                {
                    afm.CommitFailed += new CommitFailed<int, MockClassA>(delegate(CommitFailureInfo<int, MockClassA> commitInfo)
                    {
                        afm.Rebuild(commitInfo.NewRowSize, commitInfo.NewDatabaseSize, -1);
                    });

                    manager.TransactionCommitted += new TransactionCommit<int, MockClassA>(
                        delegate(ITransaction<int, MockClassA> tranny, IList<ITransaction<int, MockClassA>> childTransactions)
                        {
                            returnSegments = afm.CommitTransaction(tranny, returnSegments);
                        });

                    using (var transaction = manager.BeginTransaction() as MockTransaction<int, MockClassA>)
                    {
                        deletedSegments = returnSegments.Take(5000).ToDictionary(k => k.Key, k => k.Value);

                        foreach (var kvp in deletedSegments)
                            updateEntities.Add(afm.LoadSegmentFrom(kvp.Value) as MockClassC);

                        updateEntities.ForEach(u => transaction.Enlist(Action.Delete, u.Id, u));

                        transaction.Commit();
                    }
                }
            }



            using (var afm = new AtomicFileManager<int, MockClassA>(_testName + ".database", _formatter))
            {
                afm.Load();

                Assert.AreEqual(512, afm.Stride);

                //Deleting items from the database adds those ids to the seed's open Ids list, increasing it's size. It's an easy way to check for seed rebuilding.
                Assert.Greater(afm.SeedPosition, 23000);
            }
        }

        [Test]
        public void AfmRebuildsWithNewStride()
        {

            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var entity = TestResourceFactory.CreateRandom() as MockClassC;
            var largeEntity = TestResourceFactory.CreateRandom().WithName(new String('a', 2000)) as MockClassC;

            int seg = 0;

            using (var afm = new AtomicFileManager<int, MockClassA>(_testName + ".database", _formatter))
            {
                afm.Load();

                seg = afm.SaveSegment(entity);
            }

            using (var afm = new AtomicFileManager<int, MockClassA>(_testName + ".database", _formatter))
            {
                afm.SaveFailed += new SaveFailed<MockClassA>(delegate(SaveFailureInfo<MockClassA> saveInfo)
                {
                    afm.Rebuild(saveInfo.NewRowSize, saveInfo.NewDatabaseSize, -1);
                });

                afm.Load();

                seg = afm.SaveSegment(largeEntity);
            }

            using (var afm = new AtomicFileManager<int, MockClassA>(_testName + ".database", _formatter))
            {
                afm.Load();

                Assert.Greater(afm.Stride, 2000);
                Assert.AreEqual(10240, afm.SeedPosition);

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
            Cleanup();

            var entity = TestResourceFactory.CreateRandom() as MockClassC;
            var largeEntity = TestResourceFactory.CreateRandom().WithName(new String('a', 2000)) as MockClassC;

            IDictionary<int, int> returnSegments = null;

            using (var afm = new AtomicFileManager<int, MockClassA>(_testName + ".database", _formatter))
            {
                afm.Load();

                using (var manager = new TransactionManager<int, MockClassA>(new MockTransactionFactory<int, MockClassA>()))
                {
                    manager.TransactionCommitted += new TransactionCommit<int, MockClassA>(
                        delegate(ITransaction<int, MockClassA> tranny, IList<ITransaction<int, MockClassA>> childTransactions)
                        {
                            returnSegments = afm.CommitTransaction(tranny, new Dictionary<int, int>());
                        });


                    using (var transaction = manager.BeginTransaction() as MockTransaction<int, MockClassA>)
                    {
                        transaction.Enlist(Action.Create, entity.Id, entity);

                        transaction.Commit();
                    }
                }
            }


            using (var afm = new AtomicFileManager<int, MockClassA>(_testName + ".database", _formatter))
            {
                afm.Load();

                using (var manager = new TransactionManager<int, MockClassA>(new MockTransactionFactory<int, MockClassA>()))
                {
                    afm.CommitFailed += new CommitFailed<int, MockClassA>(delegate(CommitFailureInfo<int, MockClassA> commitInfo)
                        {
                            afm.Rebuild(commitInfo.NewRowSize, commitInfo.NewDatabaseSize, -1);
                        });

                    manager.TransactionCommitted += new TransactionCommit<int, MockClassA>(
                        delegate(ITransaction<int, MockClassA> tranny, IList<ITransaction<int, MockClassA>> childTransactions)
                        {
                            returnSegments = afm.CommitTransaction(tranny, returnSegments);
                        });

                    using (var transaction = manager.BeginTransaction() as MockTransaction<int, MockClassA>)
                    {
                        transaction.Enlist(Action.Update, largeEntity.Id, largeEntity);

                        transaction.Commit();
                    }
                }
            }

            using (var afm = new AtomicFileManager<int, MockClassA>(_testName + ".database", _formatter))
            {
                afm.Load();

                Assert.Greater(afm.Stride, 2200);
                Assert.AreEqual(10240, afm.SeedPosition);

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
}
