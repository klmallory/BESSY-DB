using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Queries
{
    public class QueryExecuteException : SystemException
    {
        private QueryExecuteException() { }
        public QueryExecuteException(string message) : base(message) { }
        public QueryExecuteException(string message, Exception innerException) : base(message, innerException) { }
    }
}
