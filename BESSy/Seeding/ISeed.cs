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
        IdType Increment();
        IdType LastSeed { get; }
        void Open(IdType id);
        IdType Peek();
        object PropertyConverter { get; set; }
        object IdConverter { get; set; }
        string GetIdMethod { get; set; }
        string SetIdMethod { get; set; }
        string GetCategoryIdMethod { get; set; }
        int MinimumSeedStride { get; set; }
        int Stride { get; set; }
    }
}
