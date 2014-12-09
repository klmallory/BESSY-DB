using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Reflection;
using BESSy.Tests.Mocks;
using BESSy.Queries;

namespace BESSy.Tests.ExpressionResolverTests
{
    [TestFixture]
    public class ExpressionResolverUpdateScalarTests : FileTest
    {
        [Test]
        public void UpdateFromQuery()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            objs.OfType<MockClassC>().ToList().ForEach(o => o.ReferenceCode = null);

            var first = objs.FirstOrDefault() as MockClassC;
            var refCode =  "R " + new Random().Next();

            var select = new ScalarSelectExpression(
                new string[] { "Location.Y" },
                new CompareToken("Id", CompareEnum.Equals, 1),
                new CompareToken("Location.X", CompareEnum.Equals, first.Location.X),
                new CompareToken("Name", CompareEnum.Like, first.Name.Substring(1, first.Name.Length - 2)));

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
                    var count = eval.ExecuteUpdate(
                        new UpdateExpression(
                            typeof(MockClassC).AssemblyQualifiedName, 
                            select, 
                            new UpdateToken("Location.Y", 133.33, ValueEnum.Float),
                            new UpdateToken("Location.Z", 222.11, ValueEnum.Float),
                            new UpdateToken("ReferenceCode", refCode)));

                    Assert.AreEqual(1, count);

                    t.Commit();

                    var results = eval.ExecuteScaler(select);

                    Assert.AreEqual(1, results.Count);

                    Assert.AreEqual(133.33f, ((MockClassC)db.Fetch(1)).Location.Y);
                    Assert.AreEqual(222.11f, ((MockClassC)db.Fetch(1)).Location.Z);
                    Assert.AreEqual(refCode, ((MockClassC)db.Fetch(1)).ReferenceCode);
                }
            }
        }

        [Test]
        public void UpdateFromQueryAddsNewProperty()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var objs = TestResourceFactory.GetMockClassAObjects(100).ToList();
            var first = objs.First() as MockClassC;
            objs.OfType<MockClassC>().ToList().ForEach(o => o.Friend = null);

            var select = new ScalarSelectExpression(
                new string[] { "Friend.Location.Y" },
                new CompareToken("Id", CompareEnum.Equals, 1),
                new CompareToken("Name", CompareEnum.Like, first.Name.Substring(1, first.Name.Length - 2)));

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
                    var count = eval.ExecuteUpdate(
                        new UpdateExpression(
                            typeof(MockClassC).AssemblyQualifiedName,
                            select,
                            new UpdateToken("Friend.Location.Y", 133.33, ValueEnum.Float),
                            new UpdateToken("Friend.Location.Z", 222.11, ValueEnum.Float)));

                    Assert.AreEqual(1, count);

                    t.Commit();

                    var results = eval.ExecuteScaler(select);

                    Assert.AreEqual(1, results.Count);

                    Assert.AreEqual(133.33f, ((MockClassC)db.Fetch(1)).Friend.Location.Y);
                    Assert.AreEqual(222.11f, ((MockClassC)db.Fetch(1)).Friend.Location.Z);
                }
            }
        }
    }
}
