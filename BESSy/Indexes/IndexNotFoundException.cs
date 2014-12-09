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
}
