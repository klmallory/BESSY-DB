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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BESSy.Extensions;
using BESSy.Factories;
using BESSy.Parallelization;
using BESSy.Seeding;
using BESSy.Serialization.Converters;

namespace BESSy.Transactions
{
    public enum TransactionEnlistmentType
    {
        None = 0,
        SinglePhasePromotable,
        FullEnlistmentNotification
    }

    public delegate void TransactionCommit<IdType, EntityType>
    (ITransaction<IdType, EntityType> transaction);

    internal interface ISynchronizedTransactionManager<IdType, EntityType>
    {
        void ForceSychronizedCommit(ITransaction<IdType, EntityType> transaction);
        void ForceSychronizedRollback(ITransaction<IdType, EntityType> transaction);
    }

    public interface ITransactionManager<IdType, EntityType> : IDisposable
    {
        bool HasActiveTransactions { get; }
        Guid Source { get; set; }
        TransactionEnlistmentType DistributedScopeEnlistment { get; set; }
        ITransaction<IdType, EntityType> CurrentTransaction { get; }
        TransactionLock<IdType, EntityType> GetActiveTransaction(bool canCreateNew);
        IDictionary<IdType, EntityType> GetActiveItems();
        TransactionLock<IdType, EntityType> BeginTransaction();
        void RollBack(ITransaction<IdType, EntityType> transaction);
        void RollBackAll(bool allThreads);
        void Commit(ITransaction<IdType, EntityType> transaction);
        void CommitAll(bool allThreads);
        void CommitAmbientTransactions();
        event TransactionCommit<IdType, EntityType> TransactionCommitted;
    }

    public class TransactionManager<IdType, EntityType> : ITransactionManager<IdType, EntityType>, ISynchronizedTransactionManager<IdType, EntityType>, IDisposable
    {
        public TransactionManager()
            : this(new TransactionFactory<IdType, EntityType>(), new TransactionSynchronizer<IdType, EntityType>()) { }

        public TransactionManager(ITransactionFactory<IdType, EntityType> transactionFactory, ITransactionSynchronizer<IdType, EntityType> transactionSynchronizer)
        {
            _transactionFactory = transactionFactory;
            _sync = transactionSynchronizer;
        }

        object _syncRoot = new object();
        object _syncRollbackAll = new object();

        protected ITransactionSynchronizer<IdType, EntityType> _sync;
        protected ITransactionFactory<IdType, EntityType> _transactionFactory;

        protected IDictionary<int, ITransaction<IdType, EntityType>> _ambientCache = new Dictionary<int, ITransaction<IdType, EntityType>>();
        protected IDictionary<int, Stack<ITransaction<IdType, EntityType>>> _transactionCache = new Dictionary<int, Stack<ITransaction<IdType, EntityType>>>();

        ITransaction<IdType, EntityType> CreateAmbientTransaction()
        {
            var id = Thread.CurrentThread.ManagedThreadId;

            lock (_syncRoot)
            {
                if (_ambientCache.ContainsKey(id))
                    return _ambientCache[id];

                var tran = _transactionFactory.Create(5000, this);

                _ambientCache.Add(id, tran);

                Trace.TraceInformation("Ambient trans created {0}", tran.Id);

                return tran;
            }
        }

        protected virtual ITransaction<IdType, EntityType> CreateTransaction()
        {
            lock (_syncRoot)
            {
                var id = Thread.CurrentThread.ManagedThreadId;

                var tran = _transactionFactory.Create(this);

                var stack = GetStack(id);

                stack.Push(tran);

                if (!_transactionCache.ContainsKey(id))
                    _transactionCache.Add(id, stack);

                Trace.TraceInformation("Transaction Created {0}", tran.Id);

                return tran;
            }
        }

        protected Stack<ITransaction<IdType, EntityType>> GetStack(int id)
        {
            if (_transactionCache.ContainsKey(id))
                return _transactionCache[id];
            else
                return new Stack<ITransaction<IdType, EntityType>>();
        }

