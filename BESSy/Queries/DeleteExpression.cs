using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Queries
{
    public class DeleteExpression : WhereExpression
    {
        public DeleteExpression(params CompareToken[] selectTokens)
            : base(selectTokens)
        {
            
        }

        public DeleteExpression(int max, bool first, params CompareToken[] selectTokens)
        {
            SelectTokens = selectTokens;

            if (first)
                First = max;
            else
                Last = max;
        }
    }
}
