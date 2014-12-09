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
using BESSy.Factories;
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


namespace BESSy.Tests.Indexes
{
    public class SecondaryIndexTests : FileTest
    {
        protected override void Cleanup()
        {
            base.Cleanup();

            var fi = new FileInfo(_testName + ".database.catIndex.location");
            if (fi.Exists)
                while (fi.IsFileLocked())
                    Thread.Sleep(100);

            fi.Delete();
        }

        [Test]
        public void DatabaseLoadsAndUnloadsSecondaryIndex()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(25);

            objs[0].Name = "Booger";
            objs[1].Name = "Pluckers";
            objs[2].Name = "Pluckers";

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>())
                .WithIndex<string>("catIndex", "CatalogName", new BinConverterString()))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    foreach (var o in objs)
                        o.Id = db.Add(o);

                    t.Commit();
                }

                var check = db.FetchFromIndex<string>("catIndex", objs.First().CatalogName);

                Assert.IsNotNull(check);
                Assert.AreEqual(1, check.Count);
                Assert.AreEqual(objs.First().Id, check[0].Id);


                check = db.FetchFromIndex<string>("catIndex", "P");

                Assert.IsNotNull(check);
                Assert.AreEqual(2, check.Count);

                check = db.FetchRangeFromIndexInclusive<string>("catIndex", "A", "C");

                Assert.IsNotNull(check);
                Assert.AreEqual(23, check.Count);

                db.WithoutIndex("notThere");
                db.WithoutIndex("catIndex");
            }
        }

        [Test]
        [ExpectedException(typeof(DuplicateKeyException))]
        public void DatabasedisallowsDuplicateIndexName()
        {
            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>())
                .WithIndex<string>("catIndex", "CatalogName", new BinConverterString())
                .WithIndex<string>("catIndex", "CatalogName", new BinConverterString()))
            {
                db.Load();
            }
        }

        [Test]
        public void DatabaseFetchesUpdatesAndDeletesWithSecondaryIndex()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var seed = new Seed32();
            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var ids = new List<int>();

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>())
                .WithIndex<string>("catIndex", "CatalogName", new BinConverterString()))
            {
                db.Load();

                objs.ToList().ForEach(o => ids.Add(db.Add(o)));

                db.FlushAll();
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id")
                .WithIndex<string>("catIndex", "CatalogName", new BinConverterString()))
            {
                db.Load();

                var last = db.Fetch(objs.Last().Id);

                Assert.IsNotNull(last);

                last.Name = "last";

                db.Update(last, last.Id);

                db.FlushAll();
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>())
                .WithIndex<string>("catIndex", "CatalogName", new BinConverterString()))
            {
                db.Load();

                var last = db.Fetch(objs.Last().Id);

                Assert.IsNotNull(last);
                Assert.AreEqual("last", last.Name);

                Assert.IsNotNull(db.Fetch(objs.First().Id));

                db.Delete(objs.First().Id);

                Assert.IsNull(db.Fetch(objs.First().Id));

                db.FlushAll();
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>())
                .WithIndex<string>("catIndex", "CatalogName", new BinConverterString()))
            {
                db.Load();

                Assert.IsNull(db.Fetch(objs.First().Id));

                db.Clear();
            }
        }

        //TODO: why does this randomly fail?
        [Test]
        public void SecondaryIndexDeletesByIdAndByQuery()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(2500);

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>())
                .WithIndex<string>("catIndex", "CatalogName", new BinConverterString()))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    foreach (var o in objs)
                        o.Id = db.Add(o);

                    t.Commit();
                }

                db.Flush();
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id")
                .WithIndex<string>("catIndex", "CatalogName", new BinConverterString()))
            {
                db.Load();

                var delete1 = objs.Skip(500).Take(500);
                var delete2 = objs.Skip(1000).Take(500);

                using (var t = db.BeginTransaction())
                {
                    foreach (var o in delete1)
                        db.Delete(o.Id);

                    t.Commit();
                }

                while (db.FileFlushQueueActive)
                    Thread.Sleep(100);

                Assert.IsNull(db.Fetch(delete1.First().Id));

                using (var t = db.BeginTransaction())
                {
                    Assert.AreEqual(500, db.Delete(f => delete2.Any(a => a.Id == f.Value<int>("Id"))));

                    t.Commit();
                }

                while (db.FileFlushQueueActive)
                    Thread.Sleep(100);

                Assert.IsNull(db.Fetch(delete2.First().Id));

                Assert.AreEqual(1500, db.Select(s => true).Count());
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>())
                .WithIndex<string>("catIndex", "CatalogName", new BinConverterString()))
            {
                db.Load();

                db.Reorganize();

                Assert.AreEqual(1500, db.Select(s => true).Count());
            }
        }

        [Test]
        public void SecondaryIndexRebuldsOnLateCreate()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(2500);

            objs[0].Name = "Booger";
            objs[1].Name = "Pluckers";
            objs[2].Name = "Pluckers";

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", new FileCore<int, long>()))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    foreach (var o in objs)
                        o.Id = db.Add(o);

                    t.Commit();
                }

                db.FlushAll();
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id")
                .WithIndex<string>("catIndex", "CatalogName", new BinConverterString()))
            {
                db.Load();

                var check = db.FetchFromIndex<string>("catIndex", objs.First().CatalogName);

                Assert.IsNotNull(check);
                Assert.AreEqual(1, check.Count);
                Assert.AreEqual(objs.First().Id, check[0].Id);

                check = db.FetchFromIndex<string>("catIndex", "P");

                Assert.IsNotNull(check);
                Assert.AreEqual(2, check.Count);

                //check = db.FetchRangeFromIndexInclusive<string>("catIndex", "A", "C");

                //Assert.IsNotNull(check);
                //Assert.AreEqual(2498, check.Count);
            }
        }
    }
}