        protected bool ForceRollback(ITransaction<IdType, EntityType> trans)
        {
            TransactionLock<IdType, EntityType> lck = default(TransactionLock<IdType, EntityType>);
            try
            {
                if (!trans.IsComplete && !trans.CommitInProgress && _sync.TryLock(trans, 5000, out lck))
                {
                    lck.Transaction.Rollback();
                    return true;
                }
                else if (!trans.IsComplete || trans.CommitInProgress)
                {
                    lck = _sync.GetExistingLockFor(trans);
                    _sync.Unlock(lck);
                    lck.Transaction.Rollback();

                    return true;
                }
                else
                    Trace.TraceError("Forcing a rollback failed becuase of a conflicting transaction lock, this process was chosen as the deadlock victim");
            }
            finally { lck.Dispose(); }

            return false;
        }

        protected bool ForceCommit(ITransaction<IdType, EntityType> trans)
        {
            TransactionLock<IdType, EntityType> lck = default(TransactionLock<IdType, EntityType>);
            try
            {
                if (!trans.IsComplete && !trans.CommitInProgress && _sync.TryLock(trans, 5000, out lck))
                {
                    lck.Transaction.Commit();
                    return true;
                }
                else if (!trans.IsComplete || trans.CommitInProgress)
                {
                    lck = _sync.GetExistingLockFor(trans);
                    _sync.Unlock(lck);
                    lck.Transaction.Commit();

                    return true;
                }
                else
                    Trace.TraceError("Forcing a commit failed becuase of a conflicting transaction lock, this process was chosen as the deadlock victim");
            }
            finally { lck.Dispose(); }

            return false;
        }

        public Guid Source { get; set; }
        public TransactionEnlistmentType DistributedScopeEnlistment { get; set; }

        public bool HasActiveTransactions
        {
            get
            {
                return _transactionCache.Any(s => s.Value.Count > 0 && s.Value.Any(t => !t.IsComplete))
                    || _ambientCache.Any(a => !a.Value.IsComplete);
            }
        }

        protected ITransaction<IdType, EntityType> ActiveTransaction
        {
            get
            {
                var id = Thread.CurrentThread.ManagedThreadId;

                lock (_syncRoot)
                {
                    if (_transactionCache.ContainsKey(id) && _transactionCache[id].Count > 0)
                        return _transactionCache[id].Peek();
                    else if (_ambientCache.ContainsKey(id))
                        return _ambientCache[id];
                    else
                        return null;
                }
            }
        }

        public ITransaction<IdType, EntityType> CurrentTransaction
        {
            get { return ActiveTransaction; }
        }

        public TransactionLock<IdType, EntityType> GetActiveTransaction(bool canCreateNew)
        {
            var active = ActiveTransaction;

            if (active != null)
            {
                return _sync.Lock(active);
            }
            else if (canCreateNew)
            {
                active = CreateTransaction();
                return _sync.Lock(active);
            }
            else
            {
                active = CreateAmbientTransaction();
                return _sync.Lock(active);
            }
        }

        public TransactionLock<IdType, EntityType> BeginTransaction()
        {
            return _sync.Lock(CreateTransaction());
        }

        public IDictionary<IdType, EntityType> GetActiveItems()
        {
            var active = ActiveTransaction;

            var tranies = new Dictionary<IdType, EntityType>();

            lock (_syncRoot)
            {
                if (active != null && !active.IsComplete)
                    foreach (var a in active.GetEnlistedActions())
                        if (!tranies.ContainsKey(a.Key))
                            tranies.Add(a.Key, a.Value.Entity);
            }

            return tranies;
        }

