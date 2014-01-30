///*
//Copyright (c) 2011,2012,2013 Kristen Mallory dba Klink

//Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
//to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
//and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
//DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

//*/

//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text;
//using BESSy.Files;
//using BESSy.Seeding;
//using BESSy.Serialization;
//using BESSy.Serialization.Converters;
//using BESSy.Cache;
//using BESSy.Json.Linq;

//namespace BESSy
//{
//    public class ContentRepository<ResourceType> : AbstractMappedRepository<ResourceContainer<ResourceType>, string>
//    {

//        /// <summary>
//        /// Creates or opens an existing repository with the specified settings.
//        /// </summary>
//        /// <param name="cacheSize"></param>
//        /// <param name="fileName"></param>
//        /// <param name="fileManager"></param>
//        public ContentRepository
//            (int cacheSize,
//            string fileName,
//            IContentConverter<ResourceType> converter,
//            ContentFileManager<ResourceType> fileManager)
//            : base(cacheSize, fileName, TypeFactory.GetSeedFor<string>(), TypeFactory.GetBinConverterFor<string>(), new BSONFormatter(), fileManager)
//        {
//            _contentConverter = converter;
//        }

//        /// <summary>
//        /// Opens an existing repository with the specified settings.
//        /// </summary>
//        /// <param name="cacheSize"></param>
//        /// <param name="fileName"></param>
//        /// <param name="mapFormatter"></param>
//        /// <param name="fileManager"></param>
//        public ContentRepository(
//            int cacheSize
//            , string fileName
//            , IQueryableFormatter mapFormatter
//            , IContentConverter<ResourceType> converter
//            , ContentFileManager<ResourceType> fileManager)
//            : base(cacheSize, fileName, mapFormatter, fileManager)
//        {
//            _contentConverter = converter;
//        }

//        /// <summary>
//        /// Creates or opens an existing repository with the specified settings.
//        /// </summary>
//        /// <param name="cacheSize"></param>
//        /// <param name="fileName"></param>
//        /// <param name="segmentSeed"></param>
//        /// <param name="idConverter"></param>
//        /// <param name="mapFormatter"></param>
//        /// <param name="fileManager"></param>
//        protected ContentRepository
//            (int cacheSize,
//            string fileName,
//            ISeed<string> seed,
//            IBinConverter<string> idConverter,
//            IQueryableFormatter mapFormatter,
//            IContentConverter<ResourceType> converter,
//            ContentFileManager<ResourceType> fileManager)
//            : base(cacheSize, fileName, seed, idConverter, mapFormatter, fileManager)
//        {
//            _contentConverter = converter;
//        }

//        protected IContentConverter<ResourceType> _contentConverter;

//        protected sealed override ResourceContainer<ResourceType> LoadFrom(JObject token)
//        {
//            var bytes = token.ToObject<ResourceContainer<byte[]>>(_mapFormatter.Serializer);

//            return new ResourceContainer<ResourceType>()
//            {
//                Name = bytes.Name,
//                Value = _contentConverter.GetResourceFrom(bytes.Value)
//            };
//        }

//        protected override string GetIdFrom(ResourceContainer<ResourceType> item)
//        {
//            return item.Name;
//        }

//        protected override void SetIdFor(ResourceContainer<ResourceType> item, string id)
//        {
//            item.Name = id;
//        }

//        protected override void InitializeDatabase(ISeed<string> seed, int count)
//        {
//            if (CacheSize < 1)
//                CacheSize = Caching.DetermineOptimumCacheSize(seed.Stride);

//            _seed = seed;

//            _idConverter = (IBinConverter<string>)_seed.IdConverter;

//            _mapFileManager = new IndexedContentMapManager<ResourceType, string>(_idConverter, _mapFormatter, _contentConverter);
//            _mapFileManager.OnFlushCompleted += new FlushCompleted<ResourceContainer<ResourceType>, string>(HandleOnFlushCompleted);
//            _mapFileManager.OpenOrCreate(_fileName, count, _seed.Stride);
//        }
//    }
//}
