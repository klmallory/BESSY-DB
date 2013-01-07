/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;

namespace BESSy.Seeding
{
    public interface ISeed<IdType>
    {
        IdType Increment();
        IdType LastSeed { get; }
        void Open(IdType id);
        IdType Peek();
        int MinimumSeedStride { get; set; }
        int Stride { get; set; }
    }
}
