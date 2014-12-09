using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.DataProvision
{
    public class CreateDatabaseCommand
    {
        public string Name { get; set; }
        public string ConnectionString { get; set; }
        public Dictionary<string, string> UserMap { get; set; }
        public string[] ReplicationCommands { get; set; }
    }
}