        public void RollBackAll(bool allThreads)
        {
            Trace.TraceInformation("Rolling back all transactions");

            var id = Thread.CurrentThread.ManagedThreadId;

            var count = 0;
            var containsKey = false;
            lock (_syncRoot)
            {
                count = _transactionCache.Count;
                containsKey = _transactionCache.ContainsKey(id);
            }

            TransactionLock<IdType, EntityType> lck = default(TransactionLock<IdType, EntityType>);

            while ((allThreads && count > 0) || containsKey)
            {
                var kv = _transactionCache.LastOrDefault();

                if (!allThreads && kv.Key != id)
                    continue;

                var stack = kv.Value;

                lock (_syncRoot)
                    if (stack == null)
                    {
                        if (_transactionCache.ContainsKey(id))
                            _transactionCache.Remove(id);

                        containsKey = false;
                        count = _transactionCache.Count;
                        continue;
                    }

                while (stack.Count > 0)
                {
                    ITransaction<IdType, EntityType> trans = null;

                    trans = stack.Peek();

                    if (!ForceRollback(trans))
                    {
                        lock (_syncRoot)
                            if (stack.Count > 0 && trans.Id == stack.Peek().Id)
                                stack.Pop();
                    }
                }
            }

            lock (_syncRoot)
                containsKey = _ambientCache.ContainsKey(id);

            if (!allThreads && containsKey)
            {
                var tran = _ambientCache[id];

                if (!ForceRollback(tran))
                    lock (_syncRoot)
                        _ambientCache.Remove(id);
            }
            else if (allThreads)
            {
                lock (_syncRoot)
                    count = _ambientCache.Count;

                while (count > 0)
                {
                    var ambient = _ambientCache.First();

                    if (!ForceRollback(ambient.Value))
                        lock (_syncRoot)
                            _ambientCache.Remove(ambient.Key);

                    lock (_syncRoot)
                        count = _ambientCache.Count;
                }
            }
        }

        public void RollBack(ITransaction<IdType, EntityType> transaction)
        {
            Trace.TraceInformation("Rolling back trans {0}", transaction.Id);

            if (transaction == null || transaction.IsComplete)
                return;

            Stack<ITransaction<IdType, EntityType>> stack = null;
            KeyValuePair<int, ITransaction<IdType, EntityType>>? ambient = null;

            lock (_syncRoot)
            {
                stack = _transactionCache.Where(t => t.Value.Any(s => s.Id == transaction.Id)).Select(v => v.Value).FirstOrDefault();

                if (stack == null || stack.Count <= 0)
                    ambient = _ambientCache.FirstOrDefault(a => a.Value.Id == transaction.Id && !a.Value.IsComplete);
            }

            if ((stack == null || stack.Count <= 0) && !ambient.HasValue)
                throw new TransactionStateException(string.Format("Transaction is no longer active: {0}", transaction.Id));

            TransactionLock<IdType, EntityType> lck = default(TransactionLock<IdType, EntityType>);

            try
            {
                if (!_sync.TryLock(transaction, 5000, out lck))
                    throw new TransactionLockTimeoutException("Transaction rollback conflicted with another lock on this transaction, this process was chosen as the deadlock victim");

                if (stack != null)
                {
                    while (stack.Count > 0 && stack.Peek().Id != transaction.Id)
                    {
                        ITransaction<IdType, EntityType> child = null;

                        lock (_syncRoot)
                            child = stack.Peek();

                        Trace.TraceInformation("Rolling back child trans {0}", child.Id);

                        TransactionLock<IdType, EntityType> childLock = default(TransactionLock<IdType, EntityType>);

                        try
                        {
                            if (_sync.TryLock(child, 5000, out childLock))
                                child.Rollback();
                            else
                                Trace.TraceError("Rolling back child transaction failed because of confliting transaction lock, this process was chosen as the deadlock victim");
                        }
                        finally { childLock.Dispose(); }
                    }

                    lock (_syncRoot)
                    {
                        if (stack.Peek().Id == transaction.Id)
                            stack.Pop();

                        while (stack.Count > 0 && stack.Peek().IsComplete)
                            stack.Pop();

                        if (stack.Count == 0)
                            _transactionCache.Remove(_transactionCache.Where(s => s.Value.Count <= 0 && s.Key != -1).Select(k => k.Key).FirstOrDefault());
                    }
                }
                else
                    lock (_syncRoot)
                        _ambientCache.Remove(ambient.Value.Key);

                transaction.MarkComplete();
            }
            finally { lck.Dispose(); }

        }

