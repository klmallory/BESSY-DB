/*
Copyright (c) 2011,2012,2013 Kristen Mallory dba Klink

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace BESSy.Queries
{
    public abstract class Expression
    {
        public string Query { get; set; }
        public abstract Type GetValueType();
    }

    public abstract class Expression<ExpressionType> : Expression
    {
        public Expression() { }
        
        public Expression(string query, ExpressionType value)
        {
            Query = query;
            Value = value;
        }

        public override Type GetValueType() { return typeof(ExpressionType); }
        public ExpressionType Value { get; set; }
    }

    public abstract class EvaluateExpression<ExpressionType> : Expression<ExpressionType>
    {
        public bool Evaluate(JObject entity)
        {
            return object.Equals(entity.SelectToken(Query).Value<ExpressionType>(), Value);
        }
    }

    public class Equals<ExpressionType> : Expression<ExpressionType> { }
    public class GreaterThan<ExpressionType> : Expression<ExpressionType> { }
    public class GreaterThanEquals<ExpressionType> : Expression<ExpressionType> { }
    public class LessThan<ExpressionType> : Expression<ExpressionType> { }
    public class LessThanEquals<ExpressionType> : Expression<ExpressionType> { }
    public class NotEquals<ExpressionType> : Expression<ExpressionType> { }
    public class Contains<ExpressionType> : Expression<ExpressionType> { }
    public class In<ExpressionType> : Expression<ExpressionType[]> { }
    public class Set<ExpressionType> : Expression<ExpressionType> { }
}
