using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Factories;
using BESSy.Json.Linq;
using BESSy.Serialization.Converters;

namespace BESSy.Transactions
{
    public interface IPocoTransactionManager<IdType, EntityType> : ITransactionManager<IdType, JObject>
    {
        IBinConverter<IdType> IdConverter { get; set; }
    }

    public class PocoTransactionManager<IdType, EntityType> : TransactionManager<IdType, JObject>, IPocoTransactionManager<IdType, EntityType>
    {
        public PocoTransactionManager()
            : base(new PocoTransactionFactory<IdType, JObject>(), new TransactionSynchronizer<IdType, JObject>()) { }

        public PocoTransactionManager(IBinConverter<IdType> idConverter)
            : base(new PocoTransactionFactory<IdType, JObject>(idConverter), new TransactionSynchronizer<IdType, JObject>()) { }

        public PocoTransactionManager(ITransactionFactory<IdType, JObject> transactionFactory, ITransactionSynchronizer<IdType, JObject> transactionSynchronizer)
            : base(transactionFactory, transactionSynchronizer)
        { }

        public IBinConverter<IdType> IdConverter
        {
            get { return _transactionFactory is IPocoTransactionFactory<IdType, EntityType> ? (_transactionFactory as IPocoTransactionFactory<IdType, EntityType>).IdConverter : null; }
            set { if (_transactionFactory is IPocoTransactionFactory<IdType, EntityType>) { (_transactionFactory as IPocoTransactionFactory<IdType, EntityType>).IdConverter = value; } }
        }
    }
}
