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
using BESSy.Cache;
using BESSy.Seeding;
using BESSy.Serialization.Converters;
using BESSy.Serialization;
using BESSy.Synchronization;

namespace BESSy.Factories
{
    public interface IAtomicFileManagerFactory
    {
        /// <summary>
        ///  Creates a File Manager for an existing data file.
        /// </summary>
        /// <typeparam name="SeedType"></typeparam>
        /// <typeparam name="EntityType"></typeparam>
        /// <param name="fileNamePath"></param>
        /// <param name="bufferSize"></param>
        /// <param name="startingSize"></param>
        /// <param name="maxBlockSize"></param>
        /// <param name="segmentSeed"></param>
        /// <param name="formatter"></param>
        /// <param name="rowSynchronizer"></param>
        /// <returns></returns>
        IAtomicFileManager<EntityType> Create<IdType, EntityType>(string fileNamePath, int bufferSize, int startingSize, int maxBlockSize, ISeed<Int32> segmentSeed, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer);

        /// <summary>
        /// Creates a File Manager for a new data file.
        /// </summary>
        /// <typeparam name="SeedType"></typeparam>
        /// <typeparam name="EntityType"></typeparam>
        /// <param name="fileNamePath"></param>
        /// <param name="bufferSize"></param>
        /// <param name="startingSize"></param>
        /// <param name="maxBlockSize"></param>
        /// <param name="entitySeed"></param>
        /// <param name="segmentSeed"></param>
        /// <param name="formatter"></param>
        /// <param name="rowSynchronizer"></param>
        /// <returns></returns>
        IAtomicFileManager<EntityType> Create<IdType, EntityType>(string fileNamePath, int bufferSize, int startingSize, int maxBlockSize, ISeed<IdType> entitySeed, ISeed<Int32> segmentSeed, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer);
    }

    internal class AtomicFileManagerFactory : IAtomicFileManagerFactory
    {
        public IAtomicFileManager<EntityType> Create<IdType, EntityType>(string fileNamePath, int bufferSize, int startingSize, int maxBlockSize, ISeed<Int32> segmentSeed, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer)
        {
            return new AtomicFileManager<EntityType>(fileNamePath, bufferSize, startingSize, maxBlockSize, segmentSeed, formatter, rowSynchronizer);
        }

        public IAtomicFileManager<EntityType> Create<IdType, EntityType>(string fileNamePath, int bufferSize, int startingSize, int maxBlockSize, ISeed<IdType> entitySeed, ISeed<Int32> segmentSeed, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer)
        {
            return new AtomicFileManager<EntityType>(fileNamePath, bufferSize, startingSize, maxBlockSize, entitySeed, segmentSeed, formatter, rowSynchronizer);
        }
    }
}
