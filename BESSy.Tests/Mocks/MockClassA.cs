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
    internal class MockClassA
    {
        public int Id { get; set; }
        public virtual string Name { get; set; }
    }

    internal static class Extend
    {
        public static MockClassA WithName(this MockClassA mock, string name)
        {
            mock.Name = name;

            return mock;
        }
    }
}
