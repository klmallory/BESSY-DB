using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Transactions;

namespace BESSy.Files
{
    public struct SaveFailureInfo<EntityType>
    {
        public EntityType Entity;
        public int Segment;
        public int NewRowSize;
        public int NewDatabaseSize;
    }

    public struct CommitFailureInfo<EntityType>
    {
        public object Transaction;
        public object Segments;
        public int NewRowSize;
        public int NewDatabaseSize;
    }
}
