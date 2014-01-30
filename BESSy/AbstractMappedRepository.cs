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
using System.Text;
using System.Threading;
using System.Security.AccessControl;
using BESSy.Seeding;
using BESSy.Files;
using BESSy.Extensions;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using BESSy.Serialization;
using BESSy.Cache;
using BESSy.Queries;
using BESSy.Json.Linq;

namespace BESSy
{
    public interface IIndexedRepository<T, I> : IRepository<T, I>, IIndexedReadOnlyRepository<T, I>
    {

    }

    public interface IIndexedReadOnlyRepository<T, I> : IReadOnlyRepository<T, I>, IFlush, ILoad
    {
        IDictionary<I, T> GetCache();

        IList<T> FetchFromIndex<IndexType>(string name, IndexType indexProperty);
        IList<T> FetchRangeFromIndexInclusive<IndexType>(string name, IndexType startProperty, IndexType endProperty);
    }

    public abstract class AbstractMappedRepository<EntityType, IdType> : IIndexedRepository<EntityType, IdType>, IQueryableRepository<EntityType>, ICache<EntityType, IdType>
    {
        /// <summary>
        /// Opens an existing repository with the specified settings.
        /// </summary>
        /// <param name="cacheSize"></param>
        /// <param name="fileName"></param>
        /// <param name="mapFormatter"></param>
        /// <param name="fileManager"></param>
        public AbstractMappedRepository(
            int cacheSize
            , string fileName
            , IQueryableFormatter mapFormatter
            , IBatchFileManager<EntityType> fileManager)
        {
            _create = false;
            CacheSize = cacheSize;
            AutoCache = true;

            _fileName = fileName;

            _mapFormatter = mapFormatter;
            _fileManager = fileManager;
        }

        /// <summary>
        /// Creates or opens an existing repository with the specified settings.
        /// </summary>
        /// <param name="cacheSize"></param>
        /// <param name="fileName"></param>
        /// <param name="segmentSeed"></param>
        /// <param name="idConverter"></param>
        /// <param name="mapFormatter"></param>
        /// <param name="fileManager"></param>
        protected AbstractMappedRepository
            (int cacheSize,
            string fileName,
            ISeed<IdType> seed,
            IBinConverter<IdType> idConverter,
            IQueryableFormatter mapFormatter,
            IBatchFileManager<EntityType> fileManager)
            : this(cacheSize, fileName, mapFormatter, fileManager)
        {
            _create = true;

            if (seed == null)
                throw new ArgumentNullException("segmentSeed is a required parameter for a new repository.");

            _seed = seed;
            _seed.IdConverter = idConverter;
        }


        List<IdType> _cacheQueue = new List<IdType>();
        IDictionary<IdType, EntityType> _cache = new Dictionary<IdType, EntityType>();
        List<IDictionary<IdType, EntityType>> _stagingCache = new List<IDictionary<IdType, EntityType>>();
        Queue<long> _fileFlushQueue = new Queue<long>();

        bool _inFlush;
        bool _create;

        protected string _fileName;
        protected string _idToken;
        protected IQueryableFormatter _mapFormatter;
        protected IBatchFileManager<EntityType> _fileManager;
        protected IIndexedEntityMapManager<EntityType, IdType> _mapFileManager;

        protected IBinConverter<IdType> _idConverter;
        protected ISeed<IdType> _seed;
        protected bool _cacheIsDirty { get; set; }
        protected object _syncCache = new object();
        protected object _syncStaging = new object();
        protected object _syncFileQueue = new object();
        protected object _syncFileFlush = new object();

        
        protected abstract IdType GetIdFrom(EntityType item);
        protected abstract void SetIdFor(EntityType item, IdType id);

