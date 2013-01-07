/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Tests.Mocks
{
    internal class MockClassB : MockClassA
    {
        public Guid ReplicationID { get; set; }
        public double[] GetSomeCheckSum { get; set; }
    }
}
