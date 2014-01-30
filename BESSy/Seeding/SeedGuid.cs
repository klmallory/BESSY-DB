/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Seeding
{
    public sealed class SeedGuid : Seed<Guid>
    {
        public SeedGuid() : base()
        {
            MinimumSeedStride = 10240;
        }

        public override Guid Increment()
        {
            return Guid.NewGuid();
        }

        public override Guid Peek()
        {
            return Guid.NewGuid();
        }
    }
}
