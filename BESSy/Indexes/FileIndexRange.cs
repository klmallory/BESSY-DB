using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime;

namespace BESSy.Indexes.FileIndexRange
{
    public struct FileIndexRange
    {
        [TargetedPatchingOptOut("Performance Critical")]
        public FileIndexRange(long start, long end) : this()
        {
            SegmentStart = start;
            SegmentEnd = end;
        }

        public long SegmentStart { get; set; }
        public long SegmentEnd { get; set; }
    }
}
