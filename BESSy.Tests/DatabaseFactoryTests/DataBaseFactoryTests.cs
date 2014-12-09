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
    public class DataBaseFactoryTests : FileTest
    {
        string testAssemblyPath = Path.Combine(Environment.CurrentDirectory, @"..\..\..\Tools\");
        string settings = JsonConvert.SerializeObject(BSONFormatter.GetDefaultSettings(), JSONFormatter.GetDefaultSettings());

        [Test]
        public void DatabaseFactoryCreatesSimpleInt32db()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var s = string.Format("filename = {0}; idtoken = {1}; idtype = {2}; entitytype = {3}; assembly = {4}"
                , _testName + ".database"
                , "Id"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            var db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNotNull(db);

            Console.WriteLine(s);

            db.Load();
        }

        [Test]
        public void CreateInt32dbWithSeed()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var s = string.Format("filename = {0}; idtoken = {1}; idtype = {2}; entitytype = {3}; seedtype = {4}; assembly = {5}"
                , _testName + ".database"
                , "Id"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , typeof(Seed32).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            var db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNotNull(db);

            Console.WriteLine(s);

            db.Load();
        }

        [Test]
        public void CreateInt32dbWithStartingSeed()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var s = string.Format("filename = {0}; idtoken = {1}; idtype = {2}; entitytype = {3}; seedtype = {4}; startingseed = 1000; assembly = {5}"
                , _testName + ".database"
                , "Id"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , typeof(Seed32).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            var db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNotNull(db);

            Console.WriteLine(s);

            db.Load();
        }

        [Test]
        public void CreateInt32dbWithSeedAndConverter()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var s = string.Format("filename = {0}; idtoken = {1}; idtype = {2}; entitytype = {3}; seedtype = {4}; binconvertertype = {5}; assembly = {6}"
                , _testName + ".database"
                , "Id"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , typeof(Seed32).AssemblyQualifiedName
                , typeof(BinConverter32).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            var db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNotNull(db);

            Console.WriteLine(s);

            db.Load();

            db.Dispose();

            s = string.Format(@"filename = {0}; idtype = {1}; entitytype = {2}; assembly = {3}"
                , _testName + ".database"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNotNull(db);

            Console.WriteLine(s);

            db.Load();
        }


        [Test]
        public void CreateInt32dbWithSeedConverterAndFormatter()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var s = string.Format("filename = {0}; idtoken = {1}; idtype = {2}; entitytype = {3}; seedtype = {4}; binconvertertype = {5}; formattertype = {6};  assembly = {7}"
                , _testName + ".database"
                , "Id"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , typeof(Seed32).AssemblyQualifiedName
                , typeof(BinConverter32).AssemblyQualifiedName
                , typeof(BSONFormatter).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            var db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNotNull(db);

            Console.WriteLine(s);

            db.Load();

            db.Dispose();

            s = string.Format(@"filename = {0}; idtype = {1}; entitytype = {2}; formattertype = {3}; assembly = {4}"
                , _testName + ".database"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , typeof(BSONFormatter).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNotNull(db);

            Console.WriteLine(s);

            db.Load();
        }

        [Test]
        public void CreateInt32dbWithSeedConverterFormatterAndSerializerSettings()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var s = string.Format("filename = {0}; idtoken = {1}; idtype = {2}; entitytype = {3}; seedtype = {4}; binconvertertype = {5}; formattertype = {6}; jsonserializersettings ={7};  assembly = {8}"
                , _testName + ".database"
                , "Id"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , typeof(Seed32).AssemblyQualifiedName
                , typeof(BinConverter32).AssemblyQualifiedName
                , typeof(BSONFormatter).AssemblyQualifiedName
                , settings
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            var db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNotNull(db);

            Console.WriteLine(s);

            db.Load();

            db.Dispose();

            s = string.Format(@"filename = {0}; idtype = {1}; entitytype = {2}; formattertype = {3}; jsonserializersettings ={4}; assembly = {5}"
                , _testName + ".database"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , typeof(BSONFormatter).AssemblyQualifiedName
                , settings
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNotNull(db);

            Console.WriteLine(s);

            db.Load();
        }

        [Test]
        public void CreateInt32dbWithSeedConverterFormatterAndTransactionManager()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var s = string.Format("filename = {0}; idtoken = {1}; idtype = {2}; entitytype = {3}; seedtype = {4}; binconvertertype = {5}; formattertype = {6}; transactionmanagertype= {7};  assembly = {8}"
                , _testName + ".database"
                , "Id"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , typeof(Seed32).AssemblyQualifiedName
                , typeof(BinConverter32).AssemblyQualifiedName
                , typeof(BSONFormatter).AssemblyQualifiedName
                , typeof(TransactionManager<Int32, MockClassC>).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            var db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNotNull(db);

            db.Load();

            db.Dispose();

            s = string.Format(@"filename = {0}; idtype = {1}; entitytype = {2}; formattertype = {3}; transactionmanagertype= {4}; assembly = {5}"
                , _testName + ".database"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , typeof(BSONFormatter).AssemblyQualifiedName
                , typeof(TransactionManager<Int32, MockClassC>).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNotNull(db);

            db.Load();
        }

        [Test]
        public void CreateInt32dbWithSeedConverterFormatterTransactionAndFileManagerFactory()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var s = string.Format("filename = {0}; idtoken = {1}; idtype = {2}; entitytype = {3}; seedtype = {4}; binconvertertype = {5}; formattertype = {6}; transactionmanagertype= {7}; filefactorytype ={8};  assembly = {9}"
                , _testName + ".database"
                , "Id"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , typeof(Seed32).AssemblyQualifiedName
                , typeof(BinConverter32).AssemblyQualifiedName
                , typeof(BSONFormatter).AssemblyQualifiedName
                , typeof(TransactionManager<Int32, MockClassC>).AssemblyQualifiedName
                , typeof(AtomicFileManagerFactory).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            var db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNotNull(db);

            db.Load();

            db.Dispose();

            s = string.Format(@"filename = {0}; idtype = {1}; entitytype = {2}; formattertype = {3}; transactionmanagertype= {4}; filefactorytype ={5}; assembly = {6}"
                , _testName + ".database"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , typeof(BSONFormatter).AssemblyQualifiedName
                , typeof(TransactionManager<Int32, MockClassC>).AssemblyQualifiedName
                , typeof(AtomicFileManagerFactory).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNotNull(db);

            db.Load();
        }

        [Test]
        public void CreateInt32dbWithSeedConverterFormatterTransactionFileAndCache()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var s = string.Format(@"filename = {0}; idtoken = {1}; idtype = {2}; entitytype = {3}; seedtype = {4}; binconvertertype = {5}; 
                formattertype = {6}; transactionmanagertype= {7}; filefactorytype ={8}; cachefactorytype={9}; cachesize=10000; assembly = {10}"
                , _testName + ".database"
                , "Id"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , typeof(Seed32).AssemblyQualifiedName
                , typeof(BinConverter32).AssemblyQualifiedName
                , typeof(BSONFormatter).AssemblyQualifiedName
                , typeof(TransactionManager<Int32, MockClassC>).AssemblyQualifiedName
                , typeof(AtomicFileManagerFactory).AssemblyQualifiedName
                , typeof(DatabaseCacheFactory).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            var db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNotNull(db);

            db.Load();

            db.Dispose();

            s = string.Format(@"filename = {0}; idtype = {1}; entitytype = {2}; formattertype = {3}; transactionmanagertype= {4}; filefactorytype ={5}; 
                    cachefactorytype={6}; assembly = {7}"
                , _testName + ".database"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , typeof(BSONFormatter).AssemblyQualifiedName
                , typeof(TransactionManager<Int32, MockClassC>).AssemblyQualifiedName
                , typeof(AtomicFileManagerFactory).AssemblyQualifiedName
                , typeof(DatabaseCacheFactory).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNotNull(db);

            db.Load();
        }

        [Test]
        public void CreateInt32dbWithSeedConverterFormatterTransactionFileCacheAndIndexFactories()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var s = string.Format(@"filename = {0}; idtoken = {1}; idtype = {2}; entitytype = {3}; seedtype = {4}; binconvertertype = {5}; 
                formattertype = {6}; transactionmanagertype= {7}; filefactorytype ={8}; cachefactorytype={9}; indexfactorytype = {10}; indexfilefactorytype={11}; assembly = {12}"
                , _testName + ".database"
                , "Id"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , typeof(Seed32).AssemblyQualifiedName
                , typeof(BinConverter32).AssemblyQualifiedName
                , typeof(BSONFormatter).AssemblyQualifiedName
                , typeof(TransactionManager<Int32, MockClassC>).AssemblyQualifiedName
                , typeof(AtomicFileManagerFactory).AssemblyQualifiedName
                , typeof(DatabaseCacheFactory).AssemblyQualifiedName
                , typeof(IndexFactory).AssemblyQualifiedName
                , typeof(IndexFileFactory).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            var db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNotNull(db);

            db.Load();

            db.Dispose();

            s = string.Format(@"filename = {0}; idtype = {1}; entitytype = {2}; formattertype = {3}; transactionmanagertype= {4}; filefactorytype ={5}; 
                    cachefactorytype={6}; indexfactorytype = {7}; indexfilefactorytype={8}; assembly = {9}"
                , _testName + ".database"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , typeof(BSONFormatter).AssemblyQualifiedName
                , typeof(TransactionManager<Int32, MockClassC>).AssemblyQualifiedName
                , typeof(AtomicFileManagerFactory).AssemblyQualifiedName
                , typeof(DatabaseCacheFactory).AssemblyQualifiedName
                , typeof(IndexFactory).AssemblyQualifiedName
                , typeof(IndexFileFactory).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNotNull(db);

            db.Load();
        }

        [Test]
        public void CreateCryptoWithKey()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var s = string.Format("filename = {0}; idtoken = {1}; idtype = {2}; entitytype = {3}; seedtype = {4}; formattertype = {5}; crypto = {6}; internalformattertype = {7}; securekey = hobgoblin; assembly = {8}"
                , _testName + ".database"
                , "Id"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , typeof(Seed32).AssemblyQualifiedName
                , typeof(QueryCryptoFormatter).AssemblyQualifiedName
                , typeof(RC2Crypto).AssemblyQualifiedName
                , typeof(BSONFormatter).AssemblyQualifiedName
                , Path.Combine(testAssemblyPath, "BESSy.Tests.dll"));

            var db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNotNull(db);

            db.Load();

            db.Dispose();

            s = string.Format(@"filename = {0}; idtype = {1}; entitytype = {2}; formattertype = {3}; crypto = {4}; internalformattertype = {5}; securekey = hobgoblin; assembly = {6}"
                , _testName + ".database"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , typeof(QueryCryptoFormatter).AssemblyQualifiedName
                , typeof(RC2Crypto).AssemblyQualifiedName
                , typeof(BSONFormatter).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNotNull(db);

            db.Load();
        }

        [Test]
        public void CreateCryptoWithKeyAndCompression()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var s = string.Format("filename = {0}; idtoken = {1}; idtype = {2}; entitytype = {3}; seedtype = {4}; formattertype = {5}; crypto = {6}; internalformattertype = {7}; internalformattertype2 = {8}; securekey = hobgoblin; assembly = {9}"
                , _testName + ".database"
                , "Id"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , typeof(Seed32).AssemblyQualifiedName
                , typeof(QueryCryptoFormatter).AssemblyQualifiedName
                , typeof(RC2Crypto).AssemblyQualifiedName
                , typeof(LZ4ZipFormatter).AssemblyQualifiedName
                , typeof(BSONFormatter).AssemblyQualifiedName
                , Path.Combine(testAssemblyPath, "BESSy.Tests.dll"));

            var db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNotNull(db);

            db.Load();

            db.Dispose();

            s = string.Format(@"filename = {0}; idtype = {1}; entitytype = {2}; formattertype = {3}; crypto = {4}; internalformattertype = {5}; internalformattertype2 = {6}; securekey = hobgoblin; assembly = {7}"
                , _testName + ".database"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , typeof(QueryCryptoFormatter).AssemblyQualifiedName
                , typeof(RC2Crypto).AssemblyQualifiedName
                , typeof(LZ4ZipFormatter).AssemblyQualifiedName
                , typeof(BSONFormatter).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            db = DatabaseFromStringFactory.Create(s) as Database<int, MockClassC>;

            Assert.IsNotNull(db);

            db.Load();
        }


        [Test]
        public void GetsConnectionStringParts()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var s = string.Format(@"filename = {0}; idtoken = {1}; idtype = {2}; entitytype = {3}; seedtype = {4}; binconvertertype = {5}; 
                formattertype = {6}; transactionmanagertype= {7}; filefactorytype ={8}; cachefactorytype={9}; indexfactorytype = {10}; indexfilefactorytype={11}; 
                startingseed = 1000; securekey = hobgoblin;
                internalformattertype = {12}; internalformattertype2 = {13}; crypto = {14}; assembly = {15}"
                , _testName + ".database"
                , "Id"
                , typeof(int).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , typeof(Seed32).AssemblyQualifiedName
                , typeof(BinConverter32).AssemblyQualifiedName
                , typeof(QueryCryptoFormatter).AssemblyQualifiedName
                , typeof(TransactionManager<Int32, MockClassC>).AssemblyQualifiedName
                , typeof(AtomicFileManagerFactory).AssemblyQualifiedName
                , typeof(DatabaseCacheFactory).AssemblyQualifiedName
                , typeof(IndexFactory).AssemblyQualifiedName
                , typeof(IndexFileFactory).AssemblyQualifiedName
                , typeof(LZ4ZipFormatter).AssemblyQualifiedName
                , typeof(BSONFormatter).AssemblyQualifiedName
                , typeof(RC2Crypto).AssemblyQualifiedName
                , Path.Combine(Environment.CurrentDirectory, "BESSy.Tests.dll"));

            var name = DatabaseFromStringFactory.GetFileName(s);

            Assert.AreEqual(name, _testName + ".database");

            var token = DatabaseFromStringFactory.GetIdToken(s);

           Assert.AreEqual(token, "Id");

            var seed = DatabaseFromStringFactory.CreateSeed(s);

            Assert.IsNotNull(seed);

            var converter = DatabaseFromStringFactory.CreateConverter(s);

            Assert.IsNotNull(converter);

            var formatter = DatabaseFromStringFactory.CreateFormatter(s);
                    
            Assert.IsNotNull(formatter);

        }


        [Test]
        public void CreateULongDBWithExternalConverterFormatterAndSerializerSettings()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var s = string.Format(@"filename = {0}; idtoken = {1}; 
                                    idtype = {2}; 
                                    entitytype = {3}; 
                                    seedtype = {4}; 
                                    binconvertertype = {5}; 
                                    formattertype = {6}; 
                                    jsonserializersettings ={7};  
                                    assembly = {8}"
                , _testName + ".database"
                , "BigId"
                , typeof(long).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , "BESSy.TestAssembly.LongSeed, BESSy.TestAssembly"
                , "BESSy.TestAssembly.LongConverter, BESSy.TestAssembly"
                , typeof(BSONFormatter).AssemblyQualifiedName
                , settings
                , Path.Combine(Environment.CurrentDirectory, "BESSy.TestAssembly.dll"));

            var db = DatabaseFromStringFactory.Create(s) as Database<long, MockClassC>;

            Assert.IsNotNull(db);

            Console.WriteLine(s);

            db.Load();

            db.Dispose();

            s = string.Format(@"filename = {0}; idtype = {1}; entitytype = {2}; formattertype = {3}; jsonserializersettings ={4}; assembly = {5}"
                , _testName + ".database"
                , typeof(long).FullName
                , typeof(MockClassC).AssemblyQualifiedName
                , typeof(BSONFormatter).AssemblyQualifiedName
                , settings
                , Path.Combine(Environment.CurrentDirectory, "BESSy.TestAssembly.dll"));

            db = DatabaseFromStringFactory.Create(s) as Database<long, MockClassC>;

            Assert.IsNotNull(db);

            Console.WriteLine(s);

            db.Load();
        }
    }
}
