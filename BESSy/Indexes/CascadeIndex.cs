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
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BESSy.Cache;
using BESSy.Extensions;
using BESSy.Factories;
using BESSy.Files;
using BESSy.Parallelization;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using BESSy.Json.Linq;
using BESSy.Transactions;
using System.IO;
using BESSy.Json;

namespace BESSy.Indexes
{
    public class CascadeIndex<IndexType, EntityType, SegmentType> : Index<IndexType, EntityType, SegmentType>
    {
        public CascadeIndex
    (string fileName
    , string indexToken
    , bool unique)
            : base(fileName, indexToken, unique)
        {
        }

        public CascadeIndex
            (string fileNamePath,
            string indexToken,
            bool unique,
            int startingSize,
            IBinConverter<IndexType> indexConverter,
            IBinConverter<SegmentType> segmentConverter,
            IRowSynchronizer<long> rowSynchronizer,
            IRowSynchronizer<int> pageSynchronizer)
            : base(fileNamePath, indexToken, unique, startingSize, indexConverter, segmentConverter, rowSynchronizer, pageSynchronizer)
        {

        }

        public CascadeIndex
        (string fileNamePath,
        string indexToken,
        bool unique,
        int startingSize,
        Func<EntityType, IndexType> indexer,
        IBinConverter<IndexType> indexConverter,
        IBinConverter<SegmentType> segmentConverter,
        IRowSynchronizer<long> rowSynchronizer,
        IRowSynchronizer<int> pageSynchronizer)
            : base(fileNamePath, indexToken, unique, startingSize, indexer, indexConverter, segmentConverter, rowSynchronizer, pageSynchronizer)
        {

        }

        public sealed override void Register(Files.IAtomicFileManager<EntityType> databaseFile)
        {
            _databaseFile = databaseFile;

            _pTree.Trim();
        }
    }
}
