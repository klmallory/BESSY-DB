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
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace BESSy.Tests.Indexes
{
    [TestFixture]
    public class IndexTests : FileTest
    {

        [Test]
        public void IndexLoads()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            using (var db = new AtomicFileManager<MockClassA>(_testName + ".database", new BSONFormatter()))
            {
                db.Load();

                var seed = new Seed32();
                var objs = TestResourceFactory.GetMockClassAObjects(2500).ToList();
                objs.ForEach(a => a.WithId(seed.Increment()));

                using (var index = new Index<int, MockClassA>
                    (_testName + ".test.index"
                    , "Id"
                    , new BinConverter32()
                    , new RepositoryCacheFactory()
                    , new BSONFormatter()
                    , new IndexFileFactory()
                    , new RowSynchronizer<int>(new BinConverter32())))
                {
                    index.Load();

                    objs.ForEach(a => index.Add(a.Id, db.SaveSegment(a)));

                    index.Register(db);
                }
            }
        }

        [Test]
        public void IndexUpdatesSegment()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            using (var db = new AtomicFileManager<MockClassA>(_testName + ".database", new BSONFormatter()))
            {
                db.Load();

                var seed = new Seed32();
                var objs = TestResourceFactory.GetMockClassAObjects(2500).ToList();
                var segments = new Dictionary<int, int>();

                objs.ForEach(a => a.WithId(seed.Increment()));

                using (var index = new Index<int, MockClassA>
                    (_testName + ".test.index"
                    , "Id"
                    , new BinConverter32()
                    , new RepositoryCacheFactory()
                    , new BSONFormatter()
                    , new IndexFileFactory()
                    , new RowSynchronizer<int>(new BinConverter32())))
                {
                    index.Load();

                    objs.ForEach(a => segments.Add(a.Id, index.Add(a.Id, db.SaveSegment(a))));

                    index.Update(segments.Last().Key, segments.Last().Value);

                    index.Register(db);
                }
            }
        }

        [Test]
        public void IndexDeletesSegment()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            using (var db = new AtomicFileManager<MockClassA>(_testName + ".database", new BSONFormatter()))
            {
                db.Load();

                var seed = new Seed32();
                var objs = TestResourceFactory.GetMockClassAObjects(2500).ToList();
                var segments = new Dictionary<int, int>();

                objs.ForEach(a => a.WithId(seed.Increment()));

                using (var index = new Index<int, MockClassA>
                    (_testName + ".test.index"
                    , "Id"
                    , new BinConverter32()
                    , new RepositoryCacheFactory()
                    , new BSONFormatter()
                    , new IndexFileFactory()
                    , new RowSynchronizer<int>(new BinConverter32())))
                {
                    index.Load();

                    index.Register(db);

                    objs.ForEach(a => segments.Add(a.Id, index.Add(a.Id, db.SaveSegment(a))));

                    index.Delete(segments.Last().Key);

                    Assert.AreEqual(0, index.Fetch(segments.Last().Key));
                }
            }
        }

        [Test]
        public void IndexReorganizes()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var sw = new Stopwatch();

            using (var db = new AtomicFileManager<MockClassA>(_testName + ".database", new BSONFormatter()))
            {
                db.Load();

                var seed = new Seed32();
                var objs = TestResourceFactory.GetMockClassAObjects(2500).ToList();
                objs.ForEach(a => a.WithId(seed.Increment()));

                using (var index = new Index<int, MockClassA>
                    (_testName + ".test.index"
                    , "Id"
                    , new BinConverter32()
                    , new RepositoryCacheFactory()
                    , new BSONFormatter()
                    , new IndexFileFactory()
                    , new RowSynchronizer<int>(new BinConverter32())))
                {
                    index.Load();

                    objs.ForEach(a => index.Add(a.Id, db.SaveSegment(a)));

                    index.Register(db);

                    sw.Reset();
                    sw.Start();

                    index.Flush();

                    sw.Stop();

                    Console.WriteLine("Index reorganization took {0} seconds for {1} records.", sw.ElapsedMilliseconds / 1000.00, objs.Count); 
                }
            }
        }
    }
}
