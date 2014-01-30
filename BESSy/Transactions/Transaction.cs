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

namespace BESSy.Transactions
{
    public struct EnlistedAction<IdType, EntityType>
    {
        [TargetedPatchingOptOut("Performance critical.")]
        public EnlistedAction(Action action, IdType id, EntityType entity)
            : this()
        {
            Action = action;
            Id = id;
            Entity = entity;
        }

        public Action Action { get; set; }
        public IdType Id { get; set; }
        public EntityType Entity { get; set; }
    }

    public interface ITransaction<IdType, EntityType> : IDisposable
    {
        bool IsComplete { get; }
        Guid Id { get; }
        Guid Source { get; }
        void Enlist(Action action, IdType id, EntityType entity);
        IEnumerable<EntityType> GetEnlistedItems();
        IDictionary<IdType, EnlistedAction<IdType, EntityType>> GetEnlistedActions();
        int EnlistCount { get; }
        bool Contains(IdType id);
        void Rollback();
        void Commit();
        void MarkComplete();
    }

    internal sealed class Transaction<IdType, EntityType> : ITransaction<IdType, EntityType>
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
        }

        object _syncRoot = new object();
        bool _isCommitted = false;

        ITransactionManager<IdType, EntityType> _transactionManager;

        [JsonProperty]
        Dictionary<IdType, EnlistedAction<IdType, EntityType>> _enlistedActions
            = new Dictionary<IdType, EnlistedAction<IdType, EntityType>>();

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

        public int EnlistCount { get { return _enlistedActions.Count; } }

        [JsonProperty]
        public Guid Id { get; private set; }
        [JsonProperty]
        public Guid Source { get; private set; }
        [JsonProperty]
        public bool IsComplete { get; private set; }

        public void Enlist(Action action, IdType id, EntityType entity)
        {
            lock (_syncRoot)
            {
                if (IsComplete) throw new TransactionStateException("Transaction is no longer active. No furthur enlistment is possible.");

                if (_enlistedActions.ContainsKey(id))
                {
                    var oldAction = _enlistedActions[id].Action;

                    if (oldAction == Action.Delete && action != Action.Delete)
                        throw new TransactionStateException("Stale transaction state exception. This object has already been deleted.");

                    var newAction = oldAction == Action.Create ? oldAction : action;

                    _enlistedActions[id] = new EnlistedAction<IdType, EntityType>(newAction, id, entity);
                }
                else
                    _enlistedActions.Add(id, new EnlistedAction<IdType, EntityType>(action, id, entity));
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

        public bool Contains(IdType id)
        {
            lock (_syncRoot)
                return _enlistedActions.ContainsKey(id);
        }

        public void Rollback()
        {
            if (IsComplete)
                return;

            //throw new TransactionStateException("Transaction is no longer active. There is nothing to rollback.");

            lock (_syncRoot)
                _transactionManager.RollBack(this);
        }

        public void Commit()
        {
            lock (_syncRoot)
            {
                if (_isCommitted) return;
                if (IsComplete) throw new TransactionStateException("Transaction is no longer active. There is nothing to commit.");

                _isCommitted = true;
                _transactionManager.Commit(this);
            }
        }

        public void MarkComplete()
        {
            lock (_syncRoot)
            {
                //reset the reference, leave the previous reference alone.
                _enlistedActions = new Dictionary<IdType, EnlistedAction<IdType, EntityType>>();
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
    }
}
