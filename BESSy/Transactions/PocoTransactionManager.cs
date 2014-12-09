using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Factories;
using BESSy.Serialization.Converters;

namespace BESSy.Transactions
{
    public interface IPocoTransactionManager<IdType, EntityType> : ITransactionManager<IdType, EntityType>
    {
        IBinConverter<IdType> IdConverter { get; set; }
    }

    public class PocoTransactionManager<IdType, EntityType> : TransactionManager<IdType, EntityType>, IPocoTransactionManager<IdType, EntityType>
    {
        public PocoTransactionManager()
            : base(new PocoTransactionFactory<IdType, EntityType>(), new TransactionSynchronizer<IdType, EntityType>()) { }

        public PocoTransactionManager(IBinConverter<IdType> idConverter)
            : base(new PocoTransactionFactory<IdType, EntityType>(idConverter), new TransactionSynchronizer<IdType, EntityType>()) { }

        public PocoTransactionManager(ITransactionFactory<IdType, EntityType> transactionFactory, ITransactionSynchronizer<IdType, EntityType> transactionSynchronizer)
            : base(transactionFactory, transactionSynchronizer)
        { }

        public IBinConverter<IdType> IdConverter
        {
            get { return _transactionFactory is IPocoTransactionFactory<IdType, EntityType> ? (_transactionFactory as IPocoTransactionFactory<IdType, EntityType>).IdConverter : null; }
            set { if (_transactionFactory is IPocoTransactionFactory<IdType, EntityType>) { (_transactionFactory as IPocoTransactionFactory<IdType, EntityType>).IdConverter = value; } }
        }
    }
}
