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
using Newtonsoft.Json;
using NUnit.Framework;

namespace BESSy.Tests.SeedTests
{
    [TestFixture]
    public class SeedGuidTests
    {
        [Test]
        public void PeekReturnsEmptyGuid()
        {
            var seed = new SeedGuid();

            Assert.AreEqual(Guid.Empty, seed.Peek());
        }

        [Test]
        public void IncrementReturnsNonEmptyGuid()
        {
            var seed = new SeedGuid();
            
            Assert.AreNotEqual(Guid.Empty, seed.Increment());

            seed.Open(seed.Increment());
        }

        [Test]
        public void OpenDoesNotMarkValue()
        {
            var seed = new SeedGuid();

            Assert.AreNotEqual(Guid.Empty, seed.Increment());

            var value = seed.Increment();

            seed.Open(value);

            Assert.AreNotEqual(value, seed.Increment());
        }
    }
}
