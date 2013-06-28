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
using System.Threading;
using BESSy.Serialization.Converters;

namespace BESSy.Transactions
{
    public interface ISynchronizeTransactions<IdType, EntityType>
    {
        ITransactionSynchronizer<IdType, EntityType> Synchronizer { get; }
    }

    public interface ITransactionSynchronizer<IdType, EntityType> : IDisposable
    {
        bool HasLocks();

        //TransactionLock<IdType, EntityType> LockAll();
        //TransactionLock<IdType, EntityType> LockAll(int milliseconds);
        //bool TryLockAll(int milliseconds, out TransactionLock<IdType, EntityType> rowLock);

        TransactionLock<IdType, EntityType> Lock(ITransaction<IdType, EntityType> transaction);
        TransactionLock<IdType, EntityType> Lock(ITransaction<IdType, EntityType> transaction, int milliseconds);
        bool TryLock(ITransaction<IdType, EntityType> transaction, int milliseconds, out TransactionLock<IdType, EntityType> tranLock);

        void Unlock(TransactionLock<IdType, EntityType> tranLock);
    }

    public class TransactionSynchronizer<IdType, EntityType> : ITransactionSynchronizer<IdType, EntityType>
    {
        public TransactionSynchronizer()
        {

        }

        //int? _exclusiveLock = null;
        string _tranLockError = "Could not get a lock on transaction id {0} in {1} milliseconds";

        object _syncRoot = new object();
        IBinConverter<Guid> _tranIdConverter = new BinConverterGuid();
        IDictionary<Guid, TransactionLock<IdType, EntityType>> _currentLocks = new Dictionary<Guid, TransactionLock<IdType, EntityType>>();

        bool IsLockedFor(Guid tranId)
        {
            lock (_syncRoot)
            {
                bool locked = _currentLocks.Any(t => _tranIdConverter.Compare(t.Value.TransactionId, tranId) == 0 
                    && t.Value.ThreadId != Thread.CurrentThread.ManagedThreadId);

                return locked;
            }
        }

        TransactionLock<IdType, EntityType> AddNewLock(ITransaction<IdType, EntityType> transaction)
        {
            var tranLock = new TransactionLock<IdType, EntityType>(this, transaction);

            _currentLocks.Add(tranLock.LockId, tranLock);

            return tranLock;
        }

        public bool HasLocks()
        {
            lock (_syncRoot)
                return _currentLocks.Count > 0;
        }

        public TransactionLock<IdType, EntityType> Lock(ITransaction<IdType, EntityType> transaction)
        {
            Monitor.Enter(_syncRoot);

            try
            {
                bool locked = IsLockedFor(transaction.Id);

                while (locked)
                {
                    Monitor.Exit(_syncRoot);

                    Thread.Sleep(50);

                    Monitor.Enter(_syncRoot);

                    locked = IsLockedFor(transaction.Id);
                }

                var rowLock = AddNewLock(transaction);

                return rowLock;
            }
            finally { Monitor.Exit(_syncRoot); }
        }

        public TransactionLock<IdType, EntityType> Lock(ITransaction<IdType, EntityType> transaction, int milliseconds)
        {
            //10,000 ticks to one millisecond.
            long timeout = DateTime.Now.Ticks + (milliseconds * 10000);

            Monitor.Enter(_syncRoot);

            try
            {
                bool locked = IsLockedFor(transaction.Id);

                while (locked)
                {
                    if (DateTime.Now.Ticks > timeout)
                        throw new TransactionLockTimeoutException(string.Format(_tranLockError, transaction.Id, milliseconds));

                    Monitor.Exit(_syncRoot);

                    Thread.Sleep(50);

                    Monitor.Enter(_syncRoot);

                    locked = IsLockedFor(transaction.Id);
                }

                var rowLock = AddNewLock(transaction);

                return rowLock;
            }
            finally { Monitor.Exit(_syncRoot); }
        }

        public bool TryLock(ITransaction<IdType, EntityType> transaction, int milliseconds, out TransactionLock<IdType, EntityType> tranLock)
        {
            //10,000 ticks to one millisecond.
            long timeout = DateTime.Now.Ticks + (milliseconds * 10000);

            tranLock = new TransactionLock<IdType, EntityType>(this, new Transaction<IdType, EntityType>());

            Monitor.Enter(_syncRoot);

            try
            {
                bool locked = IsLockedFor(transaction.Id);

                while (locked)
                {
                    if (DateTime.Now.Ticks > timeout)
                        return false;

                    Monitor.Exit(_syncRoot);

                    Thread.Sleep(50);

                    Monitor.Enter(_syncRoot);

                    locked = IsLockedFor(transaction.Id);
                }

                tranLock = AddNewLock(transaction);
            }
            finally { Monitor.Exit(_syncRoot); }

            return true;
        }

        public void Unlock(TransactionLock<IdType, EntityType> tranLock)
        {
            lock (_syncRoot)
                if (_currentLocks.ContainsKey(tranLock.LockId))
                    _currentLocks.Remove(tranLock.LockId);
        }

        public void Dispose()
        {
            lock (_syncRoot)
                _currentLocks.Clear();
        }
    }
}
