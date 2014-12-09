using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Replication
{
    public class AuthPackage
    {
        public Guid SourceId { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
    }
}
