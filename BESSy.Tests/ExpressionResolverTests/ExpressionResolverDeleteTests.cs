using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Reflection;
using BESSy.Queries;
using BESSy.Tests.Mocks;

namespace BESSy.Tests.ExpressionResolverTests
{
    [TestFixture]
    public class ExpressionResolverDeleteTests : FileTest
    {

        [Test]
        public void DeletesFromQuery()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var delete = new DeleteExpression(
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
                }

                using (var t = db.BeginTransaction())
                {
                    var results = eval.ExecuteDelete(delete);

                    Assert.AreEqual(1, results);

                    t.Commit();

                    Assert.IsNull(db.Fetch(first.Id));
                }

                Assert.IsNull(db.Fetch(first.Id));
            }
        }

        [Test]
        public void DeletesFirst10FromQuery()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();


            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var delete = new DeleteExpression(99, true,
                new CompareToken("Name", CompareEnum.GreaterOrEqual, first.Name));

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, MockClassA>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(o));

                    t.Commit();
                }

                using (var t = db.BeginTransaction())
                {
                    var results = eval.ExecuteDelete(delete);

                    Assert.LessOrEqual(1, results);

                    t.Commit();

                    Assert.IsNull(db.Fetch(first.Id));
                }

                Assert.IsNull(db.Fetch(first.Id));
            }
        }

        [Test]
        public void DeletesLast10FromQuery()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();


            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.FirstOrDefault() as MockClassC;

            var delete = new DeleteExpression(10, false,
                new CompareToken("Name", CompareEnum.Greater, "Z" + first.Name));

            using (var db = new Database<int, MockClassA>(_testName + ".database", "Id"))
            {
                var eval = new ExpressionResolver<int, MockClassA>(db);

                db.Load();

                using (var t = db.BeginTransaction())
                {
                    objs.ToList().ForEach(o => o.Id = db.Add(o));

                    t.Commit();
                }

                using (var t = db.BeginTransaction())
                {
                    var results = eval.ExecuteDelete(delete);

                    Assert.GreaterOrEqual(10, results);

                    t.Commit();
                    
                    if (results < 10)
                        Assert.IsNull(db.Fetch(first.Id));
                }
            }
        }
    }
}
