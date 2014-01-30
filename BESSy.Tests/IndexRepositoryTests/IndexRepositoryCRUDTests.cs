using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using BESSy.Files;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using BESSy.Json;
using NUnit.Framework;

namespace BESSy.Tests.IndexRepositoryTests
{
    [TestFixture]
    public class IndexRepositoryCRUDTests : FileTest
    {
        [Test]
        public void TestLoadsContentToMappingFile()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var testEntities = TestResourceFactory.GetMockClassAObjects(3);

            var repo = new IndexRepository<Guid, int>
                (_testName + ".scenario"
                , new BinConverter32());

            repo.Load();

            foreach (var e in testEntities)
                repo.Add(new IndexPropertyPair<Guid, int>(e.ReplicationID, e.Id));

            repo.Flush();

            while (repo.FileFlushQueueActive)
                Thread.Sleep(100);

            Assert.AreEqual(3, repo.Length);

            repo.Dispose();
        }

        [Test]
        public void TestUpdatesMappingFile()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var testEntities = TestResourceFactory.GetMockClassAObjects(300);

            var repo = new IndexRepository<Guid, int>
                (_testName + ".scenario"
                , new BinConverter32());

            repo.Load();

            foreach (var e in testEntities)
                repo.Add(new IndexPropertyPair<Guid, int>(e.ReplicationID, e.Id));

            var update = repo.Fetch(testEntities[150].ReplicationID);

            repo.Update(update, update.Id);
            var n = Guid.NewGuid();

            repo.AddOrUpdate(new IndexPropertyPair<Guid, int>(n, 300), Guid.Empty);

            repo.Flush();

            while (repo.FileFlushQueueActive)
                Thread.Sleep(100);

            Assert.AreEqual(301, repo.Length);

            update = repo.Fetch(testEntities[150].ReplicationID);
            update.Property = 303;
            repo.Update(update, update.Id);

            update = repo.Fetch(testEntities[120].ReplicationID);
            update.Property = 304;
            repo.Update(update, update.Id);

            repo.Flush();

            while (repo.FileFlushQueueActive)
                Thread.Sleep(100);

            repo.Dispose();
        }

        [Test]
        public void TestUsesRidLookup()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var testEntities = TestResourceFactory.GetMockClassAObjects(10);
            var seed = new Seed32();
            testEntities.ToList().ForEach(t => t.Id = seed.Increment());

            var repo = new IndexRepository<Guid, int>
                (_testName + ".scenario"
                , new BinConverter32());

            repo.Load();

            foreach (var e in testEntities)
                repo.Add(new IndexPropertyPair<Guid, int>(e.ReplicationID, e.Id));

            var r = repo.RidLookup(testEntities[5].Id);

            Assert.AreEqual(1, r.Count);

            Assert.AreEqual(testEntities[5].ReplicationID, r[0]);

            repo.Flush();

            while (repo.FileFlushQueueActive)
                Thread.Sleep(100);

            r = repo.RidLookup(testEntities[5].Id);

            Assert.AreEqual(1, r.Count);

            Assert.AreEqual(testEntities[5].ReplicationID, r[0]);

            repo.Dispose();
        }

        [Test]
        public void IndexRepositorySweepsCache()
        {
            _testName = System.Reflection.MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var testEntities = TestResourceFactory.GetMockClassAObjects(20000);
            var seed = new Seed32();
            testEntities.ToList().ForEach(t => t.Id = seed.Increment());

            var repo = new IndexRepository<Guid, int>
                (_testName + ".scenario"
                , new BinConverter32());

            repo.Load();

            foreach (var e in testEntities)
                repo.Add(new IndexPropertyPair<Guid, int>(e.ReplicationID, e.Id));

            repo.Sweep();

            repo.Flush();

            while (repo.FileFlushQueueActive)
                Thread.Sleep(100);

            repo.Sweep();

            repo.Dispose();
        }
    }
}
