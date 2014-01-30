using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Indexes
{
    public class IndexNotFoundException : ApplicationException
    {
        public IndexNotFoundException(string message) : base(message) { }
        public IndexNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class IndexLoadFoundException : ApplicationException
    {
        public IndexLoadFoundException(string message) : base(message) { }
        public IndexLoadFoundException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class IndexUnLoadFoundException : ApplicationException
    {
        public IndexUnLoadFoundException(string message) : base(message) { }
        public IndexUnLoadFoundException(string message, Exception innerException) : base(message, innerException) { }
    }
}
