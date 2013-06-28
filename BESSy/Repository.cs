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
using BESSy.Seeding;
using BESSy.Serialization.Converters;
using BESSy.Files;
using BESSy.Serialization;
using System.Reflection;

namespace BESSy
{
    public class Repository<EntityType, IdType> : AbstractMappedRepository<EntityType, IdType>
    {
        // Default settings.
        static ISafeFormatter DefaultFileFormatter { get { return new BSONFormatter(); } }
        static IBatchFileManager<EntityType> DefaultBatchFileManager { get { return new BatchFileManager<EntityType>(DefaultFileFormatter); } }
        static ISeed<IdType> DefaultSeed { get { return TypeFactory.GetSeedFor<IdType>(); } }
        static IBinConverter<IdType> DefaultBinConverter { get { return TypeFactory.GetBinConverterFor<IdType>(); } }

        /// <summary>
        /// Creates or opens a new repository with the specified settings. Requires the seed to have already been properly configured.
        /// </summary>
        /// <param name="cacheSize"></param>
        /// <param name="fileName"></param>
        /// <param name="autoCache"></param>
        /// <param name="seed"></param>
        /// <param name="idConverter"></param>
        /// <param name="mapFormatter"></param>
        /// <param name="fileManager"></param>
        protected Repository
            (int cacheSize
            , string fileName
            , bool autoCache
            , ISeed<IdType> seed
            , IBinConverter<IdType> idConverter
            , ISafeFormatter mapFormatter
            , IBatchFileManager<EntityType> fileManager)
            : base(cacheSize, fileName, seed, idConverter, mapFormatter, fileManager)
        {

        }

        /// <summary>
        /// Opens an existing repository with the specfied fileName using the default settings.
        /// </summary>
        /// <param name="fileName"></param>
        public Repository(string fileName)
            : base(-1, fileName, DefaultFileFormatter, DefaultBatchFileManager)
        {
        }

       /// <summary>
        /// Creates, or opens an existing repository with the default settings.
       /// </summary>
       /// <param name="fileName"></param>
       /// <param name="idPropertyName"></param>
       /// <param name="setUniqueIdMethod"></param>
        public Repository
            (string fileName,
            string idPropertyName)
            : this(-1, fileName, true, DefaultSeed, DefaultBinConverter, DefaultFileFormatter, DefaultBatchFileManager, idPropertyName)
        {
        }

        /// <summary>
        /// Creates or opens an existing repository with the specified settings.
        /// </summary>
        /// <param name="cacheSize"></param>
        /// <param name="fileName"></param>
        /// <param name="autoCache"></param>
        /// <param name="seed"></param>
        /// <param name="idConverter"></param>
        /// <param name="mapFormatter"></param>
        /// <param name="fileManager"></param>
        /// <param name="idPropertyName"></param>
        /// <param name="setUniqueIdMethod"></param>
        public Repository
            (int cacheSize,
            string fileName,
            bool autoCache,
            ISeed<IdType> seed,
            IBinConverter<IdType> idConverter,
            ISafeFormatter mapFormatter,
            IBatchFileManager<EntityType> fileManager,
            string idPropertyName)

            : base(cacheSize, fileName, seed, idConverter, mapFormatter, fileManager)
        {
            AutoCache = autoCache;

            _seed.IdProperty = idPropertyName;
        }

        Func<EntityType, IdType> _getUniqueId;
        Action<EntityType, IdType> _setUniqueId;

        protected override void InitializeDatabase(ISeed<IdType> seed, int count)
        {
            base.InitializeDatabase(seed, count);

            _getUniqueId = (Func<EntityType, IdType>)Delegate.CreateDelegate(typeof(Func<EntityType, IdType>), typeof(EntityType).GetProperty(seed.IdProperty).GetGetMethod());
            _setUniqueId = (Action<EntityType, IdType>)Delegate.CreateDelegate(typeof(Action<EntityType, IdType>), typeof(EntityType).GetProperty(seed.IdProperty).GetSetMethod());
        }

        protected override IdType GetIdFrom(EntityType item)
        {
            return _getUniqueId(item);
        }

        protected override void SetIdFor(EntityType item, IdType id)
        {
            _setUniqueId(item, id);
        }
    }
}
