using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Json;

namespace BESSy.Queries
{
    public enum CompareEnum
    {
        None = 0,
        Equals,
        NotEquals,
        Greater,
        Lesser,
        GreaterOrEqual,
        LesserOrEqual,
        Like
    }

    public class WhereExpression
    {

        public WhereExpression(params CompareToken[] selectTokens)
        {
            SelectTokens = selectTokens;
        }

        public WhereExpression(int max, bool first, params CompareToken[] selectTokens)
        {
            SelectTokens = selectTokens;

            if (first)
                First = max;
            else
                Last = max;
        }

        [JsonProperty]
        public CompareToken[] SelectTokens { get; protected set; }

        [JsonProperty]
        public int First { get; protected set; }

        [JsonProperty]
        public int Last { get; protected set; }
    }
}
