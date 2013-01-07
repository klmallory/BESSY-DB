using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy
{
    public class DuplicateKeyException : ApplicationException
    {
        public DuplicateKeyException(object key) : base(string.Format("Key already exists: {0}", key)) { }

        public DuplicateKeyException(object key, object entity) : base(string.Format("Key '{0}' already exists for '{1}'", key, entity)) { }
    }
}
