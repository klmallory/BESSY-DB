/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
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
       /// <param name="getUniqueIdMethod"></param>
       /// <param name="setUniqueIdMethod"></param>
        public Repository
            (string fileName,
            string getUniqueIdMethod,
            string setUniqueIdMethod)
            : this(-1, fileName, true, DefaultSeed, DefaultBinConverter, DefaultFileFormatter, DefaultBatchFileManager, getUniqueIdMethod, setUniqueIdMethod)
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
        /// <param name="getUniqueIdMethod"></param>
        /// <param name="setUniqueIdMethod"></param>
        public Repository
            (int cacheSize,
            string fileName,
            bool autoCache,
            ISeed<IdType> seed,
            IBinConverter<IdType> idConverter,
            ISafeFormatter mapFormatter,
            IBatchFileManager<EntityType> fileManager,
            string getUniqueIdMethod,
            string setUniqueIdMethod)

            : base(cacheSize, fileName, seed, idConverter, mapFormatter, fileManager)
        {
            AutoCache = autoCache;

            _seed.GetIdMethod = getUniqueIdMethod;
            _seed.SetIdMethod = setUniqueIdMethod;
        }

        Func<EntityType, IdType> _getUniqueId;
        Action<EntityType, IdType> _setUniqueId;
        private string fileName;
        private ISeed<IdType> seed;
        private IBinConverter<IdType> idConverter;
        private ISafeFormatter mapFormatter;
        private IBatchFileManager<EntityType> fileManager;

        protected override void InitializeDatabase(ISeed<IdType> seed, int count)
        {
            base.InitializeDatabase(seed, count);

            _getUniqueId = (Func<EntityType, IdType>)Delegate.CreateDelegate(typeof(Func<EntityType, IdType>), typeof(EntityType).GetMethod(seed.GetIdMethod));
            _setUniqueId = (Action<EntityType, IdType>)Delegate.CreateDelegate(typeof(Action<EntityType, IdType>), typeof(EntityType).GetMethod(seed.SetIdMethod));
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
