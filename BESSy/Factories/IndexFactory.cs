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
using BESSy.Cache;
using BESSy.Files;
using BESSy.Indexes;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Synchronization;
using BESSy.Serialization.Converters;

namespace BESSy.Factories
{
    public interface IIndexFactory
    {
        Index<IndexType, EntityType, SegmentType> Create<IndexType, EntityType, SegmentType>
        (string fileName
        , string indexToken
        , bool unique);

        Index<IndexType, EntityType, SegmentType> Create<IndexType, EntityType, SegmentType>(string fileNamePath, string indexToken, bool unique, int startingSize, IBinConverter<IndexType> indexConverter, IBinConverter<SegmentType> segmentConverter, IRowSynchronizer<long> rowSynchronizer, IRowSynchronizer<int> pageSynchronizer);

        Index<IndexType, EntityType, SegmentType> Create<IndexType, EntityType, SegmentType>(string fileNamePath, string indexToken, bool unique, int startingSize, IBinConverter<IndexType> indexConverter, IBinConverter<SegmentType> segmentConverter, IRowSynchronizer<long> rowSynchronizer, IRowSynchronizer<int> pageSynchronizer, Func<EntityType, IndexType> indexer);
    }

    public class IndexFactory : IIndexFactory
    {
        public Index<IndexType, EntityType, SegmentType> Create<IndexType, EntityType, SegmentType>(string fileName, string indexToken, bool unique)
        {
            return new Index<IndexType, EntityType, SegmentType>(fileName, indexToken, unique);
        }

        public Index<IndexType, EntityType, SegmentType> Create<IndexType, EntityType, SegmentType>(string fileNamePath, string indexToken, bool unique, int startingSize, IBinConverter<IndexType> indexConverter, IBinConverter<SegmentType> segmentConverter, IRowSynchronizer<long> rowSynchronizer, IRowSynchronizer<int> pageSynchronizer)
        {
            return new Index<IndexType, EntityType, SegmentType>(fileNamePath, indexToken, unique, startingSize, indexConverter, segmentConverter, rowSynchronizer, pageSynchronizer);
        }

        public Index<IndexType, EntityType, SegmentType> Create<IndexType, EntityType, SegmentType>(string fileNamePath, string indexToken, bool unique, int startingSize, IBinConverter<IndexType> indexConverter, IBinConverter<SegmentType> segmentConverter, IRowSynchronizer<long> rowSynchronizer, IRowSynchronizer<int> pageSynchronizer, Func<EntityType, IndexType> indexer)
        {
            return new Index<IndexType, EntityType, SegmentType>(fileNamePath, indexToken, unique, startingSize, indexer, indexConverter, segmentConverter, rowSynchronizer, pageSynchronizer);
        }
    }
}
