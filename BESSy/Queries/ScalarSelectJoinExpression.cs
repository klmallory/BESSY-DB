using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Json;

namespace BESSy.Queries
{
    public class ScalarSelectJoinExpression : ScalarSelectExpression
    {
        
        public ScalarSelectJoinExpression(string[] getTokens, params CompareToken[] selectTokens)
            : base(getTokens, selectTokens)
        {
            SplitBinders(getTokens, selectTokens);

            GetBinders = new Dictionary<string, List<string>>();
            CompareBinders = new Dictionary<string, List<CompareToken>>();
        }

        public ScalarSelectJoinExpression(int max, bool first, string[] getTokens, params CompareToken[] selectTokens)
            : base(max, first, getTokens, selectTokens)
        {
            SplitBinders(getTokens, selectTokens);

            GetBinders = new Dictionary<string, List<string>>();
            CompareBinders = new Dictionary<string, List<CompareToken>>();
        }

        void SplitBinders(string[] getTokens, params CompareToken[] selectTokens)
        {
            foreach (var token in getTokens)
            {
                if (token.Contains('\\'))
                {
                    foreach (var split in token.Split('\\'))
                        if (split.Contains('.'))
                        {
                            var binder = split.Split('.').First();
                            var index = split.IndexOf('.');
                            if (GetBinders.ContainsKey(binder))
                                GetBinders[binder].Add(split.Substring(index, split.Length - index));
                            else
                                GetBinders.Add(binder, new List<string>() { split.Substring(index, split.Length - index) });
                        }
                        else
                        {
                            if (GetBinders.ContainsKey(split))
                                GetBinders[split] = new List<string>();
                            else
                                GetBinders.Add(split, new List<string>());
                        }
                }
                else
                    GetBinders.Add("", new List<string>() { token });
            }

            foreach (var selector in selectTokens)
            {
                if (string.IsNullOrEmpty(selector.SelectToken))
                    throw new QueryExecuteException("Select Token was null or empty");

                if (selector.SelectToken.Contains('\\'))
                {
                    foreach (var split in selector.SelectToken.Split('\\'))
                        if (split.Contains('.'))
                        {
                            var binder = split.Split('.').First();
                            var index = split.IndexOf('.');

                            if (CompareBinders.ContainsKey(binder))
                                CompareBinders[binder].Add(
                                    new CompareToken(split.Substring(index, split.Length - index),
                                        selector.CompareType, selector.Value, selector.ValueType));
                            else
                                CompareBinders.Add(binder, new List<CompareToken>() 
                                { new CompareToken(split.Substring(index, split.Length - index), 
                                        selector.CompareType, selector.Value, selector.ValueType) });
                        }
                }
                else
                    CompareBinders.Add("", new List<CompareToken>() { selector });
            }
        }

        public Dictionary<string, List<string>> GetBinders { get; set; }

        public Dictionary<string, List<CompareToken>> CompareBinders { get; set; }

    }
}
