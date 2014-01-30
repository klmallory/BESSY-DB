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
        //IIndex<PropertyType, EntityType, int> Create<PropertyType, EntityType>(string fileName, string indexToken);
        //IIndex<PropertyType, EntityType, int> Create<PropertyType, EntityType>(string fileName, string indexToken,IBinConverter<PropertyType> propertyConverter);
        //IIndex<PropertyType, EntityType, int> Create<PropertyType, EntityType>(string fileName, string indexToken,IBinConverter<PropertyType> propertyConverter, IRepositoryCacheFactory cacheFactory);
        //IIndex<PropertyType, EntityType, int> Create<PropertyType, EntityType>(string fileName, string indexToken,IBinConverter<PropertyType> propertyConverter, IRepositoryCacheFactory cacheFactory, IIndexFileFactory indexFileFactory);
        //IPrimaryIndex<PropertyType, EntityType> CreatePrimary<PropertyType, EntityType>(string fileName);
        //IPrimaryIndex<PropertyType, EntityType> CreatePrimary<PropertyType, EntityType>(string fileName, IQueryableFormatter formatter);
        //IPrimaryIndex<PropertyType, EntityType> CreatePrimary<PropertyType, EntityType>(string fileName, IQueryableFormatter formatter, IRepositoryCacheFactory cacheFactory);
        //IPrimaryIndex<PropertyType, EntityType> CreatePrimary<PropertyType, EntityType>(string fileName, IQueryableFormatter formatter, IRepositoryCacheFactory cacheFactory, IIndexFileFactory indexFileFactory);
        IPrimaryIndex<PropertyType, EntityType> CreatePrimary<PropertyType, EntityType>(string fileName, IQueryableFormatter formatter, IRepositoryCacheFactory cacheFactory, IIndexFileFactory indexFileFactory, IRowSynchronizer<int> rowSynchronizer);
        //IPrimaryIndex<PropertyType, EntityType> CreatePrimary<PropertyType, EntityType>(string fileName, ISeed<PropertyType> seed);
        //IPrimaryIndex<PropertyType, EntityType> CreatePrimary<PropertyType, EntityType>(string fileName, ISeed<PropertyType> seed, IBinConverter<PropertyType> propertyConverter);
        //IPrimaryIndex<PropertyType, EntityType> CreatePrimary<PropertyType, EntityType>(string fileName, ISeed<PropertyType> seed, IBinConverter<PropertyType> propertyConverter, IRepositoryCacheFactory cacheFactory);
        //IPrimaryIndex<PropertyType, EntityType> CreatePrimary<PropertyType, EntityType>(string fileName, ISeed<PropertyType> seed, IBinConverter<PropertyType> propertyConverter, IRepositoryCacheFactory cacheFactory, IQueryableFormatter formatter);
        //IPrimaryIndex<PropertyType, EntityType> CreatePrimary<PropertyType, EntityType>(string fileName, ISeed<PropertyType> seed, IBinConverter<PropertyType> propertyConverter, IRepositoryCacheFactory cacheFactory, IQueryableFormatter formatter, IIndexFileFactory indexFileFactory);
        IPrimaryIndex<PropertyType, EntityType> CreatePrimary<PropertyType, EntityType>(string fileName, ISeed<PropertyType> seed, IBinConverter<PropertyType> propertyConverter, IRepositoryCacheFactory cacheFactory, IQueryableFormatter formatter, IIndexFileFactory indexFileFactory, IRowSynchronizer<int> rowSynchronizer);
    }

    public class IndexFactory : IIndexFactory
    {
        //public IIndex<PropertyType, EntityType, int> Create<PropertyType, EntityType>(string fileName, string indexToken)
        //{
        //    return new Index<PropertyType, EntityType>(fileName, indexToken);
        //}

        //public IIndex<PropertyType, EntityType, int> Create<PropertyType, EntityType>(string fileName, string indexToken, IBinConverter<PropertyType> propertyConverter)
        //{
        //    return new Index<PropertyType, EntityType>(fileName, indexToken, propertyConverter);
        //}

        //public IIndex<PropertyType, EntityType, int> Create<PropertyType, EntityType>
        //    (string fileName, 
        //    string indexToken,
        //    IBinConverter<PropertyType> propertyConverter,
        //    IRepositoryCacheFactory cacheFactory)
        //{
        //    return new Index<PropertyType, EntityType>(fileName, indexToken, propertyConverter, cacheFactory);
        //}

        //public IIndex<PropertyType, EntityType, int> Create<PropertyType, EntityType>
        //    (string fileName,
        //    string indexToken,
        //    IBinConverter<PropertyType> propertyConverter,
        //    IRepositoryCacheFactory cacheFactory,
        //    IIndexFileFactory indexFileFactory)
        //{
        //    return new Index<PropertyType, EntityType>(fileName, indexToken, propertyConverter, cacheFactory);
        //}

        #region Create Existing Primary Index

        //public IPrimaryIndex<PropertyType, EntityType> CreatePrimary<PropertyType, EntityType>
        //    (string fileName)
        //{
        //    return new PrimaryIndex<PropertyType, EntityType>(fileName);
        //}

        //public IPrimaryIndex<PropertyType, EntityType> CreatePrimary<PropertyType, EntityType>
        //    (string fileName,
        //    IQueryableFormatter formatter)
        //{
        //    return new PrimaryIndex<PropertyType, EntityType>(fileName, formatter);
        //}

        //public IPrimaryIndex<PropertyType, EntityType> CreatePrimary<PropertyType, EntityType>
        //    (string fileName,
        //    IQueryableFormatter formatter,
        //    IRepositoryCacheFactory cacheFactory)
        //{
        //    return new PrimaryIndex<PropertyType, EntityType>(fileName, formatter, cacheFactory);
        //}

        //public IPrimaryIndex<PropertyType, EntityType> CreatePrimary<PropertyType, EntityType>
        //    (string fileName,
        //    IQueryableFormatter formatter,
        //    IRepositoryCacheFactory cacheFactory,
        //    IIndexFileFactory indexFileFactory)
        //{
        //    return new PrimaryIndex<PropertyType, EntityType>(fileName, formatter, cacheFactory, indexFileFactory);
        //}

        public IPrimaryIndex<PropertyType, EntityType> CreatePrimary<PropertyType, EntityType>
            (string fileName,
            IQueryableFormatter formatter,
            IRepositoryCacheFactory cacheFactory,
            IIndexFileFactory indexFileFactory,
            IRowSynchronizer<int> rowSynchronizer)
        {
            return new PrimaryIndex<PropertyType, EntityType>(fileName, formatter, cacheFactory, indexFileFactory, rowSynchronizer);
        }

        #endregion

        #region Create New Primary Index

        //public IPrimaryIndex<PropertyType, EntityType> CreatePrimary<PropertyType, EntityType>(string fileName, ISeed<PropertyType> seed)
        //{
        //    return new PrimaryIndex<PropertyType, EntityType>(fileName, seed);
        //}

        //public IPrimaryIndex<PropertyType, EntityType> CreatePrimary<PropertyType, EntityType>(string fileName, ISeed<PropertyType> seed, IBinConverter<PropertyType> propertyConverter)
        //{
        //    return new PrimaryIndex<PropertyType, EntityType>(fileName, seed, propertyConverter);
        //}

        //public IPrimaryIndex<PropertyType, EntityType> CreatePrimary<PropertyType, EntityType>
        //    (string fileName,
        //    ISeed<PropertyType> seed,
        //    IBinConverter<PropertyType> propertyConverter,
        //    IRepositoryCacheFactory cacheFactory)
        //{
        //    return new PrimaryIndex<PropertyType, EntityType>(fileName, seed, propertyConverter, cacheFactory);
        //}

        //public IPrimaryIndex<PropertyType, EntityType> CreatePrimary<PropertyType, EntityType>
        //    (string fileName,
        //    ISeed<PropertyType> seed,
        //    IBinConverter<PropertyType> propertyConverter,
        //    IRepositoryCacheFactory cacheFactory,
        //    IQueryableFormatter formatter)
        //{
        //    return new PrimaryIndex<PropertyType, EntityType>(fileName, seed, propertyConverter, cacheFactory, formatter);
        //}

        //public IPrimaryIndex<PropertyType, EntityType> CreatePrimary<PropertyType, EntityType>
        //    (string fileName,
        //    ISeed<PropertyType> seed,
        //    IBinConverter<PropertyType> propertyConverter,
        //    IRepositoryCacheFactory cacheFactory,
        //    IQueryableFormatter formatter,
        //    IIndexFileFactory indexFileFactory)
        //{
        //    return new PrimaryIndex<PropertyType, EntityType>(fileName, seed, propertyConverter, cacheFactory, formatter, indexFileFactory);
        //}

        public IPrimaryIndex<PropertyType, EntityType> CreatePrimary<PropertyType, EntityType>
            (string fileName,
            ISeed<PropertyType> seed,
            IBinConverter<PropertyType> propertyConverter,
            IRepositoryCacheFactory cacheFactory,
            IQueryableFormatter formatter,
            IIndexFileFactory indexFileFactory,
            IRowSynchronizer<int> rowSynchronizer)
        {
            return new PrimaryIndex<PropertyType, EntityType>(fileName, seed, propertyConverter, cacheFactory, formatter, indexFileFactory, rowSynchronizer);
        }

        #endregion

    }
}
