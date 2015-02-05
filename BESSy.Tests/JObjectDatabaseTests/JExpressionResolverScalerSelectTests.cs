using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BESSy.Json.Linq;
using BESSy.Queries;
using BESSy.Tests.Mocks;
using NUnit.Framework;

namespace BESSy.Tests.JObjectDatabaseTests
{
    [TestFixture]
    public class ExpressionResolverScalarSelectTests : FileTest
    {
        [Test]
        public void ScalerFromQuery()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var select = new ScalarSelectExpression(
                new string[] { "Location.Y" },
                new CompareToken("Id", CompareEnum.Equals, 1),
                new CompareToken("ReferenceCode", CompareEnum.Like, first.ReferenceCode),
                new CompareToken("Location.X", CompareEnum.Equals, first.Location.X),
                new CompareToken("Name", CompareEnum.Like, first.Name.Substring(1, first.Name.Length - 2)));

            using (var db = new JObjectDatabase<int>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, JObject>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(db.Formatter.AsQueryableObj(o)));

                    t.Commit();

                    var results = eval.ExecuteScaler(select);

                    Assert.AreEqual(1, results.Count);

                    Assert.AreEqual(first.Location.Y, results[0].Value<float>("Location.Y"));
                }
            }
        }

        [Test]
        public void SimpleScalerFromQuery()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var select = new ScalarSelectExpression(
               new string[] { "Location.Y" },
                new CompareToken("Id", CompareEnum.Equals, 1),
                new CompareToken("ReferenceCode", CompareEnum.Like, first.ReferenceCode),
                new CompareToken("Location.X", CompareEnum.Equals, first.Location.X),
                new CompareToken("Name", CompareEnum.Like, first.Name.Substring(1, first.Name.Length - 2)));

            using (var db = new JObjectDatabase<int>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, JObject>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(db.Formatter.AsQueryableObj(o)));

                    t.Commit();

                    var results = eval.ExecuteScaler(select);

                    Assert.AreEqual(1, results.Count);

                    Assert.AreEqual(first.Location.Y, Convert.ToSingle(results[0].Value<float>("Location.Y")));
                }


            }

            using (var db = new JObjectDatabase<int>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, JObject>(db);

                db.Load();

                var results2 = eval.ExecuteScaler(select);

                Assert.AreEqual(1, results2.Count);

                Assert.AreEqual(first.Location.Y, Convert.ToSingle(results2[0].Value<float>("Location.Y")));
            }
        }

        [Test]
        public void SimpleScalerFirstFromQuery()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var select = new ScalarSelectExpression(10, true,
                new string[] { "Location.Y" },
                new CompareToken("Id", CompareEnum.Equals, 1),
                new CompareToken("ReferenceCode", CompareEnum.Like, first.ReferenceCode),
                new CompareToken("Location.X", CompareEnum.Equals, first.Location.X),
                new CompareToken("Name", CompareEnum.Like, first.Name.Substring(1, first.Name.Length - 2)));

            using (var db = new JObjectDatabase<int>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, JObject>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(db.Formatter.AsQueryableObj(o)));

                    t.Commit();

                    var results = eval.ExecuteScaler(select);

                    Assert.AreEqual(1, results.Count);

                    Assert.AreEqual(first.Location.Y, Convert.ToSingle(results[0].Value<float>("Location.Y")));
                }


            }

            using (var db = new JObjectDatabase<int>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, JObject>(db);

                db.Load();

                var results2 = eval.ExecuteScaler(select);

                Assert.AreEqual(1, results2.Count);

                Assert.AreEqual(first.Location.Y, Convert.ToSingle(results2[0].Value<float>("Location.Y")));
            }
        }

        [Test]
        public void ScalerFirstFromQuery()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var select = new ScalarSelectExpression(10, true,
                new string[] { "Location.Y" },
                new CompareToken("Name", CompareEnum.Greater, "Z" + first.Name));

            using (var db = new JObjectDatabase<int>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, JObject>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(db.Formatter.AsQueryableObj(o)));

                    t.Commit();

                    var results = eval.ExecuteScaler(select);

                    Assert.LessOrEqual(10, results.Count);
                }
            }

            using (var db = new JObjectDatabase<int>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, JObject>(db);

                db.Load();

                var results2 = eval.ExecuteScaler(select);

                Assert.LessOrEqual(10, results2.Count);
            }
        }

        [Test]
        public void ScalerLastFromQuery()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var select = new ScalarSelectExpression(10, false,
                new string[] { "Location.Y" } ,
                new CompareToken("Name", CompareEnum.Greater, "Z" + first.Name));

            using (var db = new JObjectDatabase<int>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, JObject>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(db.Formatter.AsQueryableObj(o)));

                    t.Commit();

                    var results = eval.ExecuteScaler(select);

                    Assert.LessOrEqual(10, results.Count);
                }
            }

            using (var db = new JObjectDatabase<int>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, JObject>(db);

                db.Load();

                var results2 = eval.ExecuteScaler(select);

                Assert.LessOrEqual(10, results2.Count);
            }
        }
    }
}
