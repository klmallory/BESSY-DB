using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Json;
using BESSy.Seeding;
using BESSy.Extensions;

namespace BESSy.Tests.Mocks
{
    public class MockSeed32 : ISeed<int>
    {
        private static readonly int Hint_Limit = 10000;

        public MockSeed32()
        {
            Passive = true;
        }

        object _syncRoot = new object();

        [JsonProperty]
        private List<int> MatterIds { get; set; }

        [JsonProperty]
        private List<int> OpenIds { get; set; }

        [JsonProperty]
        public int LastSeed { get; set; }

        [JsonProperty]
        public bool Passive { get; set; }

        public void Open(int id)
        {
            if (!OpenIds.Contains(id) && OpenIds.Count < Hint_Limit)
                OpenIds.Add(id);

            lock (_syncRoot)
                if (MatterIds.Contains(id))
                    MatterIds.Remove(id);
        }

        public int Peek()
        {
            return LastSeed + 1;
        }

        public int Increment()
        {
            if (OpenIds.IsNotNullAndNotEmpty())
            {
                lock (_syncRoot)
                {
                    var id = OpenIds.First();

                    OpenIds.RemoveAll(i => i == id);

                    return id;
                }
            }

            lock (_syncRoot)
            {
                LastSeed += 1;

                return LastSeed;
            }
        }
    }
}
