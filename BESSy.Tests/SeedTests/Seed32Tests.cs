using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BESSy.Files;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using BESSy.Json;
using NUnit.Framework;

namespace BESSy.Tests.SeedTests
{
    [TestFixture]
    public class Seed32Tests
    {
        [Test]
        public void Seed32StartsAtOne()
        {
            var seed = new Seed32();

            Assert.AreEqual(1, seed.Peek());

            Assert.AreEqual(1, seed.Increment());

            Assert.AreEqual(1, seed.LastSeed);
        }

        [Test]
        public void Seed32StartsWithParameter()
        {
            var seed = new Seed32(999);

            Assert.AreEqual(1000, seed.Peek());

            Assert.AreEqual(1000, seed.Increment());

            Assert.AreEqual(1000, seed.LastSeed);
        }

        [Test]
        public void Seed32OpensUnused()
        {
            var seed = new Seed32(999);

            Assert.AreEqual(1000, seed.Increment());

            seed.Open(500);

            Assert.AreEqual(500, seed.Peek());

            Assert.AreEqual(500, seed.Increment());

            Assert.AreEqual(1000, seed.LastSeed);
        }

        [Test]
        public void Seed32IsThreadSafe()
        {
            var seed = new Seed32(999);

            var ids = new List<int>();

            Parallel.For(0, 50, delegate(int i)
            {
                ids.Add(seed.Increment());
            });

            Assert.AreEqual(50, ids.Distinct().Count());

            Assert.AreEqual(1050, seed.Increment());

            Assert.AreEqual(1050, seed.LastSeed);
        }
    }
}
