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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BESSy.Extensions;
using BESSy.Files;
using BESSy.Indexes;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using BESSy.Tests.Mocks;
using BESSy.Tests.ResourceRepositoryTests.Resources;
using BESSy.Transactions;
using BESSy.Json.Linq;
using NUnit.Framework;

namespace BESSy.Tests.ContentRepositoryTests
{
    [TestFixture]
    public class ContentRepositoryCRUDTests : FileTest
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
        public void RepoSavesContentFile()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var sw = new Stopwatch();

            using (var db = new Repository<ResourceContainer, string>
                (128
                , _testName + ".database"
                , true
                , new SeedString()
                , new BinConverterString()
                , new BSONFormatter()
                , new BatchFileManager<ResourceContainer>(new BSONFormatter()), "Name"))
            {
                db.Load();

                db.Add(new MockImageContainer(testRes.Luna_DIFF) { Name = "Luna_DIFF" });
                db.Add(new MockImageContainer(testRes.Luna_MAT) { Name = "Luna_MAT" });
                db.Add(new MockImageContainer(testRes.Luna_NRM) { Name = "Luna_NRM" });

                db.Flush();
            }

            using (var db = new Repository<ResourceContainer, string>
                (128
                , _testName + ".database"
                , true
                , new SeedString()
                , new BinConverterString()
                , new BSONFormatter()
                , new BatchFileManager<ResourceContainer>(new BSONFormatter()), "Name"))
            {
                var len = db.Load();

                Assert.AreEqual(3, len);

                Assert.AreEqual(db.Fetch("Luna_DIFF").GetResource<Bitmap>().Size.Width, testRes.Luna_DIFF.Size.Width);
                Assert.AreEqual(db.Fetch("Luna_MAT").GetResource<Bitmap>().Size.Width, testRes.Luna_MAT.Size.Width);
                Assert.AreEqual(db.Fetch("Luna_NRM").GetResource<Bitmap>().Size.Width, testRes.Luna_NRM.Size.Width);
            }
        }


        [Test]
        public void RepoContentQueryTests()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var sw = new Stopwatch();

            using (var db = new Repository<ResourceContainer, string>
                (128
                , _testName + ".database"
                , true
                , new SeedString()
                , new BinConverterString()
                , new BSONFormatter()
                , new BatchFileManager<ResourceContainer>(new BSONFormatter()), "Name"))
            {
                db.Load();

                db.Add(new MockImageContainer(testRes.Luna_DIFF) { Name = "Luna_DIFF" });
                db.Add(new MockImageContainer(testRes.Luna_MAT) { Name = "Luna_MAT" });
                db.Add(new MockImageContainer(testRes.Luna_NRM) { Name = "Luna_NRM" });

                var normalMaps = db.Select(c => c.Value<string>("Name").Contains("NRM"));

                Assert.AreEqual(1, normalMaps.Count);

                db.Update(c => c.Value<string>("Name").Contains("DIFF"),
                    new System.Action<ResourceContainer>(r => r.Name = "Luna_DIFF1"));

                var diff = db.Select(c => c.Value<string>("Name").Contains("DIFF1")).FirstOrDefault();

                Assert.IsNotNull(diff);

                db.Flush();

            }

            using (var db = new Repository<ResourceContainer, string>
                (128
                , _testName + ".database"
                , true
                , new SeedString()
                , new BinConverterString()
                , new BSONFormatter()
                , new BatchFileManager<ResourceContainer>(new BSONFormatter()), "Name"))
            {
                var len = db.Load();

                Assert.AreEqual(3, len);

                db.Update(c => c.Value<string>("Name").Contains("DIFF1"),
                    new System.Action<ResourceContainer>(r => r.Name = "Luna_DIFF"));

                var diff = db.Select(c => c.Value<string>("Name").Contains("DIFF")).FirstOrDefault();

                Assert.IsNotNull(diff);

                Assert.AreEqual(db.Fetch("Luna_DIFF").GetResource<Bitmap>().Size.Width, testRes.Luna_DIFF.Size.Width);
                Assert.AreEqual(db.Fetch("Luna_MAT").GetResource<Bitmap>().Size.Width, testRes.Luna_MAT.Size.Width);
                Assert.AreEqual(db.Fetch("Luna_NRM").GetResource<Bitmap>().Size.Width, testRes.Luna_NRM.Size.Width);
            }
        }
    }
}
