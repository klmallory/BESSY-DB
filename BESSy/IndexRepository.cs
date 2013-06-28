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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using BESSy.Cache;
using BESSy.Extensions;
using BESSy.Factories;
using BESSy.Files;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;

namespace BESSy
{
    public interface IIndexRepository<IdType, PropertyType> : IIndexedRepository<IndexPropertyPair<IdType, PropertyType>, IdType>, ISweep
    {
        ISeed<IdType> Seed { get; }
        IList<IdType> RidLookup(PropertyType property);
    }

    public class IndexRepository<IdType, PropertyType> : IIndexRepository<IdType, PropertyType>
    {
        static ISafeFormatter DefaultFormatter { get { return new BSONFormatter(); } }
        static IBatchFileManager<IndexPropertyPair<IdType, PropertyType>> DefaultFileManager
        { get { return new BatchFileManager<IndexPropertyPair<IdType, PropertyType>>(DefaultFormatter); } }
        static IRepositoryCacheFactory DefaultCacheFactory { get { return new RepositoryCacheFactory(); } }

        /// <summary>
        /// Opens an existing index with the specified file name.
        /// </summary>
        /// <param name="fileName"></param>
        public IndexRepository(string fileName)
            : this(fileName, DefaultFormatter, DefaultCacheFactory, DefaultFileManager)
        {

        }

        /// <summary>
        /// Opens an existing index with the specified settings.
        /// </summary>
        /// <param name="cacheSize"></param>
        /// <param name="fileName"></param>
        /// <param name="mapFormatter"></param>
        /// <param name="fileManager"></param>
        public IndexRepository
            (string fileName, 
            ISafeFormatter mapFormatter, 
            IRepositoryCacheFactory cacheFactory,
            IBatchFileManager<IndexPropertyPair<IdType, PropertyType>> fileManager)
        {
            _create = false;

            _cacheFactory = cacheFactory;
            _fileName = fileName;
            _mapFormatter = mapFormatter;
            _fileManager = fileManager;
        }

        /// <summary>
        /// Creates or opens an existing index with the specified filename.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="propertyConverter"></param>
        public IndexRepository
            (string fileName
            ,IBinConverter<PropertyType> propertyConverter) 
            : this(fileName
            , TypeFactory.GetSeedFor<IdType>()
            , TypeFactory.GetBinConverterFor<IdType>()
            , propertyConverter
            , DefaultFormatter
            , DefaultCacheFactory
            , DefaultFileManager)
        {
        }

        /// <summary>
        /// Creates or opens an existing index with the specified filename.
        /// </summary>
        /// <param name="cacheSize"></param>
        /// <param name="fileName"></param>
        /// <param name="seed"></param>
        /// <param name="idConverter"></param>
        /// <param name="propertyConverter"></param>
        /// <param name="mapFormatter"></param>
        /// <param name="fileManager"></param>
        public IndexRepository
            (string fileName, 
            ISeed<IdType> seed,
            IBinConverter<IdType> idConverter,
            IBinConverter<PropertyType> propertyConverter,
            ISafeFormatter mapFormatter,
            IRepositoryCacheFactory cacheFactory,
            IBatchFileManager<IndexPropertyPair<IdType, PropertyType>> fileManager)
        {
            _create = true;
            _fileName = fileName;
            _fileManager = fileManager;
            _mapFormatter = mapFormatter;
            _cacheFactory = cacheFactory;
            
            seed.IdConverter = idConverter;
            seed.PropertyConverter = propertyConverter;
            seed.Stride = idConverter.Length + propertyConverter.Length;
            Seed = seed;
        }
        
        List<IDictionary<IdType, PropertyType>> _stagingCache = new List<IDictionary<IdType, PropertyType>>();
        Queue<long> _fileFlushQueue = new Queue<long>();

        bool _inFlush;
        bool _create;
        string _fileName;
        ISafeFormatter _mapFormatter;
        IBatchFileManager<IndexPropertyPair<IdType, PropertyType>> _fileManager;
        IIndexMapManager<IdType, PropertyType> _mapFileManager;
        IBinConverter<IdType> _idConverter;

        IRepositoryCacheFactory _cacheFactory;
        IRepositoryCache<IdType, PropertyType> _cache;

        protected object _syncStaging = new object();
        protected object _syncFileQueue = new object();
        protected object _syncFileFlush = new object();

        protected virtual void HandleOnFlushCompleted(IDictionary<IdType, PropertyType> itemsFlushed)
        {
            Trace.TraceInformation("Flush Completed Handler Called.");

            try
            {
                if (itemsFlushed != null)
                    lock (_syncStaging)
                        itemsFlushed.Clear();

                int fileQueue = 0;
                lock (_syncFileQueue)
                    fileQueue = _fileFlushQueue.Count;

                if (fileQueue > 0 && !_mapFileManager.FlushQueueActive)
                    FlushToFile();
            }
            catch (Exception ex)
            { Trace.TraceError(ex.ToString()); throw; }
        }

        public virtual ISeed<IdType> Seed { get; protected set; }

