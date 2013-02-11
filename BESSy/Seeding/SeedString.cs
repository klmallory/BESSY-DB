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
    /// <summary>
    /// This serves mostly as a pass through place holder
    /// </summary>
    [Serializable]
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

        public object PropertyConverter { get; set; }
        public object IdConverter { get; set; }
        public string GetIdMethod { get; set; }
        public string SetIdMethod { get; set; }
        public string GetCategoryIdMethod { get; set; }
        public int MinimumSeedStride { get; set; }
        public int Stride { get; set; }
    }
}
