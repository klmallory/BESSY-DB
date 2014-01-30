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

namespace BESSy.Tests.DatabaseTests
{
    [TestFixture]
    public class DatabaseCompressionTests
    {
        [Test]
        [Category("Performance")]
        public void DatabaseCompressesVeryLargeFiles()
        {
            var test = new Mocks.MockImageContainer(testRes.IronAsteroid_NRM) { Name = "IronAsteroid_NRM" };

            var bson = new BSONFormatter();
            var zip = new QuickZipFormatter(bson);

            var bytes = bson.FormatObjStream(test);

            var binFormatted = zip.Format(bytes);

            var buffer = zip.Unformat(binFormatted);

            var unformatted = bson.UnformatObj<ResourceContainer>(buffer) as MockImageContainer;

            Assert.AreEqual(unformatted.Name, test.Name);
            Assert.AreEqual(unformatted.GetResource<Bitmap>().Size.Width, test.GetResource<Bitmap>().Size.Width);
        }
    }
}
