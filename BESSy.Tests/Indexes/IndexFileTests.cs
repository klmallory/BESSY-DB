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
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Threading;
using System.IO;
using System.Reflection;
using BESSy.Serialization.Converters;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Tests.Mocks;
using BESSy.Indexes;
using BESSy.Synchronization;
using BESSy.Files;

namespace BESSy.Tests.Indexes
{
    [TestFixture]
    public class IndexFileTests : FileTest
    {
        [Test]
        public void IndexFileCreate()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var seed = new Seed32(999) { IdConverter = new BinConverter32(), IdProperty = "Id" };

            using (var index = new IndexFileManager<string, MockClassA>
                (_testName + ".index"
                , "CatalogName"
                , 4096
                , 2048
                , 2048
                , new BinConverterString(1)
                , new BSONFormatter()
                , new RowSynchronizer<int>(new BinConverter32())))
                
            {
                index.Load();

                index.SaveSegment(new IndexPropertyPair<string, int>() { Id = "C", Property = 1 });
                index.SaveSegment(new IndexPropertyPair<string, int>() { Id = "A", Property = 2 });
                index.SaveSegment(new IndexPropertyPair<string, int>() { Id = "_", Property = 3 });

                var pair = index.LoadSegmentFrom(2);

                Assert.AreEqual("A", pair.Id);
                Assert.AreEqual(2, pair.Property);
            }
        }
    }
}
