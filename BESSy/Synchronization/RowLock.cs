using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace BESSy.Synchronization
{
    public struct RowLock<RowType> : IDisposable
    {
        public RowLock(IRowSynchronizer<RowType> sync, Range<RowType> rows, int threadId)
            : this(sync, rows, threadId, FileShare.None)
        {

        }

        public RowLock(IRowSynchronizer<RowType> sync, Range<RowType> rows, int threadId, FileShare share)
            : this()
        {
            _sync = sync;
            Rows = rows;
            ThreadId = threadId;
            Share = share;

            Id = Guid.NewGuid();
        }

        IRowSynchronizer<RowType> _sync;

        public Guid Id { get; private set; }
        public Range<RowType> Rows { get; private set; }
        public int ThreadId { get; private set; }
        public FileShare Share { get; private set; }

        public void Dispose()
        {
            if (_sync != null)
                _sync.Unlock(this);
        }
    }
}