        public void CommitAmbientTransactions()
        {
            lock (_syncRoot)
                if (_ambientCache == null || _ambientCache.Count <= 0)
                    return;

            var count = 0;

            lock (_syncRoot)
                count = _ambientCache.Count;

            while (count > 0)
            {
                var a = _ambientCache.First();

                if (a.Value == null || a.Value.IsComplete)
                {
                    _ambientCache.Remove(a.Key);
                    continue;
                }

                TransactionLock<IdType, EntityType> lck = default(TransactionLock<IdType, EntityType>);

                try
                {
                    if (!_sync.TryLock(a.Value, 5000, out lck))
                        throw new TransactionLockTimeoutException("Ambient transaction commit conflicted with another lock on this transaction, this process was chosen as the deadlock victim");

                    Trace.TraceInformation("Committing ambient trans {0}", a.Value.Id);

                    a.Value.Commit();
                }
                finally
                {
                    lck.Dispose();

                    lock (_syncRoot)
                        if (_ambientCache.ContainsKey(a.Key))
                            _ambientCache.Remove(a.Key);
                }

                lock (_syncRoot)
                    count = _ambientCache.Count;
            }
        }

        public void Commit(ITransaction<IdType, EntityType> transaction)
        {
            Trace.TraceInformation("Committing trans {0}", transaction.Id);

            if (transaction.IsComplete)
                throw new TransactionStateException("Transaction is no longer active.");

            Stack<ITransaction<IdType, EntityType>> stack = null;
            KeyValuePair<int, ITransaction<IdType, EntityType>>? ambient = null;

            lock (_syncRoot)
            {
                stack = _transactionCache.Where(t => t.Value.Any(s => s.Id == transaction.Id)).Select(v => v.Value).FirstOrDefault();

                if (stack == null || stack.Count <= 0)
                    ambient = _ambientCache.FirstOrDefault(a => a.Value.Id == transaction.Id && !a.Value.IsComplete);
            }

            if ((stack == null || stack.Count <= 0) && !ambient.HasValue)
                throw new TransactionStateException(string.Format("Transaction is no longer active: {0}", transaction.Id));

            TransactionLock<IdType, EntityType> lck = default(TransactionLock<IdType, EntityType>);

            try
            {
                if (!_sync.TryLock(transaction, 5000, out lck))
                    throw new TransactionLockTimeoutException("Transaction commit conflicted with another lock on this transaction, this process was chosen as the deadlock victim");

                if (stack != null)
                {
                    while (stack != null && stack.Count > 0 && stack.Peek().Id != transaction.Id)
                    {
                        ITransaction<IdType, EntityType> child = null;

                        lock (_syncRoot)
                            child = stack.Peek();

                        Trace.TraceInformation("Committing child trans {0}", child.Id);

                        using (_sync.Lock(child))
                            child.Commit();
                    }

                    if (stack.Count > 0)
                    {
                        lock (_syncRoot)
                        {
                            if (stack.Peek().Id == transaction.Id)
                                stack.Pop();

                            while (stack.Count > 0 && stack.Peek().IsComplete)
                                stack.Pop();
                        }
                    }

                    if (stack.Count == 0)
                        _transactionCache.Remove(_transactionCache.Where(s => s.Value.Count <= 0 && s.Key != -1).Select(k => k.Key).FirstOrDefault());
                }
                else
                    lock (_syncRoot)
                        _ambientCache.Remove(ambient.Value.Key);

                InvokeTransactionCommit(transaction);
            }
            finally { lck.Dispose(); }
        }

