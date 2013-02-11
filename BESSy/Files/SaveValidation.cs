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
        public int NewRowSize;
        public int NewDatabaseSize;
    }

    public struct CommitFailureInfo<IdType, EntityType>
    {
        public ITransaction<IdType, EntityType> Transaction;
        public int NewRowSize;
        public int NewDatabaseSize;
    }
}
