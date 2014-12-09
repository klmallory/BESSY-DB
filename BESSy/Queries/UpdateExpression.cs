using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Json.Linq;
using BESSy.Json;

namespace BESSy.Queries
{
    public class UpdateExpression
    {
        public UpdateExpression(string typeName, WhereExpression selector, params UpdateToken[] updateTokens)
        {
            TypeName = typeName;
            Selector = selector;
            UpdateTokens = updateTokens;
        }

        [JsonProperty]
        public string TypeName { get; protected set; }

        [JsonProperty]
        public UpdateToken[] UpdateTokens { get; protected set; }

        [JsonProperty]
        public WhereExpression Selector { get; protected set; }
    }
}
