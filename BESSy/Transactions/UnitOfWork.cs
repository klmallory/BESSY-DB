using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Transactions
{
    public interface IUnitOfWork : IDisposable
    {
        Guid Id { get; }
        void EnlistTransaction(ITransaction transaction);
        IList<ITransaction> GetTransactions();
    }

    internal class UnitOfWork : IUnitOfWork
    {
        public UnitOfWork(IUnitOfWorkManager manager)
        {
            Id = Guid.NewGuid();

            _manager = manager;
        }

        object _syncRoot = new object();
        IDictionary<Guid, ITransaction> _transactions = new Dictionary<Guid, ITransaction>();
        IUnitOfWorkManager _manager;

        public Guid Id { get; protected set; }
        public void EnlistTransaction(ITransaction transaction)
        {
            lock (_syncRoot)
                if (!_transactions.ContainsKey(transaction.Id))
                    _transactions.Add(transaction.Id, transaction);
        }

        public IList<ITransaction> GetTransactions()
        {
            lock (_syncRoot)
                return _transactions.Values.Reverse().ToList();
        }

        public void Commit()
        {
            _manager.Commit(this);
        }

        public void Rollback()
        {
            _manager.Rollback(this);
        }

        public void Dispose()
        {
            lock (_syncRoot)
                if (_transactions != null && _transactions.Count > 0 && _transactions.Any(t => !t.Value.IsComplete))
                    _manager.Rollback(this);
        }
    }
}
