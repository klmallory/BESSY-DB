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
        bool Passive { get; }
        IdType Increment();
        IdType LastSeed { get; }
        void Open(IdType id);
        IdType Peek();
    }
}
