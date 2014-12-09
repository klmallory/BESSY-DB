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

        Dictionary<IdType, EnlistedAction<EntityType>> _enlistedActions
            = new Dictionary<IdType, EnlistedAction<EntityType>>();

        List<Tuple<string, IEnumerable<IdType>, IEnumerable<IdType>>> _cascades
            = new List<Tuple<string, IEnumerable<IdType>, IEnumerable<IdType>>>();

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
        public bool CommitInProgress { get; private set; }

        public void Enlist(Action action, IdType id, EntityType entity)
        {
            lock (_syncRoot)
            {
                if (IsComplete || CommitInProgress) throw new TransactionStateException("Transaction is no longer active. No furthur enlistment is possible.");

                var enlistment = new EnlistedAction<EntityType>(action, entity);

                if (_enlistedActions.ContainsKey(id))
                    _enlistedActions[id] = enlistment;
                else
                    _enlistedActions.Add(id, enlistment);
            }
        }

        public void Cascade(Tuple<string, IEnumerable<IdType>, IEnumerable<IdType>> cascade)
        {
            lock (_syncRoot)
            {
                if (IsComplete || CommitInProgress) throw new TransactionStateException("Transaction is no longer active. No furthur cascades are possible.");

                _cascades.Add(cascade);
            }
        }

        public void UpdateSegments(IDictionary<IdType, object> segments)
        {
            lock (_syncRoot)
            {
                foreach (var s in segments)
                {
                    if (_enlistedActions.ContainsKey(s.Key))
                    {
                        var action = _enlistedActions[s.Key];
                        action.DbSegment = s.Value;
                        _enlistedActions[s.Key] = action;
                    }
                }
            }
        }

        public IDictionary<IdType, EnlistedAction<EntityType>> GetEnlistedActions()
        {
            return _enlistedActions;
        }

        public IList<EnlistedAction<EntityType>> GetActions()
        {
            return _enlistedActions.Values.ToList();
        }

        public IList<Tuple<string, IEnumerable<IdType>, IEnumerable<IdType>>> GetCascades()
        {
            return _cascades.ToList();
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

        public void EnlistObj(Action action, IdType id, dynamic entity)
        {
            Enlist(action, id, entity);
        }

        public bool Contains(IdType id, EntityType entity)
        {
            return Contains(id);
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

        #region Transaction Scope Methods

        public void Initialize()
        {
            throw new NotImplementedException();
        }

        public void Rollback(System.Transactions.SinglePhaseEnlistment singlePhaseEnlistment)
        {
            throw new NotImplementedException();
        }

        public void SinglePhaseCommit(System.Transactions.SinglePhaseEnlistment singlePhaseEnlistment)
        {
            throw new NotImplementedException();
        }

        public byte[] Promote()
        {
            throw new NotImplementedException();
        }

        public void Commit(System.Transactions.Enlistment enlistment)
        {
            throw new NotImplementedException();
        }

        public void InDoubt(System.Transactions.Enlistment enlistment)
        {
            throw new NotImplementedException();
        }

        public void Prepare(System.Transactions.PreparingEnlistment preparingEnlistment)
        {
            throw new NotImplementedException();
        }

        public void Rollback(System.Transactions.Enlistment enlistment)
        {
            throw new NotImplementedException();
        }

        #endregion




    }
}
