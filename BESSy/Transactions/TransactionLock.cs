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
using System.Runtime;
using System.Text;
using System.Threading;
#if !MONO
    using Microsoft.Runtime;
#endif

namespace BESSy.Transactions
{
    public struct TransactionLock<IdType, EntityType> : IDisposable
    {
        private ITransactionSynchronizer<IdType, EntityType> _sync;
        bool _autoCommit;

        [TargetedPatchingOptOut("Performance critical.")]
        public TransactionLock(ITransactionSynchronizer<IdType, EntityType> sync, ITransaction<IdType, EntityType> transaction)
            : this(false, sync, transaction)
        {
            _sync = sync;
            TransactionId = transaction.Id;
            Transaction = transaction;
            LockId = Guid.NewGuid();
            ThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        [TargetedPatchingOptOut("Performance critical.")]
        public TransactionLock(bool autoCommit, ITransactionSynchronizer<IdType, EntityType> sync, ITransaction<IdType, EntityType> transaction)
            : this()
        {
            _autoCommit = autoCommit;
            _sync = sync;
            TransactionId = transaction.Id;
            Transaction = transaction;
            LockId = Guid.NewGuid();
            ThreadId = Thread.CurrentThread.ManagedThreadId;
        }
        
        public Guid LockId { get; private set; }
        public Guid TransactionId { get; private set; }
        public int ThreadId { get; private set; }
        public ITransaction<IdType, EntityType> Transaction { get; private set; }

        public void Dispose()
        {
            if (_sync != null)
                _sync.Unlock(this);
        }
    }
}
