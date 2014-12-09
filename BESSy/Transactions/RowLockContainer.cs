using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Synchronization;

namespace BESSy.Transactions
{
    public class RowLockContainer<SegmentType> : List<RowLock<SegmentType>>, IDisposable
    {
        public void Dispose()
        {
            lock (this)
                foreach (var r in this)
                    r.Dispose();
        }
    }
}
