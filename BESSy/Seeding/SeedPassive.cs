using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Json;

namespace BESSy.Seeding
{
    /// <summary>
    /// This serves mostly as a pass through place holder for custom passive implementations.
    /// </summary>
    [Serializable]
    public class SeedPassive<T> : ISeed<T>
    {
        public SeedPassive()
        {
            MinimumSeedStride = 1024;
            Source = Guid.NewGuid();
        }

        public T Increment()
        {
            return default(T);
        }

        public T LastSeed
        {
            get { return default(T); }
        }

        public void Open(T id)
        {
            //do nothing.
        }

        public T Peek()
        {
            return default(T);
        }

        [JsonProperty]
        public Guid Source { get; protected set; }

        public ISeed<int> SegmentSeed { get; set; }
        public long LastReplicatedTimeStamp { get; set; }
        public object PropertyConverter { get; set; }
        public object IdConverter { get; set; }
        public string IdProperty { get; set; }
        public string CategoryIdProperty { get; set; }
        public int MinimumSeedStride { get; set; }
        public int Stride { get; set; }
    }
}
