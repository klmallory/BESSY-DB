using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BESSy.Queries;
using BESSy.Tests.Mocks;
using NUnit.Framework;
using BESSy.Serialization.Converters;

namespace BESSy.Tests.ExpressionResolverTests
{
    [TestFixture]
    public class ExpressionResolverEvaluationTests : FileTest
    {
        [Test]
        public void SelectExpressionEvaluatesStrings()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var select = new WhereExpression(
                new CompareToken("ReferenceCode", CompareEnum.Like, first.ReferenceCode.Substring(1, first.ReferenceCode.Length - 2)),
                new CompareToken("ReferenceCode", CompareEnum.Equals, first.ReferenceCode),
                new CompareToken("ReferenceCode", CompareEnum.NotEquals, first.Name),
                new CompareToken("ReferenceCode", CompareEnum.NotEquals, null),
                new CompareToken("Name", CompareEnum.Greater, "Z" + first.Name),
                new CompareToken("Name", CompareEnum.GreaterOrEqual, first.Name),
                new CompareToken("Name", CompareEnum.Lesser, "A" + first.Name.Substring(1, first.Name.Length - 1)),
                new CompareToken("Name", CompareEnum.LesserOrEqual, "A" + first.Name));

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, MockClassA>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(o));

                    t.Commit();

                    var results = eval.ExecuteSelect(select);

                    Assert.AreEqual(1, results.Count);

