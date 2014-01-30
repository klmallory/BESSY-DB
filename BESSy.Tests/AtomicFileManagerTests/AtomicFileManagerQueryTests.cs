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
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using BESSy.Transactions;
using BESSy.Json.Linq;
using NUnit.Framework;

namespace BESSy.Tests.AtomicFileManagerTests
{
    [TestFixture]
    public class AtomicFileManagerQueryTests
    {
        string _testName;
        ISeed<int> _seed;
        IQueryableFormatter _formatter;


        [SetUp]
        public void Setup()
        {
            _seed = new Seed32(999);
            _formatter = TestResourceFactory.CreateJsonFormatterWithoutArrayFormatting();
        }

        void Cleanup()
        {
            var fi = new FileInfo(_testName + ".database");
            if (fi.Exists)
                while (fi.IsFileLocked())
                    Thread.Sleep(100);

            fi.Delete();
        }

        [Test]
        public void AfmReturnsRightNumberOfJObjects()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var addEntities = TestResourceFactory.GetMockClassAObjects(10000).ToList();

            foreach (var entity in addEntities)
                entity.Id = _seed.Increment();

            IDictionary<int, int> returnSegments = null;

            using (var afm = new AtomicFileManager<MockClassA>(_testName + ".database", _seed, new Seed32(0)))
            {
                afm.Load<int>();

                using (var manager = new TransactionManager<int, MockClassA>
                    (new MockTransactionFactory<int, MockClassA>()
                    , new TransactionSynchronizer<int, MockClassA>()))
                {
                    manager.TransactionCommitted += new TransactionCommit<int, MockClassA>(
                        delegate(ITransaction<int, MockClassA> tranny)
                        {
                            returnSegments = afm.CommitTransaction(tranny, new Dictionary<int, int>());

                            tranny.MarkComplete();
                        });

                    using (var tLock1 = manager.BeginTransaction())
                    {
                        addEntities.ForEach(delegate(MockClassA entity)
                        {
                            tLock1.Transaction.Enlist(Action.Create, entity.Id, entity);
                        });

                        tLock1.Transaction.Commit();

                        Assert.AreEqual(10000, afm.Length);
                    }

                    foreach (var group in afm.AsEnumerable())
                    {
                        var match = group.Where(i => i.SelectToken("Id").Value<int>() > 9999).ToList();

                        if (match == null || !match.Any())
                            continue;

                        var result = match.First().ToObject<MockClassC>();

                        Assert.IsNotNull(result);
                        Assert.AreEqual(10000, result.Id);
                        break;
                    }
                }
            }
        }
    }
}
