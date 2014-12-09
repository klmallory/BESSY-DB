using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Queries
{
    public struct UpdateToken
    {
        [System.Runtime.TargetedPatchingOptOut("Performance Critical Accross NGen Boundaries")]
        public UpdateToken(string setToken, object value)
            : this()
        {
            SetToken = setToken;
            Value = value;
            ValueType = ValueTypeEvaluator.GetValueTypeFor(value);
        }

        [System.Runtime.TargetedPatchingOptOut("Performance Critical Accross NGen Boundaries")]
        public UpdateToken(string setToken, object value, ValueEnum valueType)
            : this()
        {
            SetToken = setToken;
            Value = value;
            ValueType = valueType;
        }

        public string SetToken { get; set; }
        public object Value { get; set; }
        public ValueEnum ValueType { get; set; }
    }
}
