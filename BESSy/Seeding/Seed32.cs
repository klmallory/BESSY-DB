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
    public sealed class Seed32 : Seed<Int32>
    {
        public Seed32() : this(0) { }

        public Seed32(int startingSeed)
        {
            LastSeed = startingSeed;
        }

        public override int Increment()
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

        public override int Peek()
        {
            if (OpenIds.Count > 0)
                return OpenIds[0];

            return LastSeed + 1;
        }
    }
}
