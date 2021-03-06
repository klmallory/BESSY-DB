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
using BESSy.Json.Linq;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using BESSy.Transactions;
using NUnit.Framework;

namespace BESSy.Tests.JObjectDatabaseTests
{
    [TestFixture]
    public class JDatabaseCRUDTests : FileTest
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
        public void DatabaseLoadsDefaults()
        {   
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            using (var db = new JObjectDatabase<int>(_testName + ".database", "Id"))
            {
                db.Load();
            }

            using (var db = new JObjectDatabase<int>(_testName + ".database"))
            {
                db.Load();
            }
        }

        [Test]
        public void DatabaseFetchesSavedValues()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs= TestResourceFactory.GetMockClassAObjects(3).ToList();
            var ids = new List<int>();

            using (var db = new JObjectDatabase<int>(_testName + ".database", "Id"))
            {
                db.Load();

                objs.ToList().ForEach(o => o.Id = db.Add(db.Formatter.AsQueryableObj(o)));

                db.FlushAll();
            }

            using (var db = new JObjectDatabase<int>(_testName + ".database"))
            {
                db.Load();

                foreach (var obj in objs)
                {
                    var orig = obj as MockClassC;
                    var item = db.Fetch(obj.Id).ToObject<MockClassC>(db.Formatter.Serializer);

                    Assert.AreEqual(item.Id, orig.Id);
                    Assert.AreEqual(item.Name, orig.Name);
                    Assert.AreEqual(item.GetSomeCheckSum[0], orig.GetSomeCheckSum[0]);
                    Assert.AreEqual(item.Location.X, orig.Location.X);
                    Assert.AreEqual(item.Location.Y, orig.Location.Y);
                    Assert.AreEqual(item.Location.Z, orig.Location.Z);
                    Assert.AreEqual(item.Location.W, orig.Location.W);
                    Assert.AreEqual(item.ReferenceCode, orig.ReferenceCode);
                    Assert.AreEqual(item.ReplicationID, orig.ReplicationID);
                }
            }
        }

        [Test]
        public void DatabaseFetchesUpdatesAndDeletes()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();

            using (var db = new JObjectDatabase<int>(_testName + ".database", "Id"))
            {
                db.Load();

                objs.ToList().ForEach(o => o.Id = db.Add(db.Formatter.AsQueryableObj(o)));

                db.FlushAll();
            }

            using (var db = new JObjectDatabase<int>(_testName + ".database"))
            {
                db.Load();

                var last = db.Fetch(objs.Last().Id).ToObject<MockClassC>(db.Formatter.Serializer);

                Assert.IsNotNull(last);

                last.Name = "last";

                db.Update(db.Formatter.AsQueryableObj(last), last.Id);

                db.FlushAll();
            }

            using (var db = new JObjectDatabase<int>(_testName + ".database", "Id"))
            {
                db.Load();

                var last = db.Fetch(objs.Last().Id).ToObject<MockClassC>(db.Formatter.Serializer);

                Assert.IsNotNull(last);
                Assert.AreEqual("last", last.Name);

                Assert.IsNotNull(db.Fetch(objs.First().Id));

                db.Delete(objs.First().Id);

                Assert.IsNull(db.Fetch(objs.First().Id));

                db.FlushAll();
            }

            using (var db = new JObjectDatabase<int>(_testName + ".database", "Id"))
            {
                db.Load();

                Assert.IsNull(db.Fetch(objs.First().Id));

                db.Clear();
            }
        }

        [Test]
        public void DatabaseUpdatesIdFieldAndIndexes()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var ids = new List<int>();

            using (var db = new JObjectDatabase<int>(_testName + ".database", "Id"))
            {
                db.Load();

                objs.ToList().ForEach(o => o.Id = db.Add(db.Formatter.AsQueryableObj(o)));

                db.FlushAll();
            }

            using (var db = new JObjectDatabase<int>(_testName + ".database"))
            {
                db.Load();

                var last = db.Fetch(objs.Last().Id);

                Assert.IsNotNull(last);

                var oldId = last.Value<int>("Id");
                last.SetValue("Name", "last", db.Formatter.Serializer);
                last.SetValue("Id", 1024, db.Formatter.Serializer);

                db.Update(last, oldId);

                last = db.Fetch(last.Value<int>("Id"));

                Assert.AreEqual(1024, last.Value<int>("Id"));

                var old = db.Fetch(oldId);

                Assert.IsNull(old);

                db.FlushAll();
            }

            using (var db = new JObjectDatabase<int>(_testName + ".database", "Id"))
            {
                db.Load();

                var obj = db.Fetch(objs.Last().Id);
                Assert.IsNull(obj);

                var last = db.Fetch(1024).ToObject<MockClassC>();
                Assert.IsNotNull(last);

                Assert.AreEqual("last", last.Name);

                Assert.IsNotNull(db.Fetch(objs.First().Id));

                db.Delete(new int[] { objs.First().Id, objs.Last().Id });

                Assert.IsNull(db.Fetch(objs.First().Id));
                Assert.IsNull(db.Fetch(objs.Last().Id));

                db.FlushAll();
            }

            using (var db = new JObjectDatabase<int>(_testName + ".database", "Id"))
            {
                db.Load();

                Assert.IsNull(db.Fetch(objs.First().Id));

                db.Clear();
            }
        }

        [Test]
        [ExpectedException(typeof(NotSupportedException))]
        public void AddObjWithTypeThrowsException()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            using (var db = new JObjectDatabase<int>(_testName + ".database", "Id"))
            {
                db.Load();

                db.AddJObj(typeof(MockClassA), new JObject());
            }
        }

        [Test]
        [ExpectedException(typeof(NotSupportedException))]
        public void AddOrUpdateObjWithTypeThrowsException()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            using (var db = new JObjectDatabase<int>(_testName + ".database", "Id"))
            {
                db.Load();

                db.AddOrUpdateJObj(typeof(MockClassA), new JObject(), 0);
            }
        }
    }
}
