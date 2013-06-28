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

namespace BESSy.Transactions
{
    public struct TransactionResult<EntityType>
    {
        [TargetedPatchingOptOut("Performance critical.")]
        public TransactionResult(int segment, Action action, EntityType entity)
            : this()
        {
            Segment = segment;
            Action = action;
            Entity = entity;
        }

        public int Segment { get; set; }
        public Action Action { get; set; }
        public EntityType Entity { get; set; }
    }

    public struct TransactionIndexResult<IndexType>
    {
        [TargetedPatchingOptOut("Performance critical.")]
        public TransactionIndexResult(IndexType index, Action action, int segment, int indexSegment) : this()
        {
            Index = index;
            Action = action;
            Segment = segment;
            IndexSegment = indexSegment;
        }

        public IndexType Index { get; set; }
        public Action Action { get; set; }
        public int Segment { get; set; }
        public int IndexSegment { get; set; }
    }
}
