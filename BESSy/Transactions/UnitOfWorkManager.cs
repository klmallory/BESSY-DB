using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Files;
using System.Diagnostics;

namespace BESSy.Transactions
{
    public interface IUnitOfWorkManager
    {
        IUnitOfWork BeginUnitOfWork();
        void Commit(IUnitOfWork unit);
        void EnlistTransaction(ITransaction transaction);
        void EnlistTransactions(params ITransaction[] transactions);
        void Rollback(IUnitOfWork unit);
        void CommitAll();
        void RollbackAll();
    }

    internal class UnitOfWorkManager : BESSy.Transactions.IUnitOfWorkManager
    {
        object _syncRoot = new object();
        public Queue<IUnitOfWork> _units = new Queue<IUnitOfWork>();

        public IUnitOfWork BeginUnitOfWork()
        {
            var u = new UnitOfWork(this);

            lock (_syncRoot)
                _units.Enqueue(u);

            return u;
        }

        public void EnlistTransaction(ITransaction transaction)
        {
            lock (_syncRoot)
            {
                if (_units.Count < 1)
                    _units.Enqueue(new UnitOfWork(this));

                _units.Peek().EnlistTransaction(transaction);
            }
        }

        public void EnlistTransactions(params ITransaction[] transactions)
        {
            lock (_syncRoot)
                foreach(var t in transactions)
                _units.Peek().EnlistTransaction(t);
        }

        public void Commit(IUnitOfWork unit)
        {
            IList<ITransaction> transactions = null;

            try
            {
                    transactions = unit.GetTransactions().ToList();

                foreach (var t in transactions)
                    t.Commit();
            }
            catch (Exception ex)
            {
                Trace.TraceError("Commit of unit of work failed: {0}", ex.ToString());

                if (transactions != null)
                    foreach (var t in transactions)
                        t.Rollback();
            }
        }

        public void Rollback(IUnitOfWork unit)
        {
            IList<ITransaction> transactions = null;

            try
            {
                    transactions = unit.GetTransactions().ToList();

                foreach (var t in transactions)
                    t.Rollback();
            }
            catch (Exception ex)
            {
                Trace.TraceError("Rollback of unit of work failed: {0}", ex.ToString());
            }
        }

        public void CommitAll()
        {
            try
            {
                foreach (var unit in _units)
                {
                   var transactions = unit.GetTransactions().ToList();

                    foreach (var t in transactions)
                        t.Commit();
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Commit all for unit of work manager failed: {0}", ex.ToString());
            }
        }

        public void RollbackAll()
        {
            try
            {
                foreach (var unit in _units)
                {
                    var transactions = unit.GetTransactions().ToList();

                    foreach (var t in transactions)
                        t.Rollback();
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Rollback all for unit of work manager failed: {0}", ex.ToString());
            }
        }
    }
}
