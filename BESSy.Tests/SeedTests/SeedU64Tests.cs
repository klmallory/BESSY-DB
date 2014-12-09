using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using BESSy.Seeding;
using System.Threading.Tasks;

namespace BESSy.Tests.SeedTests
{
    [TestFixture]
    public class SeedU64Tests : FileTest
    {
        [Test]
        public void Seed64StartsAtOne()
        {
            var seed = new SeedU64();

            Assert.AreEqual(1, seed.Peek());

            Assert.AreEqual(1, seed.Increment());

            Assert.AreEqual(1, seed.LastSeed);
        }

        [Test]
        public void Seed64StartsWithParameter()
        {
            var seed = new SeedU64(999);

            Assert.AreEqual(1000, seed.Peek());

            Assert.AreEqual(1000, seed.Increment());

            Assert.AreEqual(1000, seed.LastSeed);
        }

        [Test]
        public void Seed64OpensUnused()
        {
            var seed = new SeedU64(999);

            Assert.AreEqual(1000, seed.Increment());

            seed.Open(500);

            Assert.AreEqual(500, seed.Peek());

            Assert.AreEqual(500, seed.Increment());

            Assert.AreEqual(1000, seed.LastSeed);
        }

        [Test]
        public void Seed64IsThreadSafe()
        {
            var seed = new SeedU64(999);

            var ids = new List<ulong>();

            seed.Open(20000);

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
