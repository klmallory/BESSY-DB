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
    public class JExpressionResolverSelectTests : FileTest
    {
        [Test]
        public void SelectExpressionReturnsItems()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var select = new WhereExpression(
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

                    var results = eval.ExecuteSelect(select);

                    Assert.AreEqual(1, results.Count);

                    MockClassC.Validate(results[0].ToObject<MockClassC>(), objs.FirstOrDefault() as MockClassC);
                }
            }
        }

        [Test]
        public void SelectExpressionReturnsFirstItems()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var max = objs.Cast<MockClassC>().Max(o => o.Location.X);
            var min = objs.Cast<MockClassC>().Min(o => o.Location.X);

            var first = objs.FirstOrDefault() as MockClassC;

            var select = new WhereExpression(10, true, new CompareToken("Location.X", CompareEnum.Greater, max - min));

            using (var db = new JObjectDatabase<int>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, JObject>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(db.Formatter.AsQueryableObj(o)));

                    t.Commit();

                    var results = eval.ExecuteSelect(select);

                    Assert.LessOrEqual(results.Count, 10);

                    foreach (var result in results)
                        MockClassC.Validate(result.ToObject<MockClassC>(), objs.FirstOrDefault(o => o.Id == result.Value<int>("Id")) as MockClassC);
                }
            }
        }

        [Test]
        public void SelectExpressionReturnsLastItems()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var max = objs.Cast<MockClassC>().Max(o => o.Location.X);
            var min = objs.Cast<MockClassC>().Min(o => o.Location.X);

            var first = objs.FirstOrDefault() as MockClassC;

            var select = new WhereExpression(10, false, new CompareToken("Location.X", CompareEnum.Lesser, max - min));

            using (var db = new JObjectDatabase<int>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, JObject>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(db.Formatter.AsQueryableObj(o)));

                    t.Commit();

                    var results = eval.ExecuteSelect(select);

                    Assert.LessOrEqual(results.Count, 10);

                    foreach (var result in results)
                        MockClassC.Validate(result.ToObject<MockClassC>(), objs.FirstOrDefault(o => o.Id == result.Value<int>("Id")) as MockClassC);
                }
            }
        }
    }
}
