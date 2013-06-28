using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Transactions;
using System.Threading;
using System.Runtime;

namespace BESSy.Tests.Mocks
{
    public class MockTransaction<IdType, EntityType> : ITransaction<IdType, EntityType>
    {
        [TargetedPatchingOptOut("Performance critical.")]
        public MockTransaction() { }

        public MockTransaction(ITransactionManager<IdType, EntityType> transactionManager)
            : this(-1, transactionManager)
        {

        }

        public MockTransaction(int timeToLive, ITransactionManager<IdType, EntityType> transactionManager)
        {
            Id = Guid.NewGuid();
            Source = transactionManager.Source;
            _transactionManager = transactionManager;

            if (timeToLive > 0)
                new Timer(ForceCommit, null, timeToLive, -1);
        }

        object _syncRoot = new object();
        bool _isCommitted = false;

        ITransactionManager<IdType, EntityType> _transactionManager;
        Dictionary<IdType, EnlistedAction<IdType, EntityType>> _enlistedActions
            = new Dictionary<IdType, EnlistedAction<IdType, EntityType>>();

        void ForceCommit(object state)
        {
            lock (_syncRoot)
            {
                if (IsComplete) return;
                if (_isCommitted) return;

                _isCommitted = true;
                _transactionManager.Commit(this);
            }
        }

        public Guid Source { get; set; }
        public bool IsComplete { get; private set; }

        public void Enlist(Action action, IdType id, EntityType entity)
        {
            lock (_syncRoot)
            {
                if (IsComplete) throw new TransactionStateException("Transaction is no longer active. No furthur enlistment is possible.");

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

        public bool Contains(IdType id)
        {
            return _enlistedActions.ContainsKey(id);
        }

        public void Rollback()
        {
            if (IsComplete) throw new TransactionStateException("Transaction is no longer active. There is nothing to rollback.");

            lock (_syncRoot)
            {
                _transactionManager.RollBack(this);
            }
        }

        public void Commit()
        {
            if (IsComplete) throw new TransactionStateException("Transaction is no longer active. There is nothing to commit.");

            lock (_syncRoot)
            {
                _isCommitted = true;
                _transactionManager.Commit(this);
            }
        }


        public void MarkComplete()
        {
            lock (_syncRoot)
            {
                IsComplete = true;
            }
        }

        public void Dispose()
        {
            if (!IsComplete)
            {
                if (!_isCommitted)
                {
                    if (_transactionManager != null)
                    {
                        lock (_syncRoot)
                            _transactionManager.RollBack(this);

                        while (!IsComplete)
                            Thread.Sleep(100);
                    }
                }
                else
                {
                    while (!IsComplete)
                        Thread.Sleep(100);
                }
            }

            //remove the reference to the transactionMagager.
            lock (_syncRoot)
                _transactionManager = null;
        }


        public int EnlistCount
        {
            get { return _enlistedActions.Count; }
        }
    }
}
