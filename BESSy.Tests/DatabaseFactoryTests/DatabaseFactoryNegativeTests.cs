using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BESSy.Crypto;
using BESSy.Factories;
using BESSy.Json;
using BESSy.Json.Linq;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using BESSy.Transactions;
using NUnit.Framework;

namespace BESSy.Tests.DatabaseFactoryTests
{
    [TestFixture]
    public class DatabaseFactoryNegativeTests : FileTest
    {
        string testAssemblyPath = Path.Combine(Environment.CurrentDirectory, @"..\..\..\Tools\");
        string settings = JsonConvert.SerializeObject(BSONFormatter.GetDefaultSettings(), JSONFormatter.GetDefaultSettings());

        [Test]
        [ExpectedException(typeof(DatabaseFactoryException))]
        public void DatabaseFactoryErrorsWithoutIdTypeParam()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var s = string.Format("filename = {0}; idtoken = {1}; entitytype = {2}; assembly = {3}"
                , _testName + ".database"
                , "Id"
                , typeof(MockClassC).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            var db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNull(db);
        }

        [Test]
        [ExpectedException(typeof(DatabaseFactoryException))]
        public void DatabaseFactoryErrorsWithoutEntityTypeParam()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var s = string.Format("filename = {0}; idtoken = {1}; idtype = {2}; assembly = {3}"
                , _testName + ".database"
                , "Id"
                , typeof(int).FullName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            var db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNull(db);
        }

        [Test]
        [ExpectedException(typeof(DatabaseFactoryException))]
        public void DatabaseFactoryErrorsWithoutFileNameParam()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var s = string.Format("idtoken = {0}; idtype = {1}; entitytype = {2}; assembly = {3}"
                , "Id"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            var db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNull(db);
        }


        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void DatabaseFactoryErrorsWithoutGetIdTokenArgument()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var s = string.Format("filename = {0}; idtoken = {1}; idtype = {2}; entitytype = {3}; assembly = {4}"
                , _testName + ".database"
                , "Id"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            var db = DatabaseFromStringFactory.GetIdToken(null);

            Assert.IsNull(db);
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void DatabaseFactoryErrorsWithoutGetFileNameArgument()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var s = string.Format("filename = {0}; idtoken = {1}; idtype = {2}; entitytype = {3}; assembly = {4}"
                , _testName + ".database"
                , "Id"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            var db = DatabaseFromStringFactory.GetFileName(null);

            Assert.IsNull(db);
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void DatabaseFactoryErrorsWithoutCreateSeedArgument()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var s = string.Format("filename = {0}; idtoken = {1}; idtype = {2}; entitytype = {3}; assembly = {4}"
                , _testName + ".database"
                , "Id"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            var db = DatabaseFromStringFactory.CreateSeed(null);

            Assert.IsNull(db);
        }
    }
}
