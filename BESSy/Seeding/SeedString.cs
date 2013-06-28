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
    /// This serves mostly as a pass through place holder for string implementations.
    /// </summary>
    [Serializable]
    public class SeedString : SeedPassive<string>
    {
        public SeedString() : this(50)
        {
            
        }

        public SeedString(int maxLength) : base()
        {
            _maxLen = maxLength;
            Stride = 1024;
        }

        [JsonProperty]
        int _maxLen { get; set; }
    }
}