                    MockClassC.Validate(results[0].ToObject<MockClassC>(), objs.FirstOrDefault() as MockClassC);
                }
            }
        }

        [Test]
        public void SelectExpressionEvaluatesGuids()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var select = new WhereExpression(
                new CompareToken("ReplicationID", CompareEnum.Equals, first.ReplicationID),
                new CompareToken("ReplicationID", CompareEnum.NotEquals, Guid.NewGuid()),
                new CompareToken("ReplicationID", CompareEnum.NotEquals, new Guid?()),
                new CompareToken("ReplicationID", CompareEnum.Greater, new BinConverterGuid().Max),
                new CompareToken("ReplicationID", CompareEnum.GreaterOrEqual, first.ReplicationID),
                new CompareToken("ReplicationID", CompareEnum.Lesser, Guid.Empty),
                new CompareToken("ReplicationID", CompareEnum.LesserOrEqual, first.ReplicationID));

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, MockClassA>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(o));

                    t.Commit();

                    var results = eval.ExecuteSelect(select);

                    Assert.AreEqual(1, results.Count);

                    MockClassC.Validate(results[0].ToObject<MockClassC>(), objs.FirstOrDefault() as MockClassC);
                }
            }
        }

        [Test]
        public void SelectExpressionEvaluatesBytes()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var select = new WhereExpression(
                1,
                true,
                new CompareToken("Id", CompareEnum.Equals, (byte)1),
                new CompareToken("Id", CompareEnum.NotEquals, (byte)(1 + 3)),
                new CompareToken("Id", CompareEnum.NotEquals, null),
                new CompareToken("Id", CompareEnum.Greater, (byte)(1 + 3)),
                new CompareToken("Id", CompareEnum.GreaterOrEqual, (byte)1),
                new CompareToken("Id", CompareEnum.Lesser, (byte)0),
                new CompareToken("Id", CompareEnum.LesserOrEqual, (byte)1));

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, MockClassA>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(o));

                    t.Commit();

                    var results = eval.ExecuteSelect(select);

                    Assert.AreEqual(1, results.Count);

                    MockClassC.Validate(results[0].ToObject<MockClassC>(), objs.FirstOrDefault() as MockClassC);
                }
            }
        }


        [Test]
        public void SelectExpressionEvaluatesInts()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var select = new WhereExpression(
                new CompareToken("Id", CompareEnum.Equals, 1),
                new CompareToken("Id", CompareEnum.NotEquals, 1 + 3),
                new CompareToken("Id", CompareEnum.NotEquals, null),
                new CompareToken("Id", CompareEnum.Greater, 1 + 3),
                new CompareToken("Id", CompareEnum.GreaterOrEqual, 1),
                new CompareToken("Id", CompareEnum.Lesser, 0),
                new CompareToken("Id", CompareEnum.LesserOrEqual, 1));

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, MockClassA>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(o));

                    t.Commit();

                    var results = eval.ExecuteSelect(select);

                    Assert.AreEqual(1, results.Count);

                    MockClassC.Validate(results[0].ToObject<MockClassC>(), objs.FirstOrDefault() as MockClassC);
                }
            }
        }

        [Test]
        public void SelectExpressionEvaluatesLongs()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var select = new WhereExpression(
                new CompareToken("BigId", CompareEnum.Equals, first.BigId),
                new CompareToken("BigId", CompareEnum.NotEquals, first.BigId + 3),
                new CompareToken("BigId", CompareEnum.NotEquals, null),
                new CompareToken("BigId", CompareEnum.Greater, first.BigId + 3),
                new CompareToken("BigId", CompareEnum.GreaterOrEqual, first.BigId),
                new CompareToken("BigId", CompareEnum.Lesser, first.BigId - 1),
                new CompareToken("BigId", CompareEnum.LesserOrEqual, first.BigId));

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, MockClassA>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(o));

                    t.Commit();

                    var results = eval.ExecuteSelect(select);

                    Assert.AreEqual(1, results.Count);

                    MockClassC.Validate(results[0].ToObject<MockClassC>(), objs.FirstOrDefault() as MockClassC);
                }
            }
        }

        [Test]
        public void SelectExpressionEvaluatesUInts()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var select = new WhereExpression(
                new CompareToken("Unsigned32", CompareEnum.Equals, first.Unsigned32),
                new CompareToken("Unsigned32", CompareEnum.NotEquals, first.Unsigned32 + 3),
                new CompareToken("Unsigned32", CompareEnum.NotEquals, null),
                new CompareToken("Unsigned32", CompareEnum.Greater, first.Unsigned32 + 3),
                new CompareToken("Unsigned32", CompareEnum.GreaterOrEqual, first.Unsigned32),
                new CompareToken("Unsigned32", CompareEnum.Lesser, first.Unsigned32 -1),
                new CompareToken("Unsigned32", CompareEnum.LesserOrEqual, first.Unsigned32));

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, MockClassA>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(o));

                    t.Commit();

                    var results = eval.ExecuteSelect(select);

                    Assert.AreEqual(1, results.Count);

                    MockClassC.Validate(results[0].ToObject<MockClassC>(), objs.FirstOrDefault() as MockClassC);
                }
            }
        }


        [Test]
        public void SelectExpressionEvaluatesUNInts()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var select = new WhereExpression(
                new CompareToken("Unsigned32", CompareEnum.Equals, new uint?(first.Unsigned32)),
                new CompareToken("Unsigned32", CompareEnum.NotEquals, new uint?(first.Unsigned32 + 3)),
                new CompareToken("Unsigned32", CompareEnum.Equals, new uint?()),
                new CompareToken("Unsigned32", CompareEnum.Greater, new uint?(first.Unsigned32 + 3)),
                new CompareToken("Unsigned32", CompareEnum.GreaterOrEqual, new uint?(first.Unsigned32)),
                new CompareToken("Unsigned32", CompareEnum.Lesser, new uint?(first.Unsigned32 - 1)),
                new CompareToken("Unsigned32", CompareEnum.LesserOrEqual, new uint?(first.Unsigned32)));

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, MockClassA>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(o));

                    t.Commit();

                    var results = eval.ExecuteSelect(select);

                    Assert.AreEqual(0, results.Count);
                }
            }
        }

        [Test]
        public void SelectExpressionEvaluatesULongs()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var select = new WhereExpression(
                new CompareToken("Unsigned64", CompareEnum.Equals, first.Unsigned64),
                new CompareToken("Unsigned64", CompareEnum.NotEquals, first.Unsigned64 + 3),
                new CompareToken("Unsigned64", CompareEnum.NotEquals, null),
                new CompareToken("Unsigned64", CompareEnum.Greater, first.Unsigned64 + 3),
                new CompareToken("Unsigned64", CompareEnum.GreaterOrEqual, first.Unsigned64),
                new CompareToken("Unsigned64", CompareEnum.Lesser, first.Unsigned64 - 1),
                new CompareToken("Unsigned64", CompareEnum.LesserOrEqual, first.Unsigned64));

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, MockClassA>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(o));

                    t.Commit();

                    var results = eval.ExecuteSelect(select);

                    Assert.AreEqual(1, results.Count);

                    MockClassC.Validate(results[0].ToObject<MockClassC>(), objs.FirstOrDefault() as MockClassC);
                }
            }
        }

        [Test]
        public void SelectExpressionEvaluatesShorts()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var select = new WhereExpression(
                new CompareToken("LittleId", CompareEnum.Equals, first.LittleId),
                new CompareToken("LittleId", CompareEnum.NotEquals, (short)(first.LittleId + 3)),
                new CompareToken("LittleId", CompareEnum.NotEquals, new short?()),
                new CompareToken("LittleId", CompareEnum.Greater, (short)(first.LittleId + 3)),
                new CompareToken("LittleId", CompareEnum.GreaterOrEqual, first.LittleId),
                new CompareToken("LittleId", CompareEnum.Lesser, (short)(first.LittleId - 1)),
                new CompareToken("LittleId", CompareEnum.LesserOrEqual, first.LittleId));

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, MockClassA>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(o));

                    t.Commit();

                    var results = eval.ExecuteSelect(select);

                    Assert.GreaterOrEqual(results.Count, 1);

                    MockClassC.Validate(results[0].ToObject<MockClassC>(), objs.FirstOrDefault() as MockClassC);
                }
            }
        }


        [Test]
        public void SelectExpressionEvaluatesUShorts()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var select = new WhereExpression(
                new CompareToken("Unsigned16", CompareEnum.Equals, first.Unsigned16),
                new CompareToken("Unsigned16", CompareEnum.NotEquals, (ushort)(first.Unsigned16 + 3)),
                new CompareToken("Unsigned16", CompareEnum.NotEquals, new ushort?()),
                new CompareToken("Unsigned16", CompareEnum.Greater, (ushort)(first.Unsigned16 + 3)),
                new CompareToken("Unsigned16", CompareEnum.GreaterOrEqual, first.Unsigned16),
                new CompareToken("Unsigned16", CompareEnum.Lesser, (ushort)(first.Unsigned16 - 1)),
                new CompareToken("Unsigned16", CompareEnum.LesserOrEqual, first.Unsigned16));

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, MockClassA>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(o));

                    t.Commit();

                    var results = eval.ExecuteSelect(select);

                    Assert.GreaterOrEqual(results.Count, 1);

                    MockClassC.Validate(results[0].ToObject<MockClassC>(), objs.FirstOrDefault() as MockClassC);
                }
            }
        }

        [Test]
        public void SelectExpressionEvaluatesFloats()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var select = new WhereExpression(
                new CompareToken("Location.Y", CompareEnum.Equals, first.Location.Y),
                new CompareToken("Location.Y", CompareEnum.NotEquals, (float)(first.Location.Y + 3)),
                new CompareToken("Location.Y", CompareEnum.NotEquals, new float?()),
                new CompareToken("Location.Z", CompareEnum.Greater, (float)(first.Location.Z + 3)),
                new CompareToken("Location.X", CompareEnum.GreaterOrEqual, first.Location.X),
                new CompareToken("Location.Y", CompareEnum.Lesser, (float)(first.Location.Y - 1)),
                new CompareToken("Location.Z", CompareEnum.LesserOrEqual, first.Location.Z));

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, MockClassA>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(o));

                    t.Commit();

                    var results = eval.ExecuteSelect(select);

                    Assert.GreaterOrEqual(results.Count, 1);

                    MockClassC.Validate(results[0].ToObject<MockClassC>(), objs.FirstOrDefault() as MockClassC);
                }
            }
        }


        [Test]
        public void SelectExpressionEvaluatesDoubles()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var select = new WhereExpression(
                new CompareToken("GetSomeCheckSum.$values[0]", CompareEnum.Equals, first.GetSomeCheckSum[0]),
                new CompareToken("GetSomeCheckSum.$values[0]", CompareEnum.NotEquals, (double)first.GetSomeCheckSum[0] + 3),
                new CompareToken("GetSomeCheckSum.$values[0]", CompareEnum.NotEquals, new double?()),
                new CompareToken("GetSomeCheckSum.$values[0]", CompareEnum.Greater, (double)(first.GetSomeCheckSum[0] + 3)),
                new CompareToken("GetSomeCheckSum.$values[0]", CompareEnum.GreaterOrEqual, first.GetSomeCheckSum[0]),
                new CompareToken("GetSomeCheckSum.$values[0]", CompareEnum.Lesser, (double)(first.GetSomeCheckSum[0] - 1)),
                new CompareToken("GetSomeCheckSum.$values[1]", CompareEnum.LesserOrEqual, first.GetSomeCheckSum[1]));

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, MockClassA>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(o));

                    t.Commit();

                    var results = eval.ExecuteSelect(select);

                    Assert.GreaterOrEqual(results.Count, 1);

                    MockClassC.Validate(results[0].ToObject<MockClassC>(), objs.FirstOrDefault() as MockClassC);
                }
            }
        }

        [Test]
        public void SelectExpressionEvaluatesDecimals()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var select = new WhereExpression(
                new CompareToken("DecAnimal", CompareEnum.Equals, first.DecAnimal),
                new CompareToken("DecAnimal", CompareEnum.NotEquals, (decimal)first.DecAnimal + 3),
                new CompareToken("DecAnimal", CompareEnum.NotEquals, new decimal?()),
                new CompareToken("DecAnimal", CompareEnum.Greater, (decimal)(first.DecAnimal + 3)),
                new CompareToken("DecAnimal", CompareEnum.GreaterOrEqual, first.DecAnimal),
                new CompareToken("DecAnimal", CompareEnum.Lesser, (decimal)(first.DecAnimal - 1)),
                new CompareToken("DecAnimal", CompareEnum.LesserOrEqual, first.DecAnimal));

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, MockClassA>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(o));

                    t.Commit();

                    var results = eval.ExecuteSelect(select);

                    Assert.GreaterOrEqual(results.Count, 1);

                    MockClassC.Validate(results[0].ToObject<MockClassC>(), objs.FirstOrDefault() as MockClassC);
                }
            }
        }

        [Test]
        public void SelectExpressionEvaluatesDateTimes()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var select = new WhereExpression(
                new CompareToken("MyDate", CompareEnum.Equals, first.MyDate),
                new CompareToken("MyDate", CompareEnum.NotEquals, first.MyDate.AddDays(3)),
                new CompareToken("MyDate", CompareEnum.NotEquals, new DateTime?()),
                new CompareToken("MyDate", CompareEnum.Greater, first.MyDate.AddDays(3)),
                new CompareToken("MyDate", CompareEnum.GreaterOrEqual, first.MyDate),
                new CompareToken("MyDate", CompareEnum.Lesser, first.MyDate.AddDays(-3)),
                new CompareToken("MyDate", CompareEnum.LesserOrEqual, first.MyDate));

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, MockClassA>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(o));

                    t.Commit();

                    var results = eval.ExecuteSelect(select);

                    Assert.GreaterOrEqual(results.Count, 1);

                    MockClassC.Validate(results[0].ToObject<MockClassC>(), objs.FirstOrDefault() as MockClassC);
                }
            }
        }
    }
}
