using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using BESSy.Serialization.Converters;

namespace BESSy.Synchronization
{
    public interface ISynchronize<RowType>
    {
        IRowSynchronizer<RowType> Synchronizer { get; }
    }

    public interface IRowSynchronizer<RowType>
    {
        bool HasLocks();

        RowLock<RowType> LockAll();
        RowLock<RowType> LockAll(int milliseconds);
        bool TryLockAll(int milliseconds, out RowLock<RowType> rowLock);

        RowLock<RowType> Lock(RowType row);
        RowLock<RowType> Lock(RowType row, int milliseconds);
        bool TryLock(RowType row, int milliseconds, out RowLock<RowType> rowLock);

        RowLock<RowType> Lock(Range<RowType> rows);
        RowLock<RowType> Lock(Range<RowType> rows, int milliseconds);
        bool TryLock(Range<RowType> rows, int milliseconds, out RowLock<RowType> rowLock);

        RowLock<RowType> Lock(RowType row, FileAccess access, FileShare share);
        RowLock<RowType> Lock(RowType row, FileAccess access, FileShare share, int milliseconds);
        bool TryLock(RowType row, FileAccess access, FileShare share, int milliseconds, out RowLock<RowType> rowLock);

        RowLock<RowType> Lock(Range<RowType> rows, FileAccess access, FileShare share);
        RowLock<RowType> Lock(Range<RowType> rows, FileAccess access, FileShare share, int milliseconds);
        bool TryLock(Range<RowType> rows, FileAccess access, FileShare share, int milliseconds, out RowLock<RowType> rowLock);

        void Unlock(RowLock<RowType> rowLock);
    }

    public class RowSynchronizer<RowType> : IRowSynchronizer<RowType>
    {
        public RowSynchronizer(IBinConverter<RowType> rowIdConverter)
        {
            _rowIdConverter = rowIdConverter;
        }

        int? _exclusiveLock = null;
        string _rowLockError = "Could not get a lock on rows: {0} through {1} in the {2} milliseconds allotted.";

        object _syncRoot = new object();
        IBinConverter<RowType> _rowIdConverter;
        IDictionary<Guid, RowLock<RowType>> _currentLocks = new Dictionary<Guid, RowLock<RowType>>();
        
        bool Intersects(Range<RowType> range, Range<RowType> other)
        {
            if (
                _rowIdConverter.Compare(range.StartInclusive, other.EndInclusive) == 0
                || _rowIdConverter.Compare(range.EndInclusive, other.StartInclusive) == 0
                || _rowIdConverter.Compare(range.StartInclusive, other.StartInclusive) == 0
                || _rowIdConverter.Compare(range.EndInclusive, other.EndInclusive) == 0

                || (_rowIdConverter.Compare(other.StartInclusive, range.StartInclusive) < 0
                && _rowIdConverter.Compare(other.EndInclusive, range.StartInclusive) > 0)

                || (_rowIdConverter.Compare(other.StartInclusive, range.EndInclusive) < 0
                && _rowIdConverter.Compare(other.EndInclusive, range.EndInclusive) > 0))

                return true;
            
            return false;
        }

        bool IsLockedFor(Range<RowType> rows, FileAccess access)
        {
            bool locked = (_exclusiveLock.HasValue && _exclusiveLock.Value != Thread.CurrentThread.ManagedThreadId)

                || (_currentLocks.Where(t => t.Value.ThreadId != Thread.CurrentThread.ManagedThreadId)
                    .Any(d => Intersects(rows, d.Value.Rows) 
                        && Conflicts(access, d.Value.Share)));

            return locked;
        }

        bool Conflicts(FileAccess accessRequest, FileShare sharing)
        {
            if (accessRequest <= FileAccess.Read 
                && sharing == FileShare.None)
                return true;

            if (accessRequest >= FileAccess.ReadWrite &&
                sharing < FileShare.Write)
                return true;

            return false;
        }
        RowLock<RowType> AddNewLock(Range<RowType> rows)
        {
            var rowLock = new RowLock<RowType>(this, rows, Thread.CurrentThread.ManagedThreadId);

            _currentLocks.Add(rowLock.Id, rowLock);

            return rowLock;
        }

        public bool HasLocks()
        {
            lock (_syncRoot)
                if (_currentLocks.Count > 0)
                    return true;

            return false;
        }

        public RowLock<RowType> LockAll()
        {
            try
            {
                lock (_syncRoot)
                    _exclusiveLock = Thread.CurrentThread.ManagedThreadId;

                return Lock(new Range<RowType>(_rowIdConverter.Min, _rowIdConverter.Max));
            }
            finally
            {
                _exclusiveLock = null;
            }
        }

        public RowLock<RowType> LockAll(int milliseconds)
        {
            try
            {
                lock (_syncRoot)
                    _exclusiveLock = Thread.CurrentThread.ManagedThreadId;

                return Lock(new Range<RowType>(_rowIdConverter.Min, _rowIdConverter.Max), milliseconds);
            }
            finally
            {
                _exclusiveLock = null;
            }
        }

