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
    internal interface IIndexUpdate<IndexType, SegmentType>
    {
        long[] PushIndexes(IEnumerable<NTreeItem<IndexType, SegmentType>> items);

        long[] PopIndexes(IEnumerable<IndexType> indexes);

        long[] PopSegments(IEnumerable<SegmentType> segments);
    }

    public interface IIndex<IndexType, EntityType, SegmentType> : IFlush, ISweep, ILoadAndRegister<EntityType>, IDisposable
    {
        long Length { get; }
        SegmentType FetchSegment(IndexType index);
        SegmentType[] FetchSegments(IndexType index);
        SegmentType[] FetchSegments(IndexType[] indexes);
        SegmentType[] FetchSegments(IndexType startIndex, IndexType endIndex);
        IndexType FetchIndex(SegmentType dbSegment, out long indexSegment);
        IndexType[] FetchIndexes(SegmentType dbSegment, out long[] indexSegments);
        IndexType[] FetchIndexes(SegmentType[] dbSegments, out long[] indexSegments);
        void Rebuild(long newLength);
    }

    public class Index<IndexType, EntityType, SegmentType> : IIndex<IndexType, EntityType, SegmentType>, IIndexUpdate<IndexType, SegmentType>
    {
        public Index
            (string fileName
            , string indexToken
            , bool unique)
        {
            _pTree = new PTree<IndexType, EntityType, SegmentType>(indexToken, fileName, unique);
        }

        public Index
            (string fileNamePath,
            string indexToken,
            bool unique,
            int startingSize,
            IBinConverter<IndexType> indexConverter,
            IBinConverter<SegmentType> segmentConverter,
            IRowSynchronizer<long> rowSynchronizer,
            IRowSynchronizer<int> pageSynchronizer)
        {
            _pTree = new PTree<IndexType, EntityType, SegmentType>(indexToken, fileNamePath, unique, startingSize, indexConverter, segmentConverter, rowSynchronizer, pageSynchronizer);
        }

        public Index
        (string fileNamePath,
        string indexToken,
        bool unique,
        int startingSize,
        Func<EntityType, IndexType> indexer,
        IBinConverter<IndexType> indexConverter,
        IBinConverter<SegmentType> segmentConverter,
        IRowSynchronizer<long> rowSynchronizer,
        IRowSynchronizer<int> pageSynchronizer)
        {
            _pTree = new PTree<IndexType, EntityType, SegmentType>(indexToken, fileNamePath, unique, startingSize, indexConverter, segmentConverter, rowSynchronizer, pageSynchronizer, indexer);
        }

        protected PTree<IndexType, EntityType, SegmentType> _pTree;
        protected IAtomicFileManager<EntityType> _databaseFile;

        void OnTransactionCommitted(ITransaction<EntityType> transaction)
        {
            _pTree.UpdateFromTransaction(transaction);
        }

        void OnDatabaseReorganized(int recordsWritten)
        {
            _pTree.Reorganize(_databaseFile.AsEnumerable());
        }

         long[] IIndexUpdate<IndexType, SegmentType>.PushIndexes(IEnumerable<NTreeItem<IndexType, SegmentType>> items)
        {
            return _pTree.PushIndexes(items);
        }

        long[] IIndexUpdate<IndexType, SegmentType>.PopIndexes(IEnumerable<IndexType> indexes)
        {
            return _pTree.PopIndexes(indexes);
        }

        long[] IIndexUpdate<IndexType, SegmentType>.PopSegments(IEnumerable<SegmentType> segments)
        {
            return _pTree.PopSegments(segments);
        }

        public long Length
        {
            get { return _pTree.Length; }
        }

        public bool FileFlushQueueActive
        {
            get { return _pTree.FileFlushQueueActive; }
        }

        public SegmentType FetchSegment(IndexType index)
        {
            return _pTree.GetFirstByIndex(index);
        }

        public SegmentType[] FetchSegments(IndexType index)
        {
            return _pTree.GetByIndex(index);
        }

        public SegmentType[] FetchSegments(IndexType[] indexes)
        {
            List<SegmentType> segments = new List<SegmentType>();

            foreach (var i in indexes)
                segments.AddRange(_pTree.GetByIndex(i));

            return segments.ToArray();
        }

        public SegmentType[] FetchSegments(IndexType startIndex, IndexType endIndex)
        {
            long[] loc;
            return _pTree.GetByIndexRangeInclusive(startIndex, endIndex, out loc);
        }

        public IndexType FetchIndex(SegmentType dbSegment, out long indexSegment)
        {
            return _pTree.GetFirstBySegment(dbSegment, out indexSegment);
        }

        public IndexType[] FetchIndexes(SegmentType dbSegment, out long[] indexSegments)
        {
            return _pTree.GetBySegment(dbSegment, out indexSegments);
        }

        public IndexType[] FetchIndexes(SegmentType[] dbSegments, out long[] indexSegments)
        {
            List<IndexType> indexes = new List<IndexType>();
            List<long> iSegs = new List<long>();
            long[] tSegs;

            foreach (var i in dbSegments)
            {
                indexes.AddRange(_pTree.GetBySegment(i, out tSegs));
                iSegs.AddRange(tSegs);
            }

            indexSegments = iSegs.ToArray();

            return indexes.ToArray();
        }

        public void Rebuild(long newLength)
        {
            _pTree.Rebuild(newLength);
        }

        public void RebuildFrom(IEnumerable<JObject[]> database)
        {
            _pTree.Reorganize(database);
        }

        public void Flush()
        {
           
        }

        public void Sweep()
        {
            
        }

        public long Load()
        {
            return _pTree.Load();
        }

        public void Register(IAtomicFileManager<EntityType> databaseFile)
        {
            //databaseFile.Rebuilt += new Rebuild<EntityType>(OnDatabaseRebuilt);
            databaseFile.Reorganized += new Reorganized<EntityType>(OnDatabaseReorganized);
            databaseFile.TransactionCommitted += new Committed<EntityType>(OnTransactionCommitted);

            _databaseFile = databaseFile;

            if (_databaseFile.Length > Length)
                _pTree.Reorganize(databaseFile.AsEnumerable());
            else if (_databaseFile.Length < Length)
                _pTree.Trim(_databaseFile.Length);
        }

        public void Dispose()
        {
            Trace.TraceInformation("Disposing Index.");

            Trace.TraceInformation("Wating for all indexUpdate file operations to complete");

            if (_pTree != null)
            {
                while (_pTree.FileFlushQueueActive)
                    Thread.Sleep(250);

                _pTree.Dispose();
            }

            Trace.TraceInformation("All indexUpdate file operations completed");


            if (_databaseFile != null)
                _databaseFile.TransactionCommitted -= new Committed<EntityType>(OnTransactionCommitted);

            _databaseFile = null;
        }
    }
}
