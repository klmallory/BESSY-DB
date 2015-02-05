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
using BESSy.Tests.AtomicFileManagerTests;
using BESSy.Reflection;
using BESSy.Cache;

namespace BESSy.Tests.Indexes
{
    [TestFixture]
    public class IndexTests : FileTest
    {
        [Test]
        public void IndexFetchesByMultipleIndexes()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var formatter = TestResourceFactory.CreateJsonFormatterWithoutArrayFormatting();
            var core = new FileCore<int, long>() { IdSeed = new Seed32(999), SegmentSeed = new Seed64(), Stride = 512 };

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToDictionary(d => d.Id = core.IdSeed.Increment());

            IDictionary<int, long> segs = null;

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", core, formatter))
            {
                using (var index = new Index<int, MockClassA, long>(_testName + ".index", "Id", true))
                {
                    afm.Load<int>();

                    index.Load();
                    index.Register(afm);

                    segs = AtomicFileManagerHelper.SaveSegments(afm, objs);

                    Assert.AreEqual(objs.Count, segs.Count);

                    afm.SaveCore();

                    var f = index.FetchSegment(objs.First().Key);
                    Assert.AreEqual(segs[objs.First().Key], f);

                    var all = index.FetchSegments(objs.First().Key, objs.Last().Key);
                    Assert.AreEqual(objs.Count, all.Count());

                    var many = index.FetchSegments(new int[] { objs.First().Key, objs.Last().Key });
                    Assert.AreEqual(2, many.Count());
                }
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", core, formatter))
            {
                var index = new Index<int, MockClassA, long>(_testName + ".index", "Id", true);
                afm.Load<int>();

                index.Load();
                index.Register(afm);

                var f = index.FetchSegment(objs.First().Key);
                Assert.AreEqual(segs[objs.First().Key], f);

                var all = index.FetchSegments(objs.First().Key, objs.Last().Key);
                Assert.AreEqual(objs.Count, all.Count());

                var many = index.FetchSegments(new int[] { objs.First().Key, objs.Last().Key});
                Assert.AreEqual(2, many.Count());
            }
        }

        [Test]
        public void IndexFetchesByMultipleSegments()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var formatter = TestResourceFactory.CreateJsonFormatterWithoutArrayFormatting();
            var core = new FileCore<int, long>() { IdSeed = new Seed32(999), SegmentSeed = new Seed64(), Stride = 512 };

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToDictionary(d => d.Id = core.IdSeed.Increment());

            IDictionary<int, long> segs = null;

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", core, formatter))
            {
                using (var index = new Index<int, MockClassA, long>(_testName + ".index", "Id", true))
                {
                    afm.Load<int>();

                    index.Load();
                    index.Register(afm);

                    segs = AtomicFileManagerHelper.SaveSegments(afm, objs);

                    Assert.AreEqual(objs.Count, segs.Count);

                    afm.SaveCore();

                    long seg;
                    var f = index.FetchIndex(segs[objs.First().Key], out seg);
                    Assert.AreEqual(objs.First().Key, f);

                    long[] segList;
                    var all = index.FetchIndexes(segs.Values.ToArray(), out segList);
                    Assert.AreEqual(objs.Count, all.Count());

                    var many = index.FetchIndexes(segs[objs.First().Key], out segList);
                    Assert.AreEqual(1, many.Count());
                }
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", core, formatter))
            {
                var index = new Index<int, MockClassA, long>(_testName + ".index", "Id", true);
                afm.Load<int>();

                index.Load();
                index.Register(afm);

                var f = index.FetchSegment(objs.First().Key);
                Assert.AreEqual(segs[objs.First().Key], f);

                var all = index.FetchSegments(objs.First().Key, objs.Last().Key);
                Assert.AreEqual(objs.Count, all.Count());

                var many = index.FetchSegments(new int[] { objs.First().Key, objs.Last().Key });
                Assert.AreEqual(2, many.Count());
            }
        }

        [Test]
        public void IndexClosesWithCorrectLength()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var formatter = TestResourceFactory.CreateJsonFormatterWithoutArrayFormatting();
            var core = new FileCore<string, long>() { IdSeed = new SeedString(2048), SegmentSeed = new Seed64(), Stride = 512 };

