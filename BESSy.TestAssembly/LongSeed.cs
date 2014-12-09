using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Seeding;

namespace BESSy.TestAssembly
{
    public class LongSeed : Seed<long>
    {
        public override long Increment()
        {
            return ++LastSeed;
        }

        public override long Peek()
        {
            return LastSeed + 1;
        }
    }
}
