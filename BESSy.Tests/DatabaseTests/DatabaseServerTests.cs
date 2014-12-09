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


namespace BESSy.Tests.DatabaseTests
{
    [TestFixture]
    public class DatabaseServerTests : FileTest
    {
        [Test]
        public void DatabaseFetechesJObject()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var core = new FileCore<int, long>(new Seed32(999));
            var formatter = TestResourceFactory.CreateJsonFormatterWithoutArrayFormatting();

            var stopWatch = new Stopwatch();

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", core))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    TestResourceFactory.GetMockClassAObjects(5000).ToList().ForEach(a => db.AddJObj(JObject.FromObject(a, formatter.Serializer)));

                    t.Commit();
                }

                stopWatch.Reset();
                stopWatch.Start();
                var gets = db.SelectJObjLast(o => o.Value<int>("Id") > 4000, 10);
                stopWatch.Stop();

                Console.WriteLine("query with 10 last records retreived in {0} seconds", stopWatch.ElapsedMilliseconds / 1000m);

                Assert.AreEqual(10, gets.Count());

                foreach (var item in gets)
                    Assert.Greater(item.Value<int>("Id"), 4989);

                var obj = db.FetchJObj(4000);

                Assert.AreEqual(obj.Value<int>("Id"), 4000);
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database"))
            {
                var len = db.Load();

                stopWatch.Reset();
                stopWatch.Start();
                var gets = db.SelectLast(o => o.Value<int>("Id") > 4000, 10);
                stopWatch.Stop();

                Console.WriteLine("query with 10 last records retreived in {0} seconds", stopWatch.ElapsedMilliseconds / 1000m);

                Assert.AreEqual(10, gets.Count());

                foreach (var item in gets)
                    Assert.Greater(item.Id, 4000);

                var obj = db.FetchJObj(4000);

                Assert.AreEqual(obj.Value<int>("Id"), 4000);
            }
        }


        [Test]
        public void DatabaseUpdatesJObject()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var core = new FileCore<int, long>(new Seed32(999));
            var formatter = TestResourceFactory.CreateJsonFormatterWithoutArrayFormatting();

            var stopWatch = new Stopwatch();

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", core))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    TestResourceFactory.GetMockClassAObjects(5000).ToList().ForEach(a => db.AddOrUpdateJObj(JObject.FromObject(a, formatter.Serializer), a.Id));

                    t.Commit();
                }
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database"))
            {
                var len = db.Load();

                using (var t = db.BeginTransaction())
                {
                    var old = db.FetchJObj(2000);

                    var eVal = old.SelectToken("LittleId") as JValue;
                    var sVal = JToken.FromObject(10, db.Formatter.Serializer);

                    eVal.Replace(sVal);

                    db.AddOrUpdateJObj(old, old.Value<int>("Id"));

                    t.Commit();
                }

                stopWatch.Reset();
                stopWatch.Start();
                var gets = db.SelectLast(o => o.Value<int>("Id") > 4000, 10);
                stopWatch.Stop();

                Console.WriteLine("query with 10 last records retreived in {0} seconds", stopWatch.ElapsedMilliseconds / 1000m);

                Assert.AreEqual(10, gets.Count());

                foreach (var item in gets)
                    Assert.Greater(item.Id, 4000);

                var obj = db.FetchJObj(4000);

                Assert.AreEqual(obj.Value<int>("Id"), 4000);

                var updated = db.Fetch(2000) as MockClassC;

                Assert.AreEqual(10, updated.LittleId);
            }
        }

        [Test]
        [ExpectedException(typeof(DuplicateKeyException))]
        public void DatabaseErrosWithetId()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();


            var core = new FileCore<int, long>(new Seed32(999));
            var formatter = TestResourceFactory.CreateJsonFormatterWithoutArrayFormatting();

            var stopWatch = new Stopwatch();

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id", core))
            {
                db.Load();

                using (var t = db.BeginTransaction())
                {
                    var a = TestResourceFactory.CreateRandom();
                    a.Id = 5;

                    var obj = JObject.FromObject(a, formatter.Serializer);

                    db.AddJObj(obj);
                }
            }
        }
    }
}
