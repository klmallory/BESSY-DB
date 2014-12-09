using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Queries
{
    public static class ValueTypeEvaluator
    {
        public static ValueEnum GetValueTypeFor(object value)
        {
            if (value == null)
                return ValueEnum.Null;

            if (value is Int64)
                return ValueEnum.Long;
            if (value is UInt64)
                return ValueEnum.ULong;

            if (value is Int32)
                return ValueEnum.Int;
            if (value is UInt32)
                return ValueEnum.UInt;

            if (value is Int16)
                return ValueEnum.SmallInt;
            if (value is UInt16)
                return ValueEnum.USmallInt;

            if (value is byte)
                return ValueEnum.Byte;

            if (value is decimal)
                return ValueEnum.Decimal;
            if (value is double)
                return ValueEnum.Double;
            if (value is float)
                return ValueEnum.Float;

            if (value is string)
                return ValueEnum.String;
            if (value is DateTime)
                return ValueEnum.DateTime;

            if (value is Guid)
                return ValueEnum.Guid;

            throw new QueryExecuteException(string.Format("Can not evaluate tBuilder of {0}, use only simple types in query tokens", value.GetType()));
        }
    }
}
