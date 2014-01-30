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
using BESSy.Json.Linq;
using NUnit.Framework;
using System.Threading.Tasks;

namespace BESSy.Tests.FileRepositoryTests
{
    [TestFixture]
    public class XmlFileRepositoryTests : FileTest
    {
        [Test]
        public void XmlFileSavesClass()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var orig = TestResourceFactory.CreateRandom().WithId(1234) as MockClassC;

            using (var repo = new XmlFileManager<MockClassC>())
            {
                repo.WorkingPath = "";
                repo.SaveToFile(orig, _testName + ".xml", ".");
            }

            using (var repo = new XmlFileManager<MockClassC>())
            {
                var item = repo.LoadFromFile(_testName + ".xml");

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

        [Test]
        public void XmlFileSavesBinaryReadsXml()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var orig = TestResourceFactory.CreateRandom().WithId(1234) as MockClassC;

            using (var repo = new XmlFileManager<MockClassC>())
            {
                repo.WorkingPath = "";
                var xml = XmlSerializationHelper.Serialize(orig);
                var buffer = UTF8Encoding.UTF8.GetBytes(xml);

                repo.SaveToFile(buffer, _testName + ".xml", ".");
            }

            using (var repo = new XmlFileManager<MockClassC>())
            {
                repo.WorkingPath = "";
                var item = repo.LoadFromFile(_testName + ".xml", ".");

                Assert.AreEqual(item.Id, orig.Id);
                Assert.AreEqual(item.Name, orig.Name);
                Assert.AreEqual(item.GetSomeCheckSum[0], orig.GetSomeCheckSum[0]);
                Assert.AreEqual(item.Location.X, orig.Location.X);
                Assert.AreEqual(item.Location.Y, orig.Location.Y);
                Assert.AreEqual(item.Location.Z, orig.Location.Z);
                Assert.AreEqual(item.Location.W, orig.Location.W);
                Assert.AreEqual(item.ReferenceCode, orig.ReferenceCode);
                Assert.AreEqual(item.ReplicationID, orig.ReplicationID);

                var stream = repo.LoadAsStream(_testName + ".xml", ".");

                Assert.IsNotNull(stream);
            }
        }

        [Test]
        public void FileAddsUpdatesAndDeletesList()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var list = TestResourceFactory.GetMockClassAObjects(22).Cast<MockClassC>().ToList();
            list.ForEach(i => i.WithId(i.GetHashCode()));

            var container = new MockContainer() { AsList = list };

            using (var repo = new MockFileRepository(_testName + ".xml", "."))
            {
                repo.Load();
                repo.Clear();

                list.ForEach(i => i.Id = repo.Add(i));
                repo.AddOrUpdate(list.First(), list.First().Id);
                repo.AddOrUpdate(TestResourceFactory.CreateRandom() as MockClassC, 0);

                repo.Flush();
            }

            using (var repo = new MockFileRepository(_testName + ".xml", "."))
            {
                repo.Load();

                repo.Delete(list.Last().Id);
                list.Remove(list.Last());

                foreach (var orig in list)
                {
                    var item = repo.Fetch(orig.Id);

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

                Assert.AreEqual(22, repo.Length);
            }
        }
    }
}
