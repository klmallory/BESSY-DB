using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Serialization.Converters;

namespace BESSy.Synchronization
{
    public struct Range<I>
    {
        public Range(I single) : this()
        {
            StartInclusive = single;
            EndInclusive = single;
        }

        public Range(I start, I end) : this()
        {
            StartInclusive = start;
            EndInclusive = end;
        }

        public I StartInclusive { get; set; }
        public I EndInclusive { get; set; }
    }
}
