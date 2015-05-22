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
using System.Threading;
using System.Linq;
using BESSy.Files;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using NUnit.Framework;
using System.Text;
using System.Drawing;
using BESSy.Tests.ResourceRepositoryTests.Resources;
using BESSy.Containers;
using System.Reflection;

namespace BESSy.Tests.DatabaseTests
{
    [TestFixture]
    public class DatabaseCompressionTests : FileTest
    {

        [Test]
        public void FormatterCompressesVeryLargeFiles()
        {
            var test = new Mocks.MockImageContainer(testRes.IronAsteroid_NRM) { Name = "IronAsteroid_NRM" };

            var bson = new BSONFormatter();
            var zip = new LZ4ZipFormatter(bson);

            var bytes = bson.FormatObjStream(test);

            var binFormatted = zip.Format(bytes);

            var buffer = zip.Unformat(binFormatted);

            var unformatted = bson.UnformatObj<ResourceContainer>(buffer) as MockImageContainer;

            Assert.AreEqual(unformatted.Name, test.Name);
            Assert.AreEqual(unformatted.GetResource<Bitmap>().Size.Width, test.GetResource<Bitmap>().Size.Width);
        }

        [Test]
        public void FormatterCompressesVeryLargeFileStreams()
        {
            var test = new Mocks.MockStreamContainer(new MemoryStream(testRes.MiscAngelic)) { Name = "MiscAngelic" };

            var bson = new BSONFormatter();
            var zip = new LZ4ZipFormatter(bson);

            var bytes = bson.FormatObjStream(test);

            var binFormatted = zip.Format(bytes);

            var buffer = zip.Unformat(binFormatted);

            var unformatted = bson.UnformatObj<ResourceStreamContainer>(buffer) as MockStreamContainer;

            Assert.AreEqual(unformatted.Name, test.Name);
            Assert.AreEqual(unformatted.GetResource<Stream>().Length, test.GetResource<Stream>().Length);
        }

        [Test]
        public void FormatterCompressesAndUnCompressesManyRandomObjects()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();

            var objs = TestResourceFactory.GetMockClassAObjects(257);

            using (var fLock = new ManagedFileLock(_testName))
            {
                Cleanup();

                using (var db = new Database<int, MockClassA>
                    (_testName + ".database", "Id",
                    new FileCore<int, long>() { MinimumCoreStride = 4096, InitialDbSize = 256 },
                     new LZ4ZipFormatter(new BSONFormatter())))
                {
                    db.Load();

                    using (var t = db.BeginTransaction())
                    {
                        foreach (var o in objs)
                            o.Id = db.Add(o);

                        t.Commit();
                    }
                }
                using (var db = new Database<int, MockClassA>
                    (_testName + ".database", new LZ4ZipFormatter(new BSONFormatter())))
                {
                    db.Load();

                    foreach (var o in objs)
                    {
                        var test = db.Fetch(o.Id);

                        Assert.IsNotNull(test, "object could not be decompressed or fetched {0}", o.Id);
                        Validation.ValidateC(o as MockClassC, test as MockClassC);
                    }
                }
            }
        }

        [Test]
        public void FormatterParsesAndUnparsesStream()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();

            var test1 = new Mocks.MockImageContainer(testRes.Luna_DIFF) { Name = "Luna_DIFF" };
            var test2 = new Mocks.MockImageContainer(testRes.IronAsteroid_NRM) { Name = "IronAsteroid_NRM" };

            using (var fLock = new ManagedFileLock(_testName))
            {
                Cleanup();
                using (var db = new Database<string, MockImageContainer>
                    (_testName + ".database", "Name",
                    new FileCore<string, long>(new SeedString(255)) { MinimumCoreStride = 4096, InitialDbSize = 256 },
                     new LZ4ZipFormatter(new BSONFormatter())))
                {
                    db.Load();

                    using (var t = db.BeginTransaction())
                    {
                        db.Add(test1);
                        db.Add(test2);

                        t.Commit();
                    }
                }

                using (var db = new Database<string, MockImageContainer>
                    (_testName + ".database", new LZ4ZipFormatter(new BSONFormatter())))
                {
                    db.Load();

                    var fetched = db.Fetch("Luna_DIFF");

                    Assert.IsNotNull(fetched);
                    Assert.AreEqual(fetched.Name, test1.Name);
                    Assert.AreEqual(fetched.GetResource<Bitmap>().Size.Height, test1.GetResource<Bitmap>().Size.Height);
                }
            }
        }
    }
}