        public bool FileFlushQueueActive
        {
            get
            {
                if (_inFlush)
                    return true;

                int count = 0;

                lock (_syncFileQueue)
                    count = _fileFlushQueue.Count;

                return count > 0 || _mapFileManager.FlushQueueActive;
            }
        }

        public virtual int Load()
        {
            lock (_syncFileFlush)
            {
                lock (_syncStaging)
                    _stagingCache.Clear();

                var fi = new FileInfo(_fileName);

                if (!fi.Directory.Exists)
                    CreateDirectory(fi);

                if (!fi.Exists)
                {
                    if (_create)
                        using (var c = _fileManager.GetWritableFileStream(_fileName))
                            c.Flush();
                    else
                        throw new FileNotFoundException("File not found.", _fileName);
                }

                using (var stream = _fileManager.GetReadableFileStream(_fileName))
                {
                    var count = _fileManager.GetBatchedSegmentCount(stream);

                    stream.Position = 0;

                    var seed = _fileManager.LoadSeedFrom<IdType>(stream);

                    if (count > 0 && !object.Equals(seed, default(ISeed<IdType>)))
                        InitializeDatabase(seed, count);
                    else
                        InitializeDatabase(Seed, count);

                    var batch = LoadBatchFromFile(stream);

                    var seg = 0;

                    while (seg < count && batch.IsNotNullAndNotEmpty())
                    {
                        seg = _mapFileManager.SaveBatchToFile(batch, seg);

                        batch = LoadBatchFromFile(stream);
                    }
                }
            }

            return Length;
        }

        protected virtual void InitializeDatabase(ISeed<IdType> seed, int count)
        {
            Seed = seed;

            _idConverter = (IBinConverter<IdType>)seed.IdConverter;

            InitializeCache(seed);

            _mapFileManager = new IndexMapManager<IdType, PropertyType>
                (_fileName
                , (IBinConverter<IdType>)seed.IdConverter
                , (IBinConverter<PropertyType>)seed.PropertyConverter);

            _mapFileManager.OpenOrCreate(_fileName, count, seed.Stride);
            _mapFileManager.OnFlushCompleted +=new FlushCompleted<PropertyType,IdType>(HandleOnFlushCompleted);
        }

        private void InitializeCache(ISeed<IdType> seed)
        {
            if (_cache != null)
            {
                _cache.ClearCache();

                _cache.FlushRequested -= new EventHandler(delegate(object sender, EventArgs e)
                { FlushCache(); });

                _cache.Dispose();
            }

            _cache = _cacheFactory.Create<IdType, PropertyType>
                (true
                , _cacheFactory.DefaultCacheSize
                , (IBinConverter<IdType>)seed.IdConverter);

            if (_cache.CacheSize < 1)
                _cache.CacheSize = Caching.DetermineOptimumCacheSize(seed.Stride);

            _cache.FlushRequested += new EventHandler(delegate(object sender, EventArgs e)
            { FlushCache(); });
        }

        protected virtual IList<IndexPropertyPair<IdType, PropertyType>> LoadBatchFromFile(Stream stream)
        {
            return _fileManager.LoadBatchFrom(stream);
        }

        protected virtual void CreateDirectory(FileInfo fi)
        {
            fi.Directory.Create();

            DirectorySecurity dirSec = Directory.GetAccessControl(fi.Directory.FullName);
            dirSec.AddAccessRule(new FileSystemAccessRule("Everyone", FileSystemRights.Write, AccessControlType.Allow));
            dirSec.AddAccessRule(new FileSystemAccessRule("Everyone", FileSystemRights.ReadAndExecute, AccessControlType.Allow));
            dirSec.AddAccessRule(new FileSystemAccessRule("Everyone", FileSystemRights.CreateFiles, AccessControlType.Allow));
            Directory.SetAccessControl(fi.Directory.FullName, dirSec);

            GC.Collect();

            GC.WaitForFullGCComplete();
        }

        protected virtual void FlushCache()
        {
            if (_stagingCache.Count > 0)
                lock (_syncStaging)
                    _stagingCache.RemoveAll(s => s.Count == 0);

            if (_cache.DirtyCount < 1)
                return;

            var sort = new SortedDictionary<IdType, PropertyType>(_cache.UnloadDirtyItems());

            lock (_syncStaging)
                _stagingCache.Add(sort);

            _mapFileManager.Flush(sort);
        }

        public virtual void Flush()
        {
            if (_cache.IsDirty)
            {
                lock (_syncFileQueue)
                    _fileFlushQueue.Enqueue(DateTime.Now.Ticks);

                FlushCache();
            }
            else if (!_mapFileManager.FlushQueueActive)
                FlushToFile();
        }

