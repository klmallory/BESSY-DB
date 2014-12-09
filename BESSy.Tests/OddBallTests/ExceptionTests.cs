using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using BESSy.Indexes;

namespace BESSy.Tests.OddBallTests
{
    [TestFixture]
    public class ExceptionTests
    {
        [Test]
        [ExpectedException(typeof(IndexNotFoundException))]
        public void TestIndexException()
        {
            throw new IndexNotFoundException("this is a test");
        }

        [Test]
        [ExpectedException(typeof(IndexNotFoundException))]
        public void TestIndexExceptionWithInnerException()
        {
            throw new IndexNotFoundException("this is a test", new Exception());
        }
    }
}
