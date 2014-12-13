/*
Copyright (c) 2011,2012,2013 Kristen Mallory dba Klink

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime;
using System.Threading;
using BESSy.Json.Linq;
using BESSy.Json;
using System.Transactions;
using System.IO;
using System.Diagnostics;

namespace BESSy.Transactions
{
    public struct EnlistedAction<EntityType>
    {
        [TargetedPatchingOptOut("Performance critical.")]
        public EnlistedAction(Action action, EntityType entity)
            : this()
        {
            Action = action;
            Entity = entity;
        }
        public Action Action { get; set; }
        public EntityType Entity { get; set; }
        public object DbSegment { get; set; }

        [JsonIgnore]
        public Stream Reversal { get; set; }
    }

    public interface ITransaction : IDisposable, IPromotableSinglePhaseNotification, IEnlistmentNotification
    {
        bool CommitInProgress { get; }
        bool IsComplete { get; }
        Guid Id { get; }
        Guid Source { get; }
        int EnlistCount { get; }
        void Rollback();
        void Commit();
        void MarkComplete();
    }

    public interface ITransaction<EntityType> : ITransaction
    {
        IEnumerable<EntityType> GetEnlistedItems();
        IList<EnlistedAction<EntityType>> GetActions();
    }

    public interface ITransaction<IdType, EntityType> : ITransaction<EntityType>
    {
        void Enlist(Action action, IdType id, EntityType entity);
        void EnlistObj(Action action, IdType id, object entity);
        IDictionary<IdType, EnlistedAction<EntityType>> GetEnlistedActions();
        IList<Tuple<string, IEnumerable<IdType>, IEnumerable<IdType>>> GetCascades();
        bool Contains(IdType id);
        void UpdateSegments(IDictionary<IdType, object> segments);
        void Cascade(Tuple<string, IEnumerable<IdType>, IEnumerable<IdType>> cascade);
    }

    internal class Transaction<IdType, EntityType> : ITransaction<IdType, EntityType>
    {
        [TargetedPatchingOptOut("Performance critical.")]
        internal Transaction() { }

        [TargetedPatchingOptOut("Performance critical.")]
        internal Transaction(ITransactionManager<IdType, EntityType> transactionManager)
            : this(-1, transactionManager)
        {

        }

        [TargetedPatchingOptOut("Performance critical.")]
        internal Transaction(int ttl, ITransactionManager<IdType, EntityType> transactionManager)
            : this()
        {
            Id = Guid.NewGuid();
            Source = transactionManager.Source;
            _transactionManager = transactionManager;

            if (ttl > 0)
                new Timer(ForceCommit, null, ttl, -1);

            if (transactionManager.DistributedScopeEnlistment == TransactionEnlistmentType.SinglePhasePromotable)
            {
                if (Transaction.Current == null)
                    _scope = new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions() { IsolationLevel = IsolationLevel.ReadUncommitted }, EnterpriseServicesInteropOption.Automatic);

                if (Transaction.Current != null && !Transaction.Current.EnlistPromotableSinglePhase(this))
                    throw new TransactionException("Unable to enlist in distributed transaction scope");
            }
            else if (transactionManager.DistributedScopeEnlistment == TransactionEnlistmentType.FullEnlistmentNotification)
            {
                if (Transaction.Current == null)
                    _scope = new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions() { IsolationLevel = IsolationLevel.ReadUncommitted }, EnterpriseServicesInteropOption.Automatic);

                if (Transaction.Current != null)
                    _enlistment = Transaction.Current.EnlistDurable(Id, (IEnlistmentNotification)(this), EnlistmentOptions.None);
            }
        }

        Enlistment _enlistment;
        TransactionScope _scope;
        bool _isCommitted = false;

        ITransactionManager<IdType, EntityType> _transactionManager;

        protected object _syncRoot = new object();

        [JsonProperty]
        List<Tuple<string, IEnumerable<IdType>, IEnumerable<IdType>>> _cascades
            = new List<Tuple<string, IEnumerable<IdType>, IEnumerable<IdType>>>();

        [JsonProperty]
        protected Dictionary<IdType, EnlistedAction<EntityType>> _enlistedActions
            = new Dictionary<IdType, EnlistedAction<EntityType>>();

        void ForceCommit(object state)
        {
            lock (_syncRoot)
            {
                if (IsComplete) return;
                if (_isCommitted) return;

                _isCommitted = true;
            }

            _transactionManager.Commit(this);
        }

        #region IPromotableSinglePhaseNotification Members

        void IPromotableSinglePhaseNotification.Initialize()
        {

        }

        void IPromotableSinglePhaseNotification.Rollback(SinglePhaseEnlistment singlePhaseEnlistment)
        {
            this.Rollback();

            singlePhaseEnlistment.Aborted();
        }

        void IPromotableSinglePhaseNotification.SinglePhaseCommit(SinglePhaseEnlistment singlePhaseEnlistment)
        {
            this.Commit();

            singlePhaseEnlistment.Committed();
        }

        byte[] ITransactionPromoter.Promote()
        {
            return this.Id.ToByteArray();
        }

        #endregion

        #region IEnlistmentNotification Memebers

        void IEnlistmentNotification.Commit(Enlistment enlistment)
        {
            if (!IsComplete && !CommitInProgress)
                ((ISynchronizedTransactionManager<IdType, EntityType>)_transactionManager).ForceSychronizedCommit(this);
        }

        void IEnlistmentNotification.InDoubt(Enlistment enlistment)
        {
            if (!IsComplete && !CommitInProgress)
                ((ISynchronizedTransactionManager<IdType, EntityType>)_transactionManager).ForceSychronizedRollback(this);
        }

        void IEnlistmentNotification.Prepare(PreparingEnlistment preparingEnlistment)
        {
            if (!IsComplete && !CommitInProgress)
                ((ISynchronizedTransactionManager<IdType, EntityType>)_transactionManager).ForceSychronizedCommit(this);
        }

        void IEnlistmentNotification.Rollback(Enlistment enlistment)
        {
            if (!IsComplete && !CommitInProgress)
                ((ISynchronizedTransactionManager<IdType, EntityType>)_transactionManager).ForceSychronizedRollback(this);
        }

        #endregion

        public int EnlistCount { get { return _enlistedActions.Count; } }

        [JsonProperty]
        public Guid Id { get; private set; }
        [JsonProperty]
        public Guid Source { get; private set; }
        [JsonProperty]
        public bool IsComplete { get; private set; }
        [JsonProperty]
        public bool CommitInProgress { get; private set; }

        public virtual void Enlist(Action action, IdType id, EntityType entity)
        {
            lock (_syncRoot)
            {
                if (IsComplete || CommitInProgress) throw new TransactionStateException("Transaction is no longer active. No furthur enlistment is possible.");

                if (_enlistedActions.ContainsKey(id))
                {
                    var oldAction = _enlistedActions[id].Action;

                    if (oldAction == Action.Delete && action != Action.Delete)
                        throw new TransactionStateException(string.Format("This object has already been deleted {0}", id));

                    if (action == Action.Delete && (oldAction == Action.Create))
                    {
                        _enlistedActions.Remove(id);
                        return;
                    }

                    var newAction = oldAction == Action.Create ? oldAction : action;

                    _enlistedActions[id] = new EnlistedAction<EntityType>(newAction, entity);
                }
                else
                    _enlistedActions.Add(id, new EnlistedAction<EntityType>(action, entity));
            }
        }

        public void EnlistObj(Action action, IdType id, object entity)
        {
            Enlist(action, id, (EntityType)entity);
        }

        public void Cascade(Tuple<string, IEnumerable<IdType>, IEnumerable<IdType>> cascade)
        {
            lock (_syncRoot)
            {
                if (IsComplete || CommitInProgress) throw new TransactionStateException("Transaction is no longer active. No furthur cascades are possible.");

                _cascades.Add(cascade);
            }
        }

        public IList<Tuple<string, IEnumerable<IdType>, IEnumerable<IdType>>> GetCascades()
        {
            lock (_syncRoot)
            {
                return _cascades.ToList();
            }
        }

        public void UpdateSegments(IDictionary<IdType, object> segments)
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

        public IDictionary<IdType, EnlistedAction<EntityType>> GetEnlistedActions()
        {
            return new Dictionary<IdType, EnlistedAction<EntityType>>(_enlistedActions);
        }

        public IList<EnlistedAction<EntityType>> GetActions()
        {
            return new List<EnlistedAction<EntityType>>(_enlistedActions.Values);
        }

        public IEnumerable<EntityType> GetEnlistedItems()
        {
            return _enlistedActions.Values.Select(v => v.Entity);
        }

        public bool Contains(IdType id)
        {
            lock (_syncRoot)
                return _enlistedActions.ContainsKey(id);
        }

        public virtual bool Contains(IdType id, EntityType entity)
        {
            return Contains(id);
        }

        public void Rollback()
        {
            if (IsComplete || CommitInProgress)
                return;

            //throw new TransactionStateException("Transaction is no longer active. There is nothing to rollback.");

            lock (_syncRoot)
                _transactionManager.RollBack(this);
        }

        public void Commit()
        {
            lock (_syncRoot)
            {
                if (_isCommitted || CommitInProgress) return;
                if (IsComplete) throw new TransactionStateException("Transaction is no longer active. There is nothing to commit.");

                CommitInProgress = true;
                _isCommitted = true;
                _transactionManager.Commit(this);
            }
        }

        public void MarkComplete()
        {
            lock (_syncRoot)
            {
                //reset the reference, leave the previous reference alone.
                //_enlistedActions = new Dictionary<IdType, EnlistedAction<IdType, EntityType>>();
                IsComplete = true;
                CommitInProgress = false;

                if (_enlistment != null)
                    _enlistment.Done();
            }
        }

        public void Dispose()
        {
            if (_scope != null)
                _scope.Dispose();

            if (!IsComplete)
            {
                if (!_isCommitted && !CommitInProgress)
                {
                    if (_transactionManager != null)
                    {
                        Trace.TraceError("Rolling back transaction with {0} segments", _enlistedActions.Count);

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
            {
                _transactionManager = null;
            }
        }
    }
}