        protected virtual void FlushToFile()
        {

            if (!Monitor.TryEnter(_syncFileFlush, 500))
                return;

            try
            {
                Trace.TraceInformation("_syncFileFlush entered.");

                var batchSize = _cache.CacheSize;
                _inFlush = true;

                long queue = 0;
                lock (_syncFileQueue)
                    if (_fileFlushQueue.Count > 0)
                        queue = _fileFlushQueue.Dequeue();

                while (queue > 0)
                {
                    var tempFileName = _fileName + Guid.NewGuid().ToString();

                    using (var f = _fileManager.GetWritableFileStream(tempFileName))
                    {
                        long position = _fileManager.SaveSeed<IdType>(f, Seed);

                        List<IndexPropertyPair<IdType, PropertyType>> batch = new List<IndexPropertyPair<IdType, PropertyType>>();

                        foreach (var item in _mapFileManager)
                        {
                            if (object.Equals(default(IndexPropertyPair<IdType, PropertyType>), item))
                                continue;

                            batch.Add(item);

                            if (batch.Count > batchSize)
                            {
                                position = _fileManager.SaveBatch(f, batch, position);

                                batch.Clear();
                            }
                        }

                        if (batch.Count > 0)
                            position = _fileManager.SaveBatch(f, batch, position);

                        f.SetLength(f.Position);

                        f.Flush();
                        f.Close();

                        GC.Collect();

                        GC.WaitForFullGCComplete(5000);

                        FileInfo fi = new FileInfo(tempFileName);

                        fi.Replace(_fileName, _fileName + ".old", true);
                    }

                    lock (_syncFileQueue)
                        if (_fileFlushQueue.Count > 0)
                            queue = _fileFlushQueue.Dequeue();
                        else queue = 0;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                throw;
            }
            finally
            {
                Monitor.Exit(_syncFileFlush);

                Trace.TraceInformation("_syncFileFlush exited.");

                _inFlush = false;
            }
        }

        public virtual IdType Add(IndexPropertyPair<IdType, PropertyType> item)
        {
            if (_idConverter.Compare(item.Id, default(IdType)) == 0)
                return default(IdType);

            _cache.UpdateCache(item.Id, item.Property, true, true);

            return item.Id;
        }

        public void AddOrUpdate(IndexPropertyPair<IdType, PropertyType> item, IdType id)
        {
            if (object.Equals(item, default(IndexPropertyPair<IdType, PropertyType>)))
                throw new ArgumentException("Indexer was null.", "item");

            if (_idConverter.Compare(item.Id, id) != 0)
                Delete(id);

            _cache.UpdateCache(item.Id, item.Property, true, true);
        }

        public void Update(IndexPropertyPair<IdType, PropertyType> item, IdType id)
        {
            if (_idConverter.Compare(id, default(IdType)) == 0)
                id = item.Id;

            if (_idConverter.Compare(item.Id, id) != 0)
                Delete(id);

            _cache.UpdateCache(item.Id, item.Property, true, true);
        }

        public IndexPropertyPair<IdType, PropertyType> Fetch(IdType id)
        {
            IndexPropertyPair<IdType, PropertyType> item; // = default(IndexPropertyPair<IdType, IdType>);

            if (_cache.Contains(id))
                item = new IndexPropertyPair<IdType, PropertyType>(id, _cache.GetFromCache(id));
            else
                item = new IndexPropertyPair<IdType, PropertyType>(id, _mapFileManager.Load(id));

            _cache.UpdateCache(item.Id, item.Property, false, false);

            return item;
        }

        public IList<IdType> RidLookup(PropertyType property)
        {
            var ids = _mapFileManager.RidLookup(property);

            return ids;
        }

        public virtual void Delete(IdType id)
        {
            _mapFileManager.Save(default(PropertyType), id);

            _cache.UpdateCache(id, default(PropertyType), true, true);
        }

        public virtual void Sweep()
        {
            lock (_syncStaging)
                if (_stagingCache.Count > 0)
                    _stagingCache.RemoveAll(s => s == null || s.Count == 0);

            _cache.Sweep();
        }

        public IDictionary<IdType, IndexPropertyPair<IdType, PropertyType>> GetCache()
        {
             return _cache.GetCache().ToDictionary(kv => kv.Key, kv => new IndexPropertyPair<IdType, PropertyType>(kv.Key, kv.Value));
        }

        public int Length
        {
            get
            {
                if (_mapFileManager != null)
                    return _mapFileManager.Length;

                return 0;
            }
        }

        public void Clear()
        {
            if (_stagingCache != null)
                lock (_syncStaging)
                    _stagingCache.Clear();

            if (_mapFileManager != null)
                _mapFileManager.Clear();

            if (_fileFlushQueue != null)
                lock (_syncFileFlush)
                    _fileFlushQueue.Clear();

            _cache.ClearCache();
        }      

        public void Dispose()
        {
            if (_cache.IsDirty)
                Flush();

            while (FileFlushQueueActive)
                Thread.Sleep(100);

            Clear();

            if (_fileManager != null)
                _fileManager.Dispose();

            if (_mapFileManager != null)
            {
                _mapFileManager.OnFlushCompleted -= new FlushCompleted<PropertyType, IdType>(HandleOnFlushCompleted);

                _mapFileManager.Dispose();
            }
        }
    }
}
