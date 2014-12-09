using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using BESSy.Extensions;

namespace BESSy.Tests.OddBallTests
{
    [TestFixture]
    public class ExtensionTests : FileTest
    {
        [Test]
        public void ClampTests()
        {
            Assert.AreEqual(10, long.MaxValue.Clamp(0, 10));
            Assert.AreEqual(0, ((long)(-1)).Clamp(0, 10));
            Assert.AreEqual(1, ((long)(1)).Clamp(0, 10));

            Assert.AreEqual(10, int.MaxValue.Clamp(0, 10));
            Assert.AreEqual(0, (-1).Clamp(0, 10));
            Assert.AreEqual(1, 1.Clamp(0, 10));

            Assert.AreEqual(10, byte.MaxValue.Clamp(0, 10));
            Assert.AreEqual(5, ((byte)(1)).Clamp(5, 10));
            Assert.AreEqual(6, ((byte)(6)).Clamp(5, 10));

            Assert.AreEqual(10, double.MaxValue.Clamp(0, 10));
            Assert.AreEqual(0, ((double)(-1)).Clamp(0, 10));
            Assert.AreEqual(1, ((double)(1)).Clamp(0, 10));
        }
    }
}
