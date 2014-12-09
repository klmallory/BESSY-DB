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
    public sealed class SeedU64 : Seed<UInt64>
    {
        public SeedU64() : base() {  }

        public SeedU64(ulong startingSeed) : base(startingSeed)
        {
        }

        public override ulong Increment()
        {
            lock (_syncRoot)
            {
                if (OpenIds.Count > 0 && OpenIds[0] > 0)
                {
                    var id = OpenIds[0];
                    OpenIds.RemoveAt(0);
                    return id;
                }

                LastSeed++;

                return LastSeed;
            }
        }

        public override void Open(ulong id)
        {
            if (id <= 0 || id > LastSeed)
                return;

            base.Open(id);
        }

        public override ulong Peek()
        {
            if (OpenIds.Count > 0)
                return OpenIds[0];

            return LastSeed + 1;
        }
    }
}
