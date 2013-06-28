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
    public delegate void TransactionCommit<IdType, EntityType>
    (ITransaction<IdType, EntityType> transaction);

    public interface ITransactionManager<IdType, EntityType> : IDisposable
    {
        bool HasActiveTransaction { get; }
        bool HasActiveTransactions { get; }
        Guid Source { get; set; }
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

    public class TransactionManager<IdType, EntityType> : ITransactionManager<IdType, EntityType>, IDisposable
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

        ITransactionSynchronizer<IdType, EntityType> _sync;
        ITransactionFactory<IdType, EntityType> _transactionFactory;

        IDictionary<int, ITransaction<IdType, EntityType>> _ambientCache = new Dictionary<int, ITransaction<IdType, EntityType>>();
        IDictionary<int, Stack<ITransaction<IdType, EntityType>>> _transactionCache = new Dictionary<int, Stack<ITransaction<IdType, EntityType>>>();

        ITransaction<IdType, EntityType> CreateAmbientTransaction()
        {
            var id = Thread.CurrentThread.ManagedThreadId;

            lock (_syncRoot)
            {
                if (_ambientCache.ContainsKey(id))
                    return _ambientCache[id];

                var tran = _transactionFactory.Create(5000, this);

                _ambientCache.Add(id, tran);

                Trace.TraceInformation("Ambient transaction created {0}", tran.Id);

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

        public Guid Source { get; set; }

        public bool HasActiveTransaction
        {
            get
            {
                var id = Thread.CurrentThread.ManagedThreadId;

                return (_transactionCache.ContainsKey(id) && _transactionCache[id].Count > 0) || (_ambientCache.ContainsKey(id));
            }
        }

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

        public TransactionLock<IdType, EntityType> GetActiveTransaction(bool canCreateNew)
        {
            lock (_syncRoot)
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

            lock (_syncRoot)
            {
                while ((allThreads && _transactionCache.Count > 0) || _transactionCache.ContainsKey(id))
                {
                    var kv = _transactionCache.LastOrDefault();

                    if (!allThreads && kv.Key != id)
                        continue;

                    var stack = kv.Value;

                    while (stack.Count > 0)
                    {
                        ITransaction<IdType, EntityType> trans = null;

                        trans = stack.Peek();

                        using (_sync.Lock(trans))
                            trans.Rollback();
                    }
                }

                if (!allThreads && _ambientCache.ContainsKey(id))
                {
                    var tran = _ambientCache[id];

                    using (var tLock = _sync.Lock(tran))
                        tLock.Transaction.Rollback();
                }
                else if (allThreads)
                {
                    lock (_syncRoot)
                    {
                        while (_ambientCache.Count > 0)
                        {
                            var ambient = _ambientCache.First();

                            using (var tLock = _sync.Lock(ambient.Value))
                                tLock.Transaction.Rollback();
                        }
                    }
                }
            }
        }

        public void RollBack(ITransaction<IdType, EntityType> transaction)
        {
            Trace.TraceInformation("Rolling back transaction {0}", transaction.Id);

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

            using (_sync.Lock(transaction))
            {
                if (stack != null)
                {
                    while (stack.Count > 0 && stack.Peek().Id != transaction.Id)
                    {
                        ITransaction<IdType, EntityType> child = null;

                        lock (_syncRoot)
                            child = stack.Peek();

                        Trace.TraceInformation("Rolling back child transaction {0}", child.Id);

                        using (_sync.Lock(child))
                            child.Rollback();
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

                using (_sync.Lock(transaction))
                    transaction.MarkComplete();
            }
        }

        public void CommitAmbientTransactions()
        {
            lock (_syncRoot)
            {
                if (_ambientCache == null || _ambientCache.Count <= 0)
                    return;

                while (_ambientCache.Count > 0)
                {
                    var a = _ambientCache.First();

                    if (a.Value == null || a.Value.IsComplete)
                        continue;

                    using (var tLock = _sync.Lock(a.Value))
                    {
                        Trace.TraceInformation("Committing ambient transaction {0}", a.Value.Id);

                        a.Value.Commit();
                    }
                }
            }
        }

        public void Commit(ITransaction<IdType, EntityType> transaction)
        {
            Trace.TraceInformation("Committing transaction {0}", transaction.Id);

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

            using (_sync.Lock(transaction))
            {
                if (stack != null)
                {
                    while (stack != null && stack.Count > 0 && stack.Peek().Id != transaction.Id)
                    {
                        ITransaction<IdType, EntityType> child = null;

                        lock (_syncRoot)
                            child = stack.Peek();

                        Trace.TraceInformation("Committing child transaction {0}", child.Id);

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
        }

        public void CommitAll(bool allThreads)
        {
            Trace.TraceInformation("Committing all transactions");

            var id = Thread.CurrentThread.ManagedThreadId;

            lock (_syncRoot)
            {
                while ((allThreads && _transactionCache.Count > 0) || _transactionCache.ContainsKey(id))
                {
                    var kv = _transactionCache.LastOrDefault();

                    if (!allThreads && kv.Key != id)
                        continue;

                    var stack = kv.Value;

                    while (stack.Count > 0)
                    {
                        ITransaction<IdType, EntityType> trans = null;

                        trans = stack.Peek();

                        using (_sync.Lock(trans))
                            trans.Commit();
                    }
                }

                if (!allThreads && _ambientCache.ContainsKey(id))
                {
                    var tran = _ambientCache[id];

                    using (var tLock = _sync.Lock(tran))
                        tLock.Transaction.Rollback();
                }
                else if (allThreads)
                {
                    lock (_syncRoot)
                    {
                        while (_ambientCache.Count > 0)
                        {
                            var ambient = _ambientCache.First();

                            using (var tLock = _sync.Lock(ambient.Value))
                                tLock.Transaction.Commit();
                        }
                    }
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

            Trace.TraceInformation("Returning {0} cached transaction enlisted actions", items.Count());

            return items;
        }


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
        }
    }
}
