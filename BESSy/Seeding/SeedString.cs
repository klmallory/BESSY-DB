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
    /// <summary>
    /// This serves mostly as a pass through place holder
    /// </summary>
    public class SeedString : ISeed<String>
    {
        public SeedString() : this(50)
        {
            
        }

        public SeedString(int maxLength)
        {
            _maxLen = maxLength;
            Stride = 1024;
        }

        [JsonProperty]
        int _maxLen { get; set; }

        string emptyString = string.Empty;

        public string Increment()
        {
            return emptyString;
        }

        public string LastSeed
        {
            get { return emptyString; }
        }

        public void Open(string id)
        {
            //do nothing.
        }

        public string Peek()
        {
            return emptyString;
        }

        public int MinimumSeedStride { get; set; }
        public int Stride { get; set; }
    }
}
