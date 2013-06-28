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
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Threading.Tasks;
using System.Drawing;
using BESSy.Tests.ResourceRepositoryTests.Resources;

namespace BESSy.Tests.DatabaseTests
{
    [TestFixture]
    public class DatabaseCapacityTests : FileTest
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
        public void DatabaseSavesTenThousandRecords()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(10000).ToList();

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                db.Load();

                objs.ToList().ForEach(o => db.Add(o));
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database"))
            {
                var len = db.Load();

                Assert.AreEqual(objs.Count(), len);
            }
        }

        [Test]
        public void DatabaseSavesLargeFile()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var img = new ImageConverter();
            var lunaDiff = (byte[])img.ConvertTo(testRes.Luna_DIFF, typeof(byte[]));
            var lunaMat = (byte[])img.ConvertTo(testRes.Luna_MAT, typeof(byte[]));
            var lunaNrm = (byte[])img.ConvertTo(testRes.Luna_NRM, typeof(byte[]));

            using (var db = new Database<string, ResourceContainer<byte[]>>(_testName + ".database", "Name"))
            {
                db.Load();

                using (var tran = db.BeginTransaction())
                {
                    db.Add(new ResourceContainer<byte[]>() { Name = "Luna_DIFF", Value = lunaDiff });
                    db.Add(new ResourceContainer<byte[]>() { Name = "Luna_MAT", Value = lunaMat });
                    db.Add(new ResourceContainer<byte[]>() { Name = "Luna_NRM", Value = lunaNrm });

                    tran.Commit();
                }
            }

            using (var db = new Database<string, ResourceContainer<byte[]>>(_testName + ".database"))
            {
                var len = db.Load();

                Assert.AreEqual(3, len);

                Assert.AreEqual(db.Fetch("Luna_DIFF").Value.Length, lunaDiff.Length);
                Assert.AreEqual(db.Fetch("Luna_MAT").Value.Length, lunaMat.Length);
                Assert.AreEqual(db.Fetch("Luna_NRM").Value.Length, lunaNrm.Length);
            }
        }

        [Test]
        public void DatabaseSavesOneHundredThousandRecords()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            decimal avgTime = 0;
            var stopWatch = new Stopwatch();

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                db.Load();

                stopWatch.Start();
                using (var t = db.BeginTransaction())
                {
                    TestResourceFactory.GetMockClassAObjects(25000).ToList().ForEach(a => db.Add(a));

                    t.Commit();
                }
                stopWatch.Stop();
                avgTime = (avgTime + stopWatch.ElapsedMilliseconds);

                Console.WriteLine("Transaction with 25000 entities committed in {0} seconds", stopWatch.ElapsedMilliseconds / 1000m);

                stopWatch.Reset();
                stopWatch.Start();
                using (var t = db.BeginTransaction())
                {
                    TestResourceFactory.GetMockClassAObjects(25000).ToList().ForEach(a => db.Add(a));

                    t.Commit();
                }
                stopWatch.Stop();
                avgTime = (avgTime + stopWatch.ElapsedMilliseconds) / 2;

                Console.WriteLine("Transaction with 25000 entities committed in {0} seconds", stopWatch.ElapsedMilliseconds / 1000m);

                stopWatch.Reset();
                stopWatch.Start();
                using (var t = db.BeginTransaction())
                {
                    TestResourceFactory.GetMockClassAObjects(25000).ToList().ForEach(a => db.Add(a));

                    t.Commit();
                }
                stopWatch.Stop();
                avgTime = (avgTime + stopWatch.ElapsedMilliseconds) / 2;

                Console.WriteLine("Transaction with 25000 entities committed in {0} seconds", stopWatch.ElapsedMilliseconds / 1000m);

                stopWatch.Reset();
                stopWatch.Start();
                using (var t = db.BeginTransaction())
                {
                    TestResourceFactory.GetMockClassAObjects(25000).ToList().ForEach(a => db.Add(a));

                    t.Commit();
                }
                stopWatch.Stop();
                avgTime = (avgTime + stopWatch.ElapsedMilliseconds) / 2;

                Console.WriteLine("Transaction with 25000 entities committed in {0} seconds", stopWatch.ElapsedMilliseconds / 1000m);

                Console.WriteLine("Avg Commit time for transaction with 25000 entities {0} seconds", avgTime / 1000m);

                stopWatch.Reset();
                stopWatch.Start();
                Assert.AreEqual(20000, db.Select(o => o.Value<int>("Id") > 80000).Count());
                stopWatch.Stop();

                Console.WriteLine("query with 20000 records retreived in {0} seconds", stopWatch.ElapsedMilliseconds / 1000m);

                db.Flush();
            }

            using (var db = new Database<int, MockClassA>(_testName + ".database"))
            {
                var len = db.Load();

                Assert.AreEqual(100000, len);

                stopWatch.Reset();
                stopWatch.Start();
                Assert.AreEqual(20000, db.Select(o => o.Value<int>("Id") > 80000).Count());
                stopWatch.Stop();

                Console.WriteLine("query with 20000 records retreived in {0} seconds", stopWatch.ElapsedMilliseconds / 1000m);

                db.Clear();
            }
        }
    }
}