        protected virtual void HandleOnFlushCompleted(IDictionary<IdType, EntityType> itemsFlushed)
        {
            Trace.TraceInformation("Flush Completed Handler Called.");

            try
            {
                if (itemsFlushed == null || itemsFlushed.Count <= 0)
                    return;

                if (_mapFileManager.Stride > _seed.Stride)
                    _seed.Stride = _mapFileManager.Stride;

                lock (_syncStaging)
                    if (itemsFlushed != null)
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

        protected virtual void UpdateCache(IdType id, EntityType item, bool dirtyOperation, bool forceCache)
        {
            IdType newId = id;

            if (item != null)
                newId = GetIdFrom(item);
                
            lock (_syncCache)
            {
                _cacheIsDirty |= dirtyOperation;

                if (AutoCache)
                    if (_cache.Count > CacheSize)
                        Sweep();

                if (_cache.ContainsKey(id))
                    if (_idConverter.Compare(newId, id) != 0)
                    {
                        _cache.Remove(id);
                        _cache.Add(newId, item);
                    }
                    else
                        _cache[newId] = item;
                else if (_cacheQueue.Contains(newId))
                    _cache.Add(newId, item);
                else if (forceCache || AutoCache)
                {
                    _cacheQueue.Add(newId);
                    _cache.Add(newId, item);
                }
            }
        }

        public virtual int CacheSize { get; set; }
        public virtual bool AutoCache { get; set; }

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
                lock (_syncCache)
                {
                    _cache.Clear();

                    _cacheQueue.Clear();

                    _cacheIsDirty = false;

                    lock (_syncStaging)
                        _stagingCache.Clear();

                    var fi = new FileInfo(_fileName);

                    if (!fi.Exists)
                    {
                        if (_create)
                        {
                            if (!fi.Directory.Exists)
                                CreateDirectory(fi);

                            using (var c = _fileManager.GetWritableFileStream(_fileName))
                                c.Flush();
                        }
                        else
                            throw new FileNotFoundException("File not found.", _fileName);
                    }

                    using (var stream = _fileManager.GetReadableFileStream(_fileName))
                    {
                        var count = _fileManager.GetBatchedSegmentCount(stream);

                        if (count > stream.Length)
                            throw new FileLoadException(string.Format("Database file {0} is corrupt, please replace with a backup.", _fileName), _fileName);

                        stream.Position = 0;

                        var seed = LoadSeed(stream);

                        if (count > 0 && !object.Equals(seed, default(ISeed<IdType>)))
                            InitializeDatabase(seed, count);
                        else
                            InitializeDatabase(_seed, count);

                        var batch = LoadBatchFromFile(stream)
                            .Where(entity => !object.Equals(entity, default(EntityType)))
                            .ToDictionary(i => GetIdFrom(i));

                        var seg = 0;

                        while (seg < count && batch.Any())
                        {
                            seg = _mapFileManager.SaveBatchToFile(batch, seg);

                            batch = LoadBatchFromFile(stream)
                                .Where(entity => !object.Equals(entity, default(EntityType)))
                                .ToDictionary(entity => GetIdFrom(entity));
                        }
                    }
                }
            }

            return Length;
        }

        protected virtual void InitializeDatabase(ISeed<IdType> seed, int count)
        {
            if (CacheSize < 1)
                CacheSize = Caching.DetermineOptimumCacheSize(seed.Stride);

            _seed = seed;

            _idConverter = (IBinConverter<IdType>)_seed.IdConverter;

            _mapFileManager = new IndexedEntityMapManager<EntityType, IdType>(_idConverter, _mapFormatter);
            _mapFileManager.OnFlushCompleted += new FlushCompleted<EntityType, IdType>(HandleOnFlushCompleted);
            _mapFileManager.OpenOrCreate(_fileName, count, _seed.Stride);
        }

        protected virtual IList<EntityType> LoadBatchFromFile(Stream stream)
        {
            return _fileManager.LoadBatchFrom(stream);
        }

        protected virtual ISeed<IdType> LoadSeed(Stream stream)
        {
            return _fileManager.LoadSeedFrom<IdType>(stream);
        }

        protected virtual long SaveSeed(Stream f)
        {
            _seed.Stride = _mapFileManager.Stride;
            return _fileManager.SaveSeed<IdType>(f, _seed);
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
            lock (_syncCache)
            {
                lock (_syncStaging)
                    if (_stagingCache.Count > 0)
                        _stagingCache.RemoveAll(s => s.Count == 0);

                var sort = new Dictionary<IdType, EntityType>(_cache);

                lock (_syncStaging)
                {
                    _stagingCache.Add(sort);

                    _mapFileManager.Flush(sort);
                }

                ClearCache();

                _cacheIsDirty = false;
            }
        }

        public virtual void Flush()
        {
            if (_cacheIsDirty)
            {
                lock (_syncFileQueue)
                    _fileFlushQueue.Enqueue(DateTime.Now.Ticks);

                FlushCache();
            }
            else if (!FileFlushQueueActive)
                FlushToFile();
        }

