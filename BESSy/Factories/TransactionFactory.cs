using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Transactions;

namespace BESSy.Factories
{
    public interface ITransactionFactory<IdType, EntityType>
    {
        ITransaction<IdType, EntityType> Create(ITransactionManager<IdType, EntityType> transactionManager);
    }

    public class TransactionFactory<IdType, EntityType> : ITransactionFactory<IdType, EntityType>
    {
        public ITransaction<IdType, EntityType> Create(ITransactionManager<IdType, EntityType> transactionManager)
        {
            return new Transaction<IdType, EntityType>(transactionManager);
        }
    }
}
