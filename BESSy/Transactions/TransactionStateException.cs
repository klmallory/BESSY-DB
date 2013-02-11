using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Transactions
{
    public class TransactionStateException : SystemException
    {
        public TransactionStateException() : base() { }
        public TransactionStateException(string message) : base(message) { }
        public TransactionStateException(string message, Exception innerException) : base(message, innerException) { }
    }
}
