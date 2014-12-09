using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace BESSy.TestAssembly
{
    [TestFixture]
    public class GetTypeNames
    {
        [Test]
        public void GetTypeNamesFor()
        {
            var converter = typeof(LongConverter).AssemblyQualifiedName;
            var seed = typeof(LongSeed).AssemblyQualifiedName;

            Assert.IsNotNull(seed);
        }
    }
}
