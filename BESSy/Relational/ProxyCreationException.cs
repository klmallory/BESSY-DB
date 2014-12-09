using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace BESSy.Relational
{
    public class ProxyCreationException : ApplicationException
    {
        public ProxyCreationException() : base(){}
        public ProxyCreationException(string message) : base(message) { }
        public ProxyCreationException(string message, Exception inner) : base(message, inner) { }
        public ProxyCreationException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
