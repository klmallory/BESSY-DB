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
using BESSy.Parallelization;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using BESSy.Tests.Mocks;
using BESSy.Transactions;
using NUnit.Framework;

namespace BESSy.Tests.AtomicFileManagerTests
{
    [TestFixture]
    public class AtomicFileManagerStreamingTests : FileTest
    {
        [Test]
        public void AtomicFileManagerReturnsPagedStream()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var formatter = new BSONFormatter();
            var core = new FileCore<int, long>() { IdSeed = new Seed32(999), SegmentSeed = new Seed64(), MinimumCoreStride = 512 };
            var addEntities = TestResourceFactory.GetMockClassAObjects(20480).ToList();

            foreach (var entity in addEntities)
                entity.Id = core.IdSeed.Increment();

            var updateEntities = new List<MockClassC>();

            IDictionary<int, long> returnSegments = null;
            IDictionary<int, long> deletedSegments = new Dictionary<int, long>();

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", core))
            {
                afm.Load<int>();

                afm.Rebuilt += new Rebuild<MockClassA>(delegate(Guid transactionId, int newStride, long newLength, int newSeedStride)
                {
                    afm.SaveCore<int>();
                });

                var segs = AtomicFileManagerHelper.SaveSegments(afm, addEntities.ToDictionary(e => e.Id));

                afm.SaveCore<int>();

                var s = afm.GetPageStream(1);

                Assert.IsNotNull(s);
                Assert.GreaterOrEqual(s.Length, 1441792);
                Assert.AreEqual(8, afm.AsStreaming().Count());
            }
        }
    }
}
