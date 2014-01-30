/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Reflection;
using BESSy.Serialization.Converters;

namespace BESSy.Seeding
{
    public interface ISeed<IdType>
    {
        Guid Source { get; }
        IdType Increment();
        IdType LastSeed { get; }
        void Open(IdType id);
        IdType Peek();
        ISeed<int> SegmentSeed { get; set; }
        object PropertyConverter { get; set; }
        object IdConverter { get; set; }
        string IdProperty { get; set; }
        string CategoryIdProperty { get; set; }
        int MinimumSeedStride { get; set; }
        int Stride { get; set; }
        long LastReplicatedTimeStamp { get; set; }
    }
}