        protected virtual void FlushToFile()
        {
            if (!Monitor.TryEnter(_syncFileFlush, 500))
                return;

            try
            {
                Trace.TraceInformation("_syncFileFlush entered.");

                _inFlush = true;

                int queueCount = 0;
                lock (_syncFileQueue)
                    queueCount = _fileFlushQueue.Count;

                while (queueCount > 0)
                {
                    lock (_syncFileQueue)
                        if (queueCount > 0)
                            _fileFlushQueue.Dequeue();
                        else
                            break;

                    var fi = new FileInfo(_fileName);

                    var tempFileName = Path.Combine(fi.DirectoryName, Guid.NewGuid().ToString() + ".tmp");

                    using (var f = _fileManager.GetWritableFileStream(tempFileName))
                    {
                        long position = SaveSeed(f);

                        List<EntityType> batch = new List<EntityType>();

                        foreach (var item in _mapFileManager.AsEnumerable())
                        {
                            if (item == null)
                                continue;

                            foreach(var e in item)
                                batch.Add(LoadFrom(e));

                            if (batch.Count > _fileManager.BatchSize)
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

                        _fileManager.Replace(tempFileName, _fileName);
                    }

                    lock (_syncFileQueue)
                        queueCount = _fileFlushQueue.Count;
                }
            }
            catch (Exception ex) { Trace.TraceError(ex.ToString()); throw; }
            finally
            {
                Monitor.Exit(_syncFileFlush);

                Trace.TraceInformation("_syncFileFlush exited.");

                _inFlush = false;

                GC.Collect();
            }
        }

        protected abstract EntityType LoadFrom(JObject token);

        public virtual IdType Add(EntityType item)
        {
            var id = GetNextIdFor(item);

            if (Contains(id))
                throw new DuplicateKeyException(id, item);

            SetIdFor(item, id);

            UpdateCache(id, item, true, true);

            return id;
        }

        protected virtual IdType GetNextIdFor(EntityType item)
        {
            //how much segmentSeed could a woodchuck chuck, if a woodchuck could chuck segmentSeed?
            //This segmentSeed is not a passthrough (auto)
            if (_idConverter.Compare(_seed.Peek(), _seed.LastSeed) != 0)
                return _seed.Increment();

            //This segmentSeed is a passthrough (manual)
            return GetIdFrom(item);
        }

        public IdType AddOrUpdate(EntityType item, IdType id = default(IdType))
        {
            var newId = GetIdFrom(item);

            if (_idConverter.Compare(newId, default(IdType)) == 0)
            {
                newId = GetNextIdFor(item);
                SetIdFor(item, newId);
            }

            if (_idConverter.Compare(newId, id) != 0)
                Delete(id);

            UpdateCache(newId, item, true, true);

            return newId;
        }

        public void Update(EntityType item, IdType id = default(IdType))
        {
            var newId = GetIdFrom(item);

            if (_idConverter.Compare(newId, default(IdType)) == 0)
                throw new KeyNotFoundException("Empty Id field not valid on Update.");

            if (_idConverter.Compare(id, default(IdType)) == 0)
                id = newId;

            if (_idConverter.Compare(newId, id) != 0)
                Delete(id);

            _cacheQueue.Add(newId);
            UpdateCache(newId, item, true, true);
        }

        public EntityType Fetch(IdType id)
        {
            EntityType item = default(EntityType);

            lock (_syncCache)
                lock (_syncStaging)
                    if (_cache.ContainsKey(id))
                        return _cache[id];
                    else if (_stagingCache.Any(c => c.ContainsKey(id)))
                        return _stagingCache.Last(s => s.ContainsKey(id))[id];

            item = _mapFileManager.Load(id);

            if (!object.Equals(item, default(EntityType)))
                UpdateCache(id, item, false, false);

            return item;
        }

        public virtual void Delete(IdType id)
        {
            lock (_syncStaging)
            {
                _mapFileManager.Save(default(EntityType), id);
                _mapFileManager.Detach(id);

                UpdateCache(id, default(EntityType), true, true);

                _seed.Open(id);
            }
        }

        public virtual void Sweep()
        {
            lock (_syncStaging)
                if (_stagingCache.Count > 0)
                    _stagingCache.RemoveAll(s => s == null || s.Count == 0);

            lock (_syncCache)
            {
                if (_cache.Count <= CacheSize)
                    return;

                if (_cacheIsDirty)
                {
                    FlushCache();
                    return;
                }

                //clear enough cache for smooth operations.
                var toRemove = _cacheQueue.Distinct().Take(CacheSize / 2).ToList();

                foreach (var id in toRemove)
                {
                    _cacheQueue.RemoveAll(c => _idConverter.Compare(c, id) == 0);
                    _cache.Remove(id);
                }
            }

            _mapFileManager.Sweep();
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
                _mapFileManager.ClearCache();

            if (_fileFlushQueue != null)
                lock (_syncFileFlush)
                    _fileFlushQueue.Clear();

            ClearCache();
        }

        public bool IsNew(IdType id)
        {
            return false;
        }

        public bool Contains(IdType id)
        {
            lock (_syncCache)
                lock (_syncStaging)
                    return _cache.ContainsKey(id)
                        || _stagingCache.Any(c => c.ContainsKey(id));
        }

        public EntityType GetFromCache(IdType id)
        {
            lock (_syncCache)
            {
                lock (_syncStaging)
                {
                    if (_cache.ContainsKey(id))
                        return _cache[id];
                    else if (_stagingCache.Any(c => c.ContainsKey(id)))
                        return _stagingCache.Last(s => s.ContainsKey(id))[id];
                }
            }
            return default(EntityType);
        }

        public IDictionary<IdType, EntityType> GetCache()
        {
            return _cache;
        }

        public void CacheItem(IdType id)
        {
            lock (_syncCache)
                _cacheQueue.Add(id);
        }



        public IList<EntityType> FetchFromIndex<IndexType>(string name, IndexType indexProperty)
        {
            throw new NotImplementedException();
        }

        public IList<EntityType> FetchRangeFromIndexInclusive<IndexType>(string name, IndexType startProperty, IndexType endProperty)
        {
            throw new NotImplementedException();
        }

        #region IQueryable Members

        public int Delete(Func<JObject, bool> selector)
        {
            int count = 0;

            try
            {
                foreach (var page in _mapFileManager.AsEnumerable())
                {
                    foreach (var e in page.Where(p => selector(p)))
                    {
                        var id = e.Value<IdType>(_idToken);

                        if (_idConverter.Compare(id, default(IdType)) == 0)
                            continue;

                        count++;

                        _mapFileManager.Save(default(EntityType), id);
                        _mapFileManager.Detach(id);

                        UpdateCache(id, default(EntityType), true, true);

                        _seed.Open(id);
                    }
                }
            }
            catch (Exception ex) { throw new QueryExecuteException("Delete Query Execution Failed.", ex); }

            return count;
        }

        public int Update<UpdateEntityType>(Func<JObject, bool> selector, params Action<UpdateEntityType>[] updates) where UpdateEntityType : EntityType
        {
            try
            {
                var count = 0;

                var entities = new Dictionary<IdType, UpdateEntityType>();

                var ids = new SortedSet<IdType>(_idConverter);

                lock (_syncCache)
                {
                    if (_cache.Count > 0)
                    {
                        var items = _cache.Values
                                .Where(s => s != null)
                                .Where(o => selector(JObject.FromObject(o, _mapFormatter.Serializer)))
                                .OfType<UpdateEntityType>().ToList();

                        foreach (var c in items)
                        {
                            var id = GetIdFrom(c);

                            if (ids.Contains(id))
                                continue;

                            foreach (var action in updates)
                                action.Invoke(c);

                            ids.Add(id);
                            entities.Add(id, c);

                            UpdateCache(id, c, true, true);

                            count++;
                        }
                    }
                }

                foreach (var page in this._mapFileManager.AsEnumerable())
                {
                    foreach (var obj in page.Where(o => selector(o)))
                    {
                        //TODO: what if the object is null or not of the right type?
                        var entity = (UpdateEntityType)LoadFrom(obj);

                        if (entity == null)
                            continue;

                        var id = GetIdFrom(entity);

                        if (ids.Contains(id))
                            continue;

                        foreach (var action in updates)
                            action.Invoke(entity);

                        _mapFileManager.Save(entity, id);
                        UpdateCache(id, entity, true, true);

                        count++;
                    }
                }

                return count;
            }
            catch (Exception ex) { throw new QueryExecuteException("Update Query Execution Failed.", ex); }
        }

        public IList<EntityType> Select(Func<JObject, bool> selector)
        {
            try
            {
                var list = new List<EntityType>();

                lock (_syncStaging)
                    if (_stagingCache.Count > 0)
                        list.AddRange(_stagingCache
                            .SelectMany(s => s.Values)
                                .Where(s => s != null)
                                .Where(o => selector(JObject.FromObject(o, _mapFormatter.Serializer))));

                lock (_syncCache)
                    if (_cache.Count > 0)
                        list.AddRange(_cache.Values
                                .Where(s => s != null)
                                .Where(o => selector(JObject.FromObject(o, _mapFormatter.Serializer))));

                foreach (var page in _mapFileManager.AsEnumerable())
                    foreach (var obj in page.Where(o => selector(o)))
                        list.Add(LoadFrom(obj));

                return list.GroupBy(k => GetIdFrom(k)).Select(f => f.First()).ToList();
            }
            catch (Exception ex) { throw new QueryExecuteException("Select Query Execution Failed.", ex); }
        }

        public IList<EntityType> SelectFirst(Func<JObject, bool> selector, int max)
        {
            try
            {
                var list = new List<EntityType>();

                lock (_syncStaging)
                    if (list.Count < max && _stagingCache.Count > 0)
                        list.AddRange(_stagingCache
                            .SelectMany(s => s.Values)
                                .Where(s => s != null)
                                .Where(o => selector(JObject.FromObject(o, _mapFormatter.Serializer))));

                lock (_syncCache)
                    if (_cache.Count > 0)
                        list.AddRange(_cache.Values
                                .Where(s => s != null)
                                .Where(o => selector(JObject.FromObject(o, _mapFormatter.Serializer))));

                if (list.Count < max)
                {
                    foreach (var page in _mapFileManager.AsEnumerable())
                    {
                        list.AddRange(page.Where(o => selector(o)).Select(e => e.ToObject<EntityType>(_mapFormatter.Serializer)));

                        if (list.Count >= max)
                            break;
                    }
                }

                return list.GroupBy(k => GetIdFrom(k)).Select(f => f.First()).Take(Math.Min(list.Count, max)).ToList();
            }
            catch (Exception ex) { throw new QueryExecuteException("SelectFirst Query Execution Failed.", ex); }
        }

        public IList<EntityType> SelectLast(Func<JObject, bool> selector, int max)
        {
            try
            {
                var list = new List<EntityType>();

                lock (_syncStaging)
                    if (list.Count < max && _stagingCache.Count > 0)
                        list.AddRange(_stagingCache.AsEnumerable().Reverse()
                            .SelectMany(s => s.Values.Reverse())
                                .Where(s => s != null)
                                .Where(o => selector(JObject.FromObject(o, _mapFormatter.Serializer))));

                lock (_syncCache)
                    if (_cache.Count > 0)
                        list.AddRange(_cache.Values.Reverse()
                                .Where(s => s != null)
                                .Where(o => selector(JObject.FromObject(o, _mapFormatter.Serializer))));

                if (list.Count < max)
                {
                    foreach (var page in _mapFileManager.AsReverseEnumerable())
                    {
                        list.AddRange(page.Where(o => selector(o)).Select(e => e.ToObject<EntityType>(_mapFormatter.Serializer)));

                        if (list.Count >= max)
                            break;
                    }
                }

                return list.GroupBy(k => GetIdFrom(k)).Select(f => f.First()).Take(Math.Min(list.Count, max)).ToList();
            }
            catch (Exception ex) { throw new QueryExecuteException("SelectLast Query Execution Failed.", ex); }
        }

        #endregion IQueryable Members

        public void Detach(IdType id)
        {
            lock (_syncCache)
            {
                if (_cacheQueue.Contains(id))
                    _cacheQueue.Remove(id);

                if (_cache.ContainsKey(id))
                    _cache.Remove(id);
            }
        }

        public void ClearCache()
        {
            lock (_syncCache)
            {
                if (_cacheQueue != null)
                    _cacheQueue.Clear();

                if (_cache != null)
                    _cache.Clear();

                _cacheIsDirty = false;
            }
        }

        public void Dispose()
        {
            if (_cacheIsDirty)
                Flush();

            if (_mapFileManager != null)
                while (FileFlushQueueActive)
                    Thread.Sleep(100);

            Clear();

            if (_fileManager != null)
                _fileManager.Dispose();

            if (_mapFileManager != null)
            {
                _mapFileManager.OnFlushCompleted -= new FlushCompleted<EntityType, IdType>(HandleOnFlushCompleted);

                _mapFileManager.Dispose();
            }
        }
    }
}