        public bool TryLockAll(int milliseconds, out RowLock<RowType> rowLock)
        {
            try
            {
                lock (_syncRoot)
                    _exclusiveLock = Thread.CurrentThread.ManagedThreadId;

                return TryLock(new Range<RowType>(_rowIdConverter.Min, _rowIdConverter.Max), milliseconds, out rowLock);
            }
            finally
            {
                _exclusiveLock = null;
            }
        }

        public RowLock<RowType> Lock(RowType row)
        {
            var rows = new Range<RowType>(row, row);

            return Lock(rows);
        }

        public RowLock<RowType> Lock(RowType row, int milliseconds)
        {
            var rows = new Range<RowType>(row, row);

            return Lock(rows, milliseconds);
        }

        public bool TryLock(RowType row, int milliseconds, out RowLock<RowType> rowLock)
        {
            var rows = new Range<RowType>(row, row);

            return TryLock(rows, milliseconds, out rowLock);
        }

        public RowLock<RowType> Lock(RowType row, FileAccess access, FileShare share)
        {
            var rows = new Range<RowType>(row, row);

            return Lock(rows, access, share);
        }

        public RowLock<RowType> Lock(RowType row, FileAccess access, FileShare share, int milliseconds)
        {
            var rows = new Range<RowType>(row, row);

            return Lock(rows, access, share, milliseconds);
        }

        public bool TryLock(RowType row, FileAccess access, FileShare share, int milliseconds, out RowLock<RowType> rowLock)
        {
            var rows = new Range<RowType>(row, row);

            return TryLock(rows, access, share, milliseconds, out rowLock);
        }

        public RowLock<RowType> Lock(Range<RowType> rows)
        {
            return Lock(rows, FileAccess.ReadWrite, FileShare.None);
        }

        public RowLock<RowType> Lock(Range<RowType> rows, int milliseconds)
        {
            return Lock(rows, FileAccess.ReadWrite, FileShare.None, milliseconds);
        }

        public bool TryLock(Range<RowType> rows, int milliseconds, out RowLock<RowType> rowLock)
        {
            return TryLock(rows, FileAccess.ReadWrite, FileShare.None, milliseconds, out rowLock);
        }

        public RowLock<RowType> Lock(Range<RowType> rows, FileAccess access, FileShare share)
        {
            Monitor.Enter(_syncRoot);

            try
            {
                bool locked = IsLockedFor(rows, access);

                while (locked)
                {
                    Monitor.Exit(_syncRoot);

                    Thread.Sleep(50);

                    Monitor.Enter(_syncRoot);

                    locked = IsLockedFor(rows, access);
                }

                var rowLock = AddNewLock(rows);

                return rowLock;
            }
            finally { Monitor.Exit(_syncRoot); }
        }

        public RowLock<RowType> Lock(Range<RowType> rows, FileAccess access, FileShare share, int milliseconds)
        {
            //10,000 ticks to one millisecond.
            long timeout = DateTime.Now.Ticks + (milliseconds * 10000);

            Monitor.Enter(_syncRoot);

            try
            {
                bool locked = IsLockedFor(rows, access);

                while (locked)
                {
                    if (DateTime.Now.Ticks > timeout)
                        throw new RowLockTimeoutException(string.Format(_rowLockError, rows.StartInclusive, rows.EndInclusive, milliseconds));

                    Monitor.Exit(_syncRoot);

                    Thread.Sleep(50);

                    Monitor.Enter(_syncRoot);

                    locked = IsLockedFor(rows, access);
                }

                var rowLock = AddNewLock(rows);

                return rowLock;
            }
            finally { Monitor.Exit(_syncRoot); }
        }

        public bool TryLock(Range<RowType> rows, FileAccess access, FileShare share, int milliseconds, out RowLock<RowType> rowLock)
        {
            //10,000 ticks to one millisecond.
            long timeout = DateTime.Now.Ticks + (milliseconds * 10000);

            rowLock = new RowLock<RowType>(this, new Range<RowType>(default(RowType), default(RowType)), Thread.CurrentThread.ManagedThreadId, share);

            Monitor.Enter(_syncRoot);

            try
            {
                bool locked = IsLockedFor(rows, access);

                while (locked)
                {
                    if (DateTime.Now.Ticks > timeout)
                        return false;

                    Monitor.Exit(_syncRoot);

                    Thread.Sleep(50);

                    Monitor.Enter(_syncRoot);

                    locked = IsLockedFor(rows, access);
                }

                rowLock = AddNewLock(rows);
            }
            finally { Monitor.Exit(_syncRoot); }

            return true;
        }



        public void Unlock(RowLock<RowType> rowLock)
        {
            lock (_syncRoot)
                if (_currentLocks.ContainsKey(rowLock.Id))
                    _currentLocks.Remove(rowLock.Id);
        }
    }
}
