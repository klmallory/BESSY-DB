using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Queries
{
    public struct CompareToken
    {
        [System.Runtime.TargetedPatchingOptOut("Performance Critical Accross NGen Boundaries")]
        public CompareToken(string selectToken, CompareEnum compareType, object value)
            : this()
        {
            SelectToken = selectToken;
            CompareType = compareType;
            Value = value;
            ValueType = ValueTypeEvaluator.GetValueTypeFor(value);
        }

        [System.Runtime.TargetedPatchingOptOut("Performance Critical Accross NGen Boundaries")]
        public CompareToken(string selectToken, CompareEnum compareType, object value, ValueEnum valueType)
            : this()
        {
            SelectToken = selectToken;
            CompareType = compareType;
            Value = value;
            ValueType = valueType;
        }

        public string SelectToken { get; set; }
        public CompareEnum CompareType { get; set; }
        public object Value { get; set; }
        public ValueEnum ValueType { get; set; }
    }
}
