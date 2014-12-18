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
using BESSy.Indexes;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using BESSy.Tests.Mocks;
using BESSy.Transactions;
using BESSy.Json.Linq;
using NUnit.Framework;
using System.Threading.Tasks;
using BESSy.Queries;

namespace BESSy.Tests.DatabaseTests
{
    [TestFixture]
    public class DatabaseTransactionTests : FileTest
    {
        ISeed<int> _seed;
        IQueryableFormatter _formatter;

        [SetUp]
        public void Setup()
        {
            _seed = new Seed32(999);
            _formatter = TestResourceFactory.CreateJsonFormatterWithoutArrayFormatting();
        }

        [Test]
        public void DatabaseAddsUpdatesAndQueriesActiveTransaction()
        {

            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objects = TestResourceFactory.GetMockClassAObjects(12);

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                db.Load();

                db.BeginTransaction();

                foreach (var obj in objects)
                    db.AddOrUpdate(obj, 0);

                var update = db.Fetch(3);

                update.Name = "Updated " + update.Id;

                db.AddOrUpdate(update, update.Id);

                db.FlushAll();
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database"))
            {
                db.Load();

                db.Update<MockClassA>(u => !u.Value<string>("Name").Contains("Updated"), m => m.Name = "batch " + m.Id);

                db.FlushAll();

                var old = db.Select(s => s.Value<string>("Name").Contains("Updated"));

                Assert.AreEqual(1, old.Count);
                Assert.AreEqual("Updated 3", old.Single().Name);

                var updates = db.SelectFirst(s => s.Value<string>("Name").Contains("batch"), 11);

                Assert.AreEqual(11, updates.Count);
                Assert.AreEqual(1, updates.First().Id);
                Assert.AreEqual(12, updates.Last().Id);
            }
        }

        [Test]
        public void DatabaseRebuildsWithNewSeedSize()
        {

            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objects = TestResourceFactory.GetMockClassAObjects(10000);

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>() { InitialDbSize = 9000 }))
            {
                db.Load();

                using (var tran = db.BeginTransaction())
                {

                    foreach (var obj in objects)
                        obj.Id = db.AddOrUpdate(obj, 0);

                    var update = db.Fetch(3);

                    update.Name = "Updated " + update.Id;

                    db.AddOrUpdate(update, update.Id);

                    tran.Commit();
                }

                db.FlushAll();
            }

            var deletes = objects.Skip(5000).Take(2500);
            var queryDeletes = objects.Skip(7500);

            using (var db = new Database<int, MockClassA>(_testName + ".database"))
            {
                db.Load();

                var check = db.Fetch(10000);

                using (var tran = db.BeginTransaction())
                {
                    deletes.ToList().ForEach(d => db.Delete(d.Id));

                    db.Delete(s => s.Value<int>("Id") >= objects.First().Id);

                    tran.Commit();
                }
            }
        }

        [Test]
        public void TransactionResolvesDuplicateActions()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objects = TestResourceFactory.GetMockClassAObjects(12);

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objects.ToList().ForEach(o => o.Id = db.Add(o));

                    db.Update(objects[1], objects[1].Id);

                    t.Commit();
                }
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database"))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    db.Update<MockClassA>(s => s.Value<int>("Id") == objects[2].Id, a => a.Name = a.Name + " updated");

                    db.Delete(objects[2].Id);

                    t.Commit();
                }
            }
        }

        [Test]
        [ExpectedException(typeof(QueryExecuteException))]
        public void TransactionFailsWhenUpdatingDeletedRecord()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objects = TestResourceFactory.GetMockClassAObjects(12);

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                db.Load();

                db.BeginTransaction();

                foreach (var obj in objects)
                    db.AddOrUpdate(obj, 0);

                var update = db.Fetch(3);

                update.Name = "Updated " + update.Id;

                db.AddOrUpdate(update, update.Id);

                db.Flush();
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database"))
            {
                db.Load();

                db.BeginTransaction();

                db.Delete(d => d.Value<string>("Name").Contains("Updated"));

                db.Update<MockClassA>(u => u.Value<string>("Name").Contains("Updated"), m => m.Name = "Should Fail " + m.Id);

                db.Flush();
            }
        }
    }
}
