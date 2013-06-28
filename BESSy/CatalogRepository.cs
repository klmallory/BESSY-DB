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
using BESSy.Serialization.Converters;
using BESSy.Seeding;
using BESSy.Files;
using BESSy.Serialization;

namespace BESSy
{
    internal class CatalogRepository<EntityType, IdType> : Repository<EntityType, IdType>
    {
        public CatalogRepository
            (int cacheSize,
            string fileName,
            bool autoCache,
            ISeed<IdType> seed,
            IBinConverter<IdType> idConverter,
            ISafeFormatter mapFormatter,
            IBatchFileManager<EntityType> fileManager)

            : base(cacheSize
            , fileName
            , autoCache
            , seed
            , idConverter
            , mapFormatter
            , fileManager)
        {

        }

        protected override Seeding.ISeed<IdType> LoadSeed(System.IO.Stream stream)
        {
            return default(ISeed<IdType>);
        }

        public override int Load()
        {
            return base.Load();
        }

        protected override void InitializeDatabase(ISeed<IdType> seed, int count)
        {
            base.InitializeDatabase(seed, count);
        }

        //In catalogs, the seed is not managed by the individual repositories.
        //TODO: find a better way to do this than parasitical inheritance.
        protected override long SaveSeed(System.IO.Stream f)
        {
            if (_mapFileManager.Stride > _seed.Stride)
                _seed.Stride = _mapFileManager.Stride;

            return 0;
        }
    }
}
