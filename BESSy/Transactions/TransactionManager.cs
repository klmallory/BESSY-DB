using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using BESSy.Factories;

namespace BESSy.Transactions
{
    public delegate void TransactionCommit<IdType, EntityType>
    (ITransaction<IdType, EntityType> transaction
    , IList<ITransaction<IdType, EntityType>> childTransactions);

    public interface ITransactionManager<IdType, EntityType> : IDisposable
    {
        ITransaction<IdType, EntityType> BeginTransaction();
        void RollBack(ITransaction<IdType, EntityType> transaction);
        void RollBackAll();
        void Commit(ITransaction<IdType, EntityType> transaction);
        void CommitAll();
        event TransactionCommit<IdType, EntityType> TransactionCommitted;
    }

    public class TransactionManager<IdType, EntityType> : ITransactionManager<IdType, EntityType>, IDisposable
    {
        public TransactionManager()
            : this(new TransactionFactory<IdType, EntityType>())
        {
        }

        public TransactionManager(ITransactionFactory<IdType, EntityType> transactionFactory)
        {
            _transactionFactory = transactionFactory;
        }

        ITransactionFactory<IdType, EntityType> _transactionFactory;
        object _syncRoot = new object();
        IDictionary<Guid, ITransaction<IdType, EntityType>> _transactionCache = new Dictionary<Guid, ITransaction<IdType, EntityType>>();
        Stack<Guid> _activeTransactions = new Stack<Guid>();

        public virtual ITransaction<IdType, EntityType> BeginTransaction()
        {
            var tran = _transactionFactory.Create(this);

            lock (_syncRoot)
            {
                _transactionCache.Add(tran.Id, tran);
                _activeTransactions.Push(tran.Id);
            }

            return tran;
        }

        public void RollBackAll()
        {
            lock (_syncRoot)
                while (_activeTransactions.Count > 0)
                    _transactionCache.Remove(_activeTransactions.Pop());
        }

        public void RollBack(ITransaction<IdType, EntityType> transaction)
        {
            if (transaction == null)
                return;

            if (_activeTransactions.Count < 1)
                throw new TransactionStateException("Transaction is no longer active.");

            var children = new List<ITransaction<IdType, EntityType>>();

            lock (_syncRoot)
            {
                var next = _activeTransactions.Pop();

                while (next != transaction.Id)
                {
                    if (_activeTransactions.Count < 1)
                        throw new TransactionStateException("Transaction is no longer active, or, transaction id not found.");

                    _transactionCache.Remove(next);

                    next = _activeTransactions.Pop();
                }
            }
        }

        public void Commit(ITransaction<IdType, EntityType> transaction)
        {
            if (_activeTransactions.Count < 1)
                throw new TransactionStateException("Transaction is no longer active.");

            var children = new List<ITransaction<IdType, EntityType>>();
            ITransaction<IdType, EntityType> trans = null;

            lock (_syncRoot)
            {
                var next = _activeTransactions.Pop();

                while (next != transaction.Id)
                {
                    if (_activeTransactions.Count < 1)
                        throw new TransactionStateException("Transaction is no longer active, or, transaction id not found.");

                    children.Add(_transactionCache[next]);

                    next = _activeTransactions.Pop();
                }

                trans = _transactionCache[next];
            }

            InvokeTransactionCommit(trans, children);
        }

        public void CommitAll()
        {
            if (_activeTransactions.Count < 1)
                return;

            var children = new List<ITransaction<IdType, EntityType>>();
            ITransaction<IdType,EntityType> rootTrans = null;

            lock (_syncRoot)
            {
                var next = _activeTransactions.Pop();

                while (_activeTransactions.Count > 0)
                {
                    children.Add(_transactionCache[next]);

                    next = _activeTransactions.Pop();
                }

                rootTrans = _transactionCache[next];
            }

            InvokeTransactionCommit(rootTrans, children);
        }

        public IEnumerable<IEnumerable<EntityType>> GetCached()
        {
            lock (_syncRoot)
            {
                if (_transactionCache.Count < 1)
                    return new List<IList<EntityType>>();

                return _transactionCache.Values.Select(t => t.GetEnlistedItems());
            }
        }

        #region TransactionCommit Event

        private void InvokeTransactionCommit(ITransaction<IdType, EntityType> transaction, IList<ITransaction<IdType, EntityType>> childTransactions)
        {
            if (TransactionCommitted != null)
                TransactionCommitted(transaction, childTransactions);
        }

        public event TransactionCommit<IdType, EntityType> TransactionCommitted;

        #endregion

        public void Dispose()
        {
            if (_activeTransactions.Count > 0)
                Trace.TraceError("Not all active transactions have been committed or rolled back. Data was lost.");
        }
    }
}
