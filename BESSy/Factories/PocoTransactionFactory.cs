using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Transactions;
using BESSy.Serialization.Converters;
using BESSy.Seeding;

namespace BESSy.Factories
{
    public interface IPocoTransactionFactory<IdType, EntityType>
    {
        IBinConverter<IdType> IdConverter { get; set; }
    }

    internal class PocoTransactionFactory<IdType, EntityType> : ITransactionFactory<IdType, EntityType>, IPocoTransactionFactory<IdType, EntityType>
    {
        internal PocoTransactionFactory()
        {
            try
            { this.IdConverter = TypeFactory.GetBinConverterFor<IdType>(); }
            catch (ArgumentException) { }
        }

        internal PocoTransactionFactory(IBinConverter<IdType> idConverter)
        {
            this.IdConverter = idConverter;
        }

        public IBinConverter<IdType> IdConverter { get; set; }

        public virtual ITransaction<IdType, EntityType> Create(ITransactionManager<IdType, EntityType> transactionManager)
        {
            return new PocoTransaction<IdType, EntityType>(transactionManager, IdConverter);
        }

        public virtual ITransaction<IdType, EntityType> Create(int timeToLive, ITransactionManager<IdType, EntityType> transactionManager)
        {
            return new PocoTransaction<IdType, EntityType>(timeToLive, transactionManager, IdConverter);
        }
    }
}
