using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using BESSy.Files;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Tests.Mocks;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BESSy.Tests.SeedTests
{
    [TestFixture]
    public class TypeFactoryTests
    {
        [Test]
        public void TestTypeFactoryInt32Test()
        {
            var bin = TypeFactory.GetBinConverterFor<Int32>();

            Assert.IsInstanceOf<BinConverter32>(bin);

            var seed = TypeFactory.GetSeedFor<Int32>();

            Assert.IsInstanceOf<Seed32>(seed);
        }

        [Test]
        public void TestTypeFactoryInt64Test()
        {
            var bin = TypeFactory.GetBinConverterFor<Int64>();

            Assert.IsInstanceOf<BinConverter64>(bin);

            var seed = TypeFactory.GetSeedFor<Int64>();

            Assert.IsInstanceOf<Seed64>(seed);
        }

        [Test]
        public void TestTypeFactoryGuidTest()
        {
            var bin = TypeFactory.GetBinConverterFor<Guid>();

            Assert.IsInstanceOf<BinConverterGuid>(bin);

            var seed = TypeFactory.GetSeedFor<Guid>();

            Assert.IsInstanceOf<SeedGuid>(seed);
        }

        [Test]
        public void TestTypeFactoryStringTest()
        {
            var bin = TypeFactory.GetBinConverterFor<String>();

            Assert.IsInstanceOf<BinConverterString>(bin);

            var seed = TypeFactory.GetSeedFor<String>();

            Assert.IsInstanceOf<SeedString>(seed);
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void TestTypeFactoryBinThrowsAnExceptionForUnkownTypes()
        {
            var bin = TypeFactory.GetBinConverterFor<decimal>();
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void TestTypeFactorySeedThrowsAnExceptionForUnkownTypes()
        {
            var bin = TypeFactory.GetSeedFor<decimal>();
        }
    }
}
