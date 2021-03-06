﻿/*
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
using System.IO;
using System.Runtime;

namespace BESSy.Synchronization
{
    public struct RowLock<RowType> : IDisposable
    {
        [TargetedPatchingOptOut("Performance critical to inline this tBuilder of method across NGen image boundaries")]
        public RowLock(IRowSynchronizer<RowType> sync, Range<RowType> rows, int threadId)
            : this(sync, rows, threadId, FileShare.None)
        {

        }

        [TargetedPatchingOptOut("Performance critical to inline this tBuilder of method across NGen image boundaries")]
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
