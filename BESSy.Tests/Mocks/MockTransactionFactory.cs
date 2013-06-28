using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Factories;

namespace BESSy.Tests.Mocks
{
    public class MockTransactionFactory<IdType, EntityType> : ITransactionFactory<IdType, EntityType>
    {
        public MockTransactionFactory()
        {

        }

        public Transactions.ITransaction<IdType, EntityType> Create(Transactions.ITransactionManager<IdType, EntityType> transactionManager)
        {
            return new MockTransaction<IdType, EntityType>(transactionManager);
        }

        public Transactions.ITransaction<IdType, EntityType> Create(int timeToLive, Transactions.ITransactionManager<IdType, EntityType> transactionManager)
        {
            return new MockTransaction<IdType, EntityType>(timeToLive, transactionManager);
        }
    }
}