            var objs = TestResourceFactory.GetMockClassAObjects(1000).ToDictionary(d => d.Name);
            var additions = TestResourceFactory.GetMockClassAObjects(2).ToDictionary(d => d.Name);

            IDictionary<string, long> segs = null;

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", core, formatter))
            {
                using (var index = new Index<string, MockClassA, long>(_testName + ".index", "Name", true, 8, new BinConverterString(2048), new BinConverter64(), new RowSynchronizer<long>(new BinConverter64()), new RowSynchronizer<int>(new BinConverter32())))
                {
                    afm.Load<string>();

                    index.Load();
                    index.Register(afm);

                    segs = AtomicFileManagerHelper.SaveSegments(afm, objs);

                    Assert.AreEqual(1000, index.Length);
                }
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", core, formatter))
            {
                using (var index = new Index<string, MockClassA, long>(_testName + ".index", "Name", true, 8, new BinConverterString(2048), new BinConverter64(), new RowSynchronizer<long>(new BinConverter64()), new RowSynchronizer<int>(new BinConverter32())))
                {
                    afm.Load<string>();

                    index.Load();

                    Assert.AreEqual(1000, index.Length);

                    index.Register(afm);

                    Assert.AreEqual(1000, index.Length);

                    var idx = DynamicMemberManager.GetManager(index);
                    var pt = DynamicMemberManager.GetManager(idx._pTree);

                    Assert.AreEqual(3, pt._cache.Count);
                    Assert.AreEqual(400, pt._cache[0].Count);

                    segs = AtomicFileManagerHelper.SaveSegments(afm, additions);
                }
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", core, formatter))
            {
                using (var index = new Index<string, MockClassA, long>(_testName + ".index", "Name", true, 8, new BinConverterString(2048), new BinConverter64(), new RowSynchronizer<long>(new BinConverter64()), new RowSynchronizer<int>(new BinConverter32())))
                {
                    afm.Load<string>();

                    index.Load();

                    Assert.AreEqual(1002, index.Length);

                    index.Register(afm);

                    Assert.AreEqual(1002, index.Length);

                    var seg = index.FetchSegment(additions.First().Key);

                    Assert.IsNotNull(seg);
                }
            }
        }

        [Test]
        public void IndexReorganizes()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var formatter = TestResourceFactory.CreateJsonFormatterWithoutArrayFormatting();
            var core = new FileCore<int, long>() { IdSeed = new Seed32(999), SegmentSeed = new Seed64(), Stride = 512 };

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToDictionary(d => d.Id = core.IdSeed.Increment());

            IDictionary<int, long> segs = null;

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", core, formatter))
            {
                using (var index = new Index<int, MockClassA, long>(_testName + ".index", "Id", true, 8, new BinConverter32(), new BinConverter64(), new RowSynchronizer<long>(new BinConverter64()), new RowSynchronizer<int>(new BinConverter32())))
                {
                    afm.Load<int>();

                    index.Load();

                    segs = AtomicFileManagerHelper.SaveSegments(afm, objs);

                    //should trigger re-org.
                    index.Register(afm);

                    Assert.AreEqual(objs.Count, segs.Count);

                    afm.SaveCore();

                    long seg;
                    var f = index.FetchIndex(segs[objs.First().Key], out seg);
                    Assert.AreEqual(objs.First().Key, f);

                    long[] segList;
                    var all = index.FetchIndexes(segs.Values.ToArray(), out segList);
                    Assert.AreEqual(objs.Count, all.Count());

                    var many = index.FetchIndexes(segs[objs.First().Key], out segList);
                    Assert.AreEqual(1, many.Count());
                }
            }

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", core, formatter))
            {
                using (var index = new Index<int, MockClassA, long>(_testName + ".index", "Id", true))
                {
                    afm.Load<int>();

                    index.Load();
                    index.Register(afm);

                    var f = index.FetchSegment(objs.First().Key);
                    Assert.AreEqual(segs[objs.First().Key], f);

                    var all = index.FetchSegments(objs.First().Key, objs.Last().Key);
                    Assert.AreEqual(objs.Count, all.Count());

                    var many = index.FetchSegments(new int[] { objs.First().Key, objs.Last().Key });
                    Assert.AreEqual(2, many.Count());
                }
            }
        }
    }
}
