using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Synchronization
{
    public class RowLockTimeoutException : SystemException
    {
        public RowLockTimeoutException(string message) : base(message) { }
    }
}
