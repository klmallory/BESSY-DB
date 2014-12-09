/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Json;
using System.Reflection;
using BESSy.Serialization.Converters;

namespace BESSy.Seeding
{
    [Serializable]
    public abstract class Seed<IdType> : ISeed<IdType>
    {
        private static readonly int Hint_Limit = 10000;

        public Seed()
        {
            OpenIds = new List<IdType>();
        }

        public Seed(IdType startingSeed)
            : this()
        {
            LastSeed = startingSeed;
            Passive = false;
        }

        protected object _syncRoot = new object();

        [JsonProperty]
        protected List<IdType> OpenIds { get; set; }

        [JsonProperty]
        public IdType LastSeed { get; protected set; }

        [JsonProperty]
        public bool Passive { get; protected set; }

        public virtual void Open(IdType id)
        {
            lock (_syncRoot)
                if (!OpenIds.Contains(id) && OpenIds.Count < Hint_Limit)
                    OpenIds.Add(id);
        }

        public abstract IdType Increment();
        public abstract IdType Peek();
    }
}
