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
using BESSy.Files;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using BESSy.Transactions;
using BESSy.Extensions;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using BESSy.Cache;

namespace BESSy.Tests.AtomicFileManagerTests
{
    [TestFixture]
    public class DatabaseCacheTests
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
        public void DatabaseCacheAllAroundTest()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var seed = new Seed32();

            var cleanEntities = TestResourceFactory.GetMockClassAObjects(500);
            var dirtyEntities = TestResourceFactory.GetMockClassAObjects(500);

            cleanEntities.ToList().ForEach(c => c.Id = seed.Increment());
            dirtyEntities.ToList().ForEach(d => d.Id = seed.Increment());

            var db = new DatabaseCache<int, MockClassA>(false, 2048, new BinConverter32());

            db.CacheItem(cleanEntities.First().Id);

            //with manual caching
            foreach (var e in cleanEntities)
                db.UpdateCache(e.Id, e, false, false);

            Assert.AreEqual(1, db.Count);
            Assert.IsTrue(db.Contains(cleanEntities.First().Id));
            Assert.AreEqual(cleanEntities.First().Name, db.GetFromCache(cleanEntities.First().Id).Name);

            //with autoCache
            foreach (var e in cleanEntities)
                db.UpdateCache(e.Id, e, true, false);

            Assert.AreEqual(500, db.Count);

            db.Detach(cleanEntities.First().Id);

            Assert.AreEqual(499, db.Count);
            Assert.IsFalse(db.Contains(cleanEntities.First().Id));
            Assert.IsNull(db.GetFromCache(cleanEntities.First().Id));

            db.CacheSize = 400;

            db.UpdateCache(cleanEntities.First().Id, cleanEntities.First(), true, false);

            Assert.AreEqual(201, db.Count);
            Assert.IsFalse(db.IsNew(cleanEntities.First().Id));

            db.ClearCache();

            Assert.AreEqual(0, db.Count);
        }

        [Test]
        public void FlushRequestAndUnloadDirtyTest()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var seed = new Seed32();

            var dirtyEntities = TestResourceFactory.GetMockClassAObjects(500);

            dirtyEntities.ToList().ForEach(d => d.Id = seed.Increment());

            var db = new DatabaseCache<int, MockClassA>(false, 2048, new BinConverter32());
            db.CacheSize = 400;

            db.FlushRequested += new EventHandler(delegate(object sender, EventArgs e)
            {
                var dbparam = sender as DatabaseCache<int, MockClassA>;
                Assert.AreEqual(dbparam.CacheSize + 1, dbparam.DirtyCount);

                var dirtyItems = dbparam.UnloadDirtyItems();

                Assert.AreEqual(0, dbparam.DirtyCount);
                Assert.AreEqual(0, dbparam.Count);
            });

            foreach (var d in dirtyEntities)
                db.UpdateCache(d.Id, d, true, true);

            db.Sweep();
        }
    }
}
