using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Transactions;

namespace BESSy.Replication.Tcp
{
    public abstract class TcpSecurityPackage
    {
        public TcpSecurityPackage(Guid authToken)
        {
            AuthToken = authToken;
        }

        public Guid AuthToken { get; set; }
    }

    public class TcpTransactionBuffer<IdType, EntityType> : TcpSecurityPackage
    {
        public TcpTransactionBuffer(Guid authToken, ITransaction<IdType, EntityType> transaction) : base(authToken)
        {
            Transaction = transaction;
        }

        public ITransaction<IdType, EntityType> Transaction { get; set; }
    }
}
