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
    public class MockClassC : MockClassB
    {
        public string ReferenceCode { get; set; }
        public MockStruct Location { get; set; }
        IDictionary<int, MockClassD> Ds { get; set; }
    }
}
