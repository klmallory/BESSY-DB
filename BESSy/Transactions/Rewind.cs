using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace BESSy.Transactions
{
    public class Rewind<IdType, EntityType>
    {
        public IdType Id { get; set; }
        public int Segment { get; set; }
        public Stream Buffer { get; set; }
    }
}
