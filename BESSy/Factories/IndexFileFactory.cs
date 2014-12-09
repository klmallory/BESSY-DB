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
using System.Text;
using BESSy.Files;
using BESSy.Serialization;
using BESSy.Synchronization;
using BESSy.Indexes;
using BESSy.Seeding;
using BESSy.Serialization.Converters;
using BESSy.Cache;

namespace BESSy.Factories
{
    public interface IIndexFileFactory
    {
         PTree<IndexType, EntityType, SegmentType> Create<IndexType, EntityType, SegmentType>(string fileNamePath, string indexToken);

        PTree<IndexType, EntityType, SegmentType> Create<IndexType, EntityType, SegmentType>
            (string fileNamePath, 
            string indexToken, 
            int startingSize, 
            IBinConverter<IndexType> indexConverter, 
            IBinConverter<SegmentType> segmentConverter, 
            IQueryableFormatter formatter, 
            IRowSynchronizer<long> rowSynchronizer, 
            IRowSynchronizer<int> pageSynchronizer);

        PTree<IndexType, EntityType, SegmentType> CreatePrimary<IndexType, EntityType, SegmentType>(string fileNamePath, string indexToken);

        PTree<IndexType, EntityType, SegmentType> CreatePrimary<IndexType, EntityType, SegmentType>(string fileNamePath, string indexToken, int bufferSize, int startingSize, IBinConverter<IndexType> indexConverter, IBinConverter<SegmentType> segmentConverter, IRowSynchronizer<long> rowSynchronizer, IRowSynchronizer<int> pageSynchronizer);
    }

    public class IndexFileFactory : IIndexFileFactory
    {
        public PTree<IndexType, EntityType, SegmentType> Create<IndexType, EntityType, SegmentType>(string fileNamePath, string indexToken)
        {
            return new PTree<IndexType, EntityType, SegmentType>(indexToken, fileNamePath, false);
        }

        public PTree<IndexType, EntityType, SegmentType> Create<IndexType, EntityType, SegmentType>
            (string fileNamePath, 
            string indexToken, 
            int startingSize, 
            IBinConverter<IndexType> indexConverter, 
            IBinConverter<SegmentType> segmentConverter, 
            IQueryableFormatter formatter, 
            IRowSynchronizer<long> rowSynchronizer, 
            IRowSynchronizer<int> pageSynchronizer)
        {
            return new PTree<IndexType, EntityType, SegmentType>(indexToken, fileNamePath, false, startingSize, indexConverter, segmentConverter, rowSynchronizer, pageSynchronizer);
        }

        public PTree<IndexType, EntityType, SegmentType> CreatePrimary<IndexType, EntityType, SegmentType>(string fileNamePath, string indexToken)
        {
            return new PTree<IndexType, EntityType, SegmentType>(indexToken, fileNamePath, true);
        }

        public PTree<IndexType, EntityType, SegmentType> CreatePrimary<IndexType, EntityType, SegmentType>(string fileNamePath, string indexToken, int bufferSize, int startingSize, IBinConverter<IndexType> indexConverter, IBinConverter<SegmentType> segmentConverter, IRowSynchronizer<long> rowSynchronizer, IRowSynchronizer<int> pageSynchronizer)
        {
            return new PTree<IndexType, EntityType, SegmentType>(fileNamePath, indexToken, true , startingSize, indexConverter, segmentConverter, rowSynchronizer, pageSynchronizer);
        }
    }
}
