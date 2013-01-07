using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy
{
    public class UniqueKeyViolationException<IdType> : ApplicationException
    {
        public UniqueKeyViolationException(IdType id, string repositoryName) :
            base (string.Format("The key {0}, already exists in this repository {1}.", id, repositoryName))
        {

        }
    }
}