        public void CommitAll(bool allThreads)
        {
            Trace.TraceInformation("Committing all transactions");

            var id = Thread.CurrentThread.ManagedThreadId;

            var count = 0;
            var containsKey = false;

            lock (_syncRoot)
            {
                count = _transactionCache.Count;
                containsKey = _transactionCache.ContainsKey(id);
            }

            while ((allThreads && count > 0) || containsKey)
            {

                var kv = _transactionCache.LastOrDefault();

                if (!allThreads && kv.Key != id)
                    continue;

                var stack = kv.Value;

                lock (_syncRoot)
                    if (stack == null)
                    {
                        if (_transactionCache.ContainsKey(id))
                            _transactionCache.Remove(id);

                        containsKey = false;
                        count = _transactionCache.Count;
                        continue;
                    }

                while (stack.Count > 0)
                {
                    ITransaction<IdType, EntityType> trans = null;

                    lock (_syncRoot)
                        trans = stack.Peek();

                    if (!ForceCommit(trans))
                        lock (_syncRoot)
                            if (stack.Count > 0 && trans.Id == stack.Peek().Id)
                                stack.Pop();
                }

                lock (_syncRoot)
                    _transactionCache.Remove(kv.Key);

                lock (_syncRoot)
                {
                    count = _transactionCache.Count;
                    containsKey = _transactionCache.ContainsKey(id);
                }
            }

            lock (_syncRoot)
                containsKey = _ambientCache.ContainsKey(id);

            if (!allThreads && containsKey)
            {
                var tran = _ambientCache[id];

                if (!ForceCommit(tran))
                    lock (_syncRoot)
                        if (_ambientCache.ContainsKey(id))
                            _ambientCache.Remove(id);
            }
            else if (allThreads)
            {
                lock (_syncRoot)
                    count = _ambientCache.Count;

                while (count > 0)
                {
                    var ambient = _ambientCache.First();

                    if (!ForceCommit(ambient.Value))
                        lock (_syncRoot)
                            if (_ambientCache.ContainsKey(ambient.Key))
                                _ambientCache.Remove(ambient.Key);

                    lock (_syncRoot)
                        count = _ambientCache.Count;
                }
            }

        }

        public IEnumerable<EntityType> GetCached()
        {
            var id = Thread.CurrentThread.ManagedThreadId;

            Stack<ITransaction<IdType, EntityType>> stack = null;

            lock (_syncRoot)
            {
                if (_transactionCache.Count <= 0)
                    return new List<EntityType>();

                if (_transactionCache.ContainsKey(id))
                    stack = _transactionCache[id];
                else
                    stack = new Stack<ITransaction<IdType, EntityType>>();

                var active = ActiveTransaction;

                if (active != null && !stack.Contains(active))
                    stack.Push(active);
            }

            var items = stack.Where(s => !s.IsComplete).SelectMany(s => s.GetEnlistedItems()).Reverse();

            Trace.TraceInformation("Returning {0} cached trans enlisted actions", items.Count());

            return items;
        }

        #region ISynchronizedTransactionManager Members

        void ISynchronizedTransactionManager<IdType, EntityType>.ForceSychronizedCommit(ITransaction<IdType, EntityType> transaction)
        {
            if (!transaction.IsComplete || transaction.CommitInProgress)
            {
                using (var lck = _sync.GetExistingLockFor(transaction))
                {
                    _sync.Unlock(lck);
                    lck.Transaction.Commit();
                }
            }
        }

        void ISynchronizedTransactionManager<IdType, EntityType>.ForceSychronizedRollback(ITransaction<IdType, EntityType> transaction)
        {
            if (!transaction.IsComplete || transaction.CommitInProgress)
            {
                using (var lck = _sync.GetExistingLockFor(transaction))
                {
                    _sync.Unlock(lck);
                    lck.Transaction.Rollback();
                }
            }
        }

        #endregion

        #region TransactionCommit Event

        private void InvokeTransactionCommit(ITransaction<IdType, EntityType> transaction)
        {
            Trace.TraceInformation("Transaction {0} committing", transaction.Id);

            try
            {
                if (TransactionCommitted != null)
                    TransactionCommitted(transaction);
            }
            catch (Exception)
            {
                if (transaction != null && !transaction.IsComplete)
                    transaction.MarkComplete();

                throw;
            }
        }

        public event TransactionCommit<IdType, EntityType> TransactionCommitted;

        #endregion

        public void Dispose()
        {
            lock (_syncRoot)
                if (_transactionCache.Count > 0)
                    Trace.TraceError("Not all active transactions have been committed or rolled back. Data was lost.");

            if (_sync != null)
                _sync.Dispose();
        }


    }
}
