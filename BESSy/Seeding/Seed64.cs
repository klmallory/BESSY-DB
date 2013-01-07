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
    public sealed class Seed64 : Seed<Int64>
    {
        public Seed64() : this(0) { }

        public Seed64(long startingSeed)
        {
            LastSeed = startingSeed;
        }

        public override long Increment()
        {
            lock (_syncRoot)
            {
                if (OpenIds.Count > 0)
                {
                    var id = OpenIds[0];
                    OpenIds.RemoveAt(0);
                    return id;
                }

                LastSeed++;

                return LastSeed;
            }
        }

        public override long Peek()
        {
            if (OpenIds.Count > 0)
                return OpenIds[0];

            return LastSeed + 1;
        }
    }
}
