/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
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
            Stride = 512;
            MinimumSeedStride = 10240;
            OpenIds = new List<IdType>();
            Source = Guid.NewGuid();
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
        public Guid Source { get; protected set; }

        [JsonProperty]
        public IdType LastSeed { get; protected set; }

        public long LastReplicatedTimeStamp { get; set; }
        public object PropertyConverter { get; set; }
        public object IdConverter { get; set; }

        public string IdProperty { get; set; }
        public string CategoryIdProperty { get; set; }

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
