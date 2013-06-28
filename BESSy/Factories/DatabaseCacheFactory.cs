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
using BESSy.Seeding;
using BESSy.Serialization.Converters;

namespace BESSy.Factories
{
    public interface IRepositoryCacheFactory
    {
        int DefaultCacheSize { get; set; }

        IRepositoryCache<IdType, EntityType> Create<IdType, EntityType>(bool autoCache, int cacheSize, IBinConverter<IdType> converter);
    }

    public sealed class RepositoryCacheFactory : IRepositoryCacheFactory
    {
        public RepositoryCacheFactory() : this(10240) { }
        public RepositoryCacheFactory(int cacheSize) : this(cacheSize, true) { }
        public RepositoryCacheFactory(int cacheSize, bool autoCache)
        {
            DefaultCacheSize = cacheSize;
            DefaultAutoCache = autoCache;
        }

        public int DefaultCacheSize { get; set; }
        public bool DefaultAutoCache { get; set; }

        public IRepositoryCache<IdType, EntityType> Create<IdType, EntityType>(bool autoCache, int cacheSize, IBinConverter<IdType> converter)
        {
            return new DatabaseCache<IdType, EntityType>(autoCache, cacheSize, converter);
        }
    }
}
