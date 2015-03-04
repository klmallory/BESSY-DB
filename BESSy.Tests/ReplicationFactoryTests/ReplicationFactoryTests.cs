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
using BESSy.Replication.Tcp;
using System.Net.Sockets;

namespace BESSy.Tests.DatabaseFactoryTests
{
    [TestFixture]
    public class ReplicationFactoryTests : FileTest
    {
        string testAssemblyPath = Path.Combine(Environment.CurrentDirectory, @"..\..\..\Tools\");
        string settings = JsonConvert.SerializeObject(BSONFormatter.GetDefaultSettings(), JSONFormatter.GetDefaultSettings());

        [Test]
        public void FactoryCreatesTcpPublisherSimple()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var port = new Random().Next(8355, 25000);
            var s = string.Format(
                "publishertype = {0}; ipaddress = {1}; port = {2}",
                typeof(TcpTransactionPublisher<int, MockClassA>).AssemblyQualifiedName,
                "127.0.0.1",
                port);

            var publisher = ReplicationFromStringFactory.Create(s);

            Assert.IsNotNull(publisher);

            Console.WriteLine(s);
        }


        [Test]
        public void FactoryCreatesTcpPublisherSimpleWithInterval()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var port = new Random().Next(8355, 25000);
            var s = string.Format(
                "publishertype = {0}; ipaddress = {1}; port = {2}; interval = {3}; ",
                typeof(TcpTransactionPublisher<int, MockClassA>).AssemblyQualifiedName,
                "127.0.0.1",
                port, 1);

            var publisher = ReplicationFromStringFactory.Create(s);

            Assert.IsNotNull(publisher);

            Console.WriteLine(s);
        }

        [Test]
        public void FactoryCreatesTcpPublisherNotSoSimple()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var port = new Random().Next(8355, 25000);
            var s = string.Format(
                @"publishertype = {0}; ipaddress = {1}; port = {2}; interval = {3}; 
                    sendtimeout = {4}; receivetimeout = {5}; nodelay = {6}; exclusiveaddress = {7}; sendbuffersize = {8}; receivebuffersize = {9}; linger = {10}; 
                    formattertype = {11}; crypto = {12}; internalformattertype = {13}; internalformattertype2 = {14}; securekey = hobgoblin; assembly = {15}",
                typeof(TcpTransactionPublisher<int, MockClassA>).AssemblyQualifiedName,
                "127.0.0.1", port, 1,
                3000, 3000, true, true, 50000, 50000, true,
                typeof(QueryCryptoFormatter).AssemblyQualifiedName,
                typeof(RC2Crypto).AssemblyQualifiedName,
                typeof(LZ4ZipFormatter).AssemblyQualifiedName,
                typeof(BSONFormatter).AssemblyQualifiedName,
                Path.Combine(testAssemblyPath, "BESSy.Tests.dll"));

            var publisher = ReplicationFromStringFactory.Create(s);

            Assert.IsNotNull(publisher);

            Console.WriteLine(s);
        }


        [Test]
        public void FactoryCreatesTcpSubscriberSimple()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var port = new Random().Next(8355, 25000);
            var s = string.Format(
                "subscribertype = {0}; port = {1}",
                typeof(TcpTransactionSubscriber<int, MockClassA>).AssemblyQualifiedName,
                port);

            var subscriber = ReplicationFromStringFactory.Create(s);

            Assert.IsNotNull(subscriber);

            Console.WriteLine(s);
        }

        [Test]
        public void FactoryCreatesTcpSubscriberNotSoSimple()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var port = new Random().Next(8355, 25000);
            var s = string.Format(
                @"subscribertype = {0}; port = {1}; exclusiveaddress = {2}; dontfragment = {3}; linger = {4}; lingertime = {5};
                formattertype = {6}; crypto = {7}; internalformattertype = {8}; internalformattertype2 = {9}; securekey = hobgoblin; assembly = {10}",
                typeof(TcpTransactionSubscriber<int, MockClassA>).AssemblyQualifiedName,
                port,
                 true, true, true, 30,
                typeof(QueryCryptoFormatter).AssemblyQualifiedName,
                typeof(RC2Crypto).AssemblyQualifiedName,
                typeof(LZ4ZipFormatter).AssemblyQualifiedName,
                typeof(BSONFormatter).AssemblyQualifiedName,
                Path.Combine(testAssemblyPath, "BESSy.Tests.dll"));

            var subscriber = ReplicationFromStringFactory.Create(s);

            Assert.IsNotNull(subscriber);

            Console.WriteLine(s);
        }

        [Test]
        [ExpectedException(typeof(ReplicationFactoryException))]
        public void FactoryCreatesTcpPublisherFailure()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var port = new Random().Next(8355, 25000);
            var s = string.Format(
                @"publishertype = {0}; nodelay = {1}",
                typeof(TcpTransactionPublisher<int, MockClassA>).AssemblyQualifiedName,
                true);

            var subscriber = ReplicationFromStringFactory.Create(s);

            Assert.IsNull(subscriber);
        }

        [Test]
        [ExpectedException(typeof(ReplicationFactoryException))]
        public void FactoryCreatesTcpSubscriberFailure()
        {
            _testName = MethodInfo.GetCurrentMethod().Name.GetHashCode().ToString();
            Cleanup();

            var port = new Random().Next(8355, 25000);
            var s = string.Format(
                @"subscribertype = {0}; nodelay = {1}",
                typeof(TcpTransactionSubscriber<int, MockClassA>).AssemblyQualifiedName,
                true);

            var subscriber = ReplicationFromStringFactory.Create(s);

            Assert.IsNull(subscriber);
        }
    }
}
