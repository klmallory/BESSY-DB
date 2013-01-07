/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace BESSy.Seeding
{
    public abstract class Seed<IdType> : ISeed<IdType>
    {
        private static readonly int Hint_Limit = 10000;

        public Seed()
        {
            OpenIds = new List<IdType>();
            Stride = 8096;
        }

        public Seed(IdType startingSeed)
            : this()
        {
            LastSeed = startingSeed;
        }

        protected object _syncRoot = new object();

        [JsonProperty]
        protected List<IdType> OpenIds { get; set; }

        [JsonProperty]
        public IdType LastSeed { get; protected set; }

        public int MinimumSeedStride { get; set; }
        public int Stride { get; set; }

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
