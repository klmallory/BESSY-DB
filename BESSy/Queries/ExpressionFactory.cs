using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Queries
{
    public class ExpressionFactory
    {
        public IList<ExpressionGroup> CreateFrom(string query)
        {
            query = query.ToLower();

            var ands = query.Split(new string[] {" and "}, StringSplitOptions.RemoveEmptyEntries);
            var grouped = ands.Select(a => a.Split(new string[] {" or "}, StringSplitOptions.RemoveEmptyEntries));

            List<ExpressionGroup> expressions = new List<ExpressionGroup>();

            foreach (var group in grouped)
                expressions.Add(Create(group));

            return expressions;
        }

        ExpressionGroup Create(string[] group)
        {
            ExpressionGroup expressions = new ExpressionGroup();

            foreach (var statement in group)
            {
                if (statement.IndexOf(" = ") >= 0)
                    expressions.Add(CreateSet(statement));
                else if (statement.IndexOf(" == ") >= 0)
                    expressions.Add(CreateEquals(statement));
                else if (statement.IndexOf(" contains ") >= 0)
                    expressions.Add(CreateContains(statement));
                else if (statement.IndexOf(" in ") >= 0)
                    expressions.Add(CreateIn(statement));
            }

            return expressions;
        }

        Expression CreateContains(string statement)
        {
            throw new NotImplementedException();
        }

        Expression CreateIn(string statement)
        {
            throw new NotImplementedException();
        }

        Expression CreateEquals(string query)
        {
            throw new NotImplementedException();
        }

        Expression CreateSet(string query)
        {
            throw new NotImplementedException();
        }
    }
}
