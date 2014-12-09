using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Relational;
using System.Runtime;
using BESSy.Serialization.Converters;

namespace BESSy.Transactions
{
    public interface IPocoTransaction<IdType, EntityType> : ITransaction<IdType, EntityType>
    {
        bool Contains(IdType id, string typeName);
    }

    internal sealed class PocoTransaction<IdType, EntityType> : Transaction<IdType, EntityType>, IPocoTransaction<IdType, EntityType>
    {
        [TargetedPatchingOptOut("Performance critical.")]
        internal PocoTransaction(IBinConverter<IdType> idConverter) : base() { _idConverter = idConverter; }

        [TargetedPatchingOptOut("Performance critical.")]
        internal PocoTransaction(ITransactionManager<IdType, EntityType> transactionManager, IBinConverter<IdType> idConverter)
            : base(transactionManager)
        {
            _idConverter = idConverter;
        }

        [TargetedPatchingOptOut("Performance critical.")]
        internal PocoTransaction(int ttl, ITransactionManager<IdType, EntityType> transactionManager, IBinConverter<IdType> idConverter)
            : base(ttl, transactionManager)
        {
            _idConverter = idConverter;
        }

        IBinConverter<IdType> _idConverter = null;

        List<Tuple<IdType, string>> _enlistedOldIds = new List<Tuple<IdType, string>>();

        public override void Enlist(Action action, IdType id, EntityType entity)
        {
            var proxy = entity as IBESSyProxy<IdType, EntityType>;

            if (proxy != null)
                lock (_syncRoot)
                    _enlistedOldIds.Add(new Tuple<IdType, string>(proxy.Bessy_Proxy_OldId, proxy.Bessy_Proxy_Simple_Type_Name));

            base.Enlist(action, id, entity);
        }

        public bool Contains(IdType id, string typeName)
        {
            lock (_syncRoot)
                return _enlistedOldIds.Any(o => _idConverter.Compare(o.Item1, id) == 0 && typeName == o.Item2);
        }
    }
}
