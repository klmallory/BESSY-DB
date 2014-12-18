/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Permissions;
using System.Security;

namespace BESSy.Tests.Mocks
{
    [SecuritySafeCritical]
    public class MockClassB : MockClassA
    {
        public DateTime MyDate { get; set; }
        public decimal DecAnimal { get; set; }
        public double[] GetSomeCheckSum { get; set; }
    }
}
