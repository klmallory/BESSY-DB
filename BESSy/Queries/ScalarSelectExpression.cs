using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Queries
{
    public class ScalarSelectExpression : WhereExpression
    {
        public ScalarSelectExpression(string[] getTokens, params CompareToken[] selectTokens) : base(selectTokens)
        {
            Tokens = getTokens;
        }

        public ScalarSelectExpression(int max, bool first, string[] getTokens, params CompareToken[] selectTokens) : base(max, first, selectTokens)
        {
            Tokens = getTokens;
        }

        public string[] Tokens { get; set; }
    }
}
