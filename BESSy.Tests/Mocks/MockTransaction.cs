using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Transactions;

namespace BESSy.Tests.Mocks
{
    public class MockTransaction<IdType, EntityType> : ITransaction<IdType, EntityType>
    {
        public MockTransaction(ITransactionManager<IdType, EntityType> transactionManager)
        {
            Id = Guid.NewGuid();
            _transactionManager = transactionManager;
        }

        object _syncRoot = new object();
        bool _complete = false;
        ITransactionManager<IdType, EntityType> _transactionManager;
        Dictionary<IdType, EnlistedAction<IdType, EntityType>> _enlistedActions 
            = new Dictionary<IdType, EnlistedAction<IdType, EntityType>>();

        public void Enlist(Action action, IdType id, EntityType entity)
        {
            lock (_syncRoot)
            {
                if (_complete) throw new TransactionStateException("Transaction is no longer active. No furthur enlistment is possible.");

                var enlistment = new EnlistedAction<IdType, EntityType>(action, id, entity);

                if (_enlistedActions.ContainsKey(id))
                    _enlistedActions[id] = enlistment;
                else
                    _enlistedActions.Add(id, enlistment);
            }
        }

        public IDictionary<IdType, EnlistedAction<IdType, EntityType>> GetEnlistedActions()
        {
            return _enlistedActions;
        }

        public IEnumerable<EntityType> GetEnlistedItems()
        {
            return _enlistedActions.Values.Select(v => v.Entity);
        }

        public Guid Id { get; private set; }

        public void Rollback()
        {
            if (_complete) throw new TransactionStateException("Transaction is no longer active. There is nothing to rollback.");

            lock (_syncRoot)
            {
                _transactionManager.RollBack(this);

                _complete = true;
            }
        }

        public void Commit()
        {
            if (_complete) throw new TransactionStateException("Transaction is no longer active. There is nothing to commit.");

            lock (_syncRoot)
            {
                _transactionManager.Commit(this);

                _complete = true;
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
                if (!_complete)
                    if (_transactionManager != null)
                        _transactionManager.RollBack(this);

            _transactionManager = null;
        }
    }
}
