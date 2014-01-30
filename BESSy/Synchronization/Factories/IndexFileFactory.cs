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

namespace BESSy.Factories
{
    public interface IIndexFileFactory
    {
         IIndexFileManager<IndexType, EntityType, int> Create<IndexType, EntityType>(string fileNamePath, string indexToken, int bufferSize, int startingSize, int maximumBlockSize, IBinConverter<IndexType> propertyConverter, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer);

        IPrimaryIndexFileManager<IndexType, EntityType, int> CreatePrimary<IndexType, EntityType>(string fileNamePath, int bufferSize, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer);
        IPrimaryIndexFileManager<IndexType, EntityType, int> CreatePrimary<IndexType, EntityType>(string fileNamePath, int bufferSize, IQueryableFormatter formatter, ISeed<IndexType> seed, IRowSynchronizer<int> rowSynchronizer);
    }

    public class IndexFileFactory : BESSy.Factories.IIndexFileFactory 
    {
        public IIndexFileManager<IndexType, EntityType, int> Create<IndexType, EntityType>(string fileNamePath, string indexToken, int bufferSize, int startingSize, int maximumBlockSize, IBinConverter<IndexType> propertyConverter, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer)
        {
            return new IndexFileManager<IndexType, EntityType>(fileNamePath, indexToken, bufferSize, startingSize, maximumBlockSize, propertyConverter, formatter, rowSynchronizer);
        }

        public IPrimaryIndexFileManager<IndexType, EntityType, int> CreatePrimary<IndexType, EntityType>(string fileNamePath, int bufferSize, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer)
        {
            return new PrimaryIndexFileManager<IndexType, EntityType>(fileNamePath, bufferSize, formatter, rowSynchronizer);
        }

        public IPrimaryIndexFileManager<IndexType, EntityType, int> CreatePrimary<IndexType, EntityType>(string fileNamePath, int bufferSize, IQueryableFormatter formatter, ISeed<IndexType> seed, IRowSynchronizer<int> rowSynchronizer)
        {
            return new PrimaryIndexFileManager<IndexType, EntityType>(fileNamePath, bufferSize, formatter, seed, rowSynchronizer);
        }
    }
}
