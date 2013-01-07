/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
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

namespace BESSy
{
    public interface IMappedRepository<T, I> : IRepository<T, I>, ILinqRepository<T, I>, ICache<T, I>
    {
        int Load();
        int CacheSize { get; set; }
        bool AutoCache { get; set; }
        bool FileFlushQueueActive { get; }
        void Flush();
    }


    public abstract class AbstractMappedRepository<EntityType, IdType> : IMappedRepository<EntityType, IdType> 
    {
        protected AbstractMappedRepository(string fileName) 
        {
            _fileName = fileName;
        }

        //TODO: Row Level locking, like a dictionary of RowLock objects, with start and end segment property.
        //TODO: Json .NET like query language support.

        public AbstractMappedRepository
            (bool createIfNotExistant,
            int cacheSize,
            string fileName,
            ISeed<IdType> seed,
            IBinConverter<IdType> idConverter,
            IBatchFileManager<EntityType> fileManager,
            IIndexedEntityMapManager<EntityType, IdType> mapFileManager) : this(fileName)
        {
            if (seed == null)
                throw new ArgumentNullException("seed is a required paramter.");

            _idConverter = idConverter;
            _create = createIfNotExistant;
            _fileManager = fileManager;
            _mapFileManager = mapFileManager;

            _seed = seed;

            AutoCache = true;

            if (cacheSize < 0)
                DetermineOptimumCacheSize();
            else
                CacheSize = cacheSize;

            _mapFileManager.OnFlushCompleted += new FlushCompleted<EntityType, IdType>(HandleOnFlushCompleted);
        }

        
        List<IdType> _cacheQueue = new List<IdType>();
        IDictionary<IdType, EntityType> _cache = new Dictionary<IdType, EntityType>();
        List<IDictionary<IdType, EntityType>> _stagingCache = new List<IDictionary<IdType, EntityType>>();
        Queue<long> _fileFlushQueue = new Queue<long>();

        bool _inFlush;
        string _fileName;

        protected bool _create;
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

        protected virtual void DetermineOptimumCacheSize()
        {
            if (Environment.Is64BitProcess)
                CacheSize = 512000000 / (((int)(Math.Ceiling(_seed.Stride / (double)Environment.SystemPageSize))) * Environment.SystemPageSize).Clamp(1, int.MaxValue);
            else
                CacheSize = 256000000 / (((int)(Math.Ceiling(_seed.Stride / (double)Environment.SystemPageSize))) * Environment.SystemPageSize).Clamp(1, int.MaxValue);
        }

        protected virtual void HandleOnFlushCompleted(IDictionary<IdType, EntityType> itemsFlushed)
        {
            Trace.TraceInformation("Flush Completed Handler Called.");

            try
            {
                if (_mapFileManager.Stride > _seed.Stride)
                    _seed.Stride = _mapFileManager.Stride;

                DetermineOptimumCacheSize();

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
            lock (_syncCache)
            {
                _cacheIsDirty |= dirtyOperation;

                if (_cache.ContainsKey(id))
                    _cache[id] = item;
                else if (_cacheQueue.Contains(id))
                    _cache.Add(id, item);
                else if (AutoCache || forceCache)
                {
                    _cacheQueue.Add(id);
                    _cache.Add(id, item);
                }
            }

            if (AutoCache)
                if (_cache.Count > CacheSize)
                    Sweep();
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

                        if (count > stream.Length)
                            throw new FileLoadException(string.Format("Database file {0} is corrupt, please replace with a backup.", _fileName), _fileName);

                        stream.Position = 0;

                        var seed = LoadSeed(stream);

                        if (count > 0 && !object.Equals(seed, default(ISeed<IdType>)))
                            _seed = seed;

                        _mapFileManager.OpenOrCreate(_fileName, count, _seed.Stride);

                        var batch = LoadBatchFromFile(stream).ToDictionary(i => GetIdFrom(i));

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

            return Count();
        }

        protected virtual IList<EntityType> LoadBatchFromFile(FileStream stream)
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

                IDictionary<IdType, EntityType> sort = new Dictionary<IdType, EntityType>(_cache);

                lock (_syncStaging)
                    _stagingCache.Add(sort);

                _mapFileManager.Flush(sort);

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

                        foreach (var item in _mapFileManager)
                        {
                            batch.Add(item);

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

                        GC.Collect();

                        GC.WaitForFullGCComplete(5000);

                        fi = new FileInfo(tempFileName);

                        fi.Replace(_fileName, _fileName + ".old", true);
                    }

                    lock (_syncFileQueue)
                        queueCount = _fileFlushQueue.Count;
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
            //how much seed could a woodchuck chuck, if a woodchuck could chuck seed?
            //This seed is not a passthrough (auto)
            if (_idConverter.Compare(_seed.Peek(), _seed.LastSeed) != 0)
                return _seed.Increment();

            //This seed is a passthrough (manual)
            return GetIdFrom(item);
        }

        public void AddOrUpdate(EntityType item, IdType id = default(IdType))
        {
            var newId = GetIdFrom(item);

            if (_idConverter.Compare(newId, default(IdType)) == 0 || !Contains(newId))
            {
                newId = GetNextIdFor(item);
                SetIdFor(item, newId);
            }

            if (_idConverter.Compare(newId, id) != 0)
                Delete(id);

            UpdateCache(newId, item, true, true);
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

        #region ILinqRepository<EntityType,IdType> Members

        public IEnumerable<EntityType> Take(int count)
        {
            lock (_syncFileFlush)
                lock (_syncCache)
                    lock (_syncStaging)
                        return _mapFileManager
                            .Union(_cache.Values)
                            .Union(_stagingCache.SelectMany(s => s.Values))
                            .Take(count);
        }

        public IEnumerable<EntityType> Skip(int count)
        {
            lock (_syncFileFlush)
                lock (_syncCache)
                    lock (_syncStaging)
                        return _mapFileManager
                            .Union(_cache.Values)
                            .Union(_stagingCache.SelectMany(s => s.Values))
                            .Skip(count);
        }

        public IEnumerable<EntityType> Where(Func<EntityType, bool> query)
        {
            lock (_syncFileFlush)
                lock (_syncCache)
                    lock (_syncStaging)
                        return Enumerable.Where(_mapFileManager, query)
                            .Union(_cache.Values.Where(query))
                            .Union(_stagingCache.SelectMany(s => s.Values.Where(query)));
        }

        public EntityType First(Func<EntityType, bool> query)
        {
            EntityType first;

            lock (_syncCache)
                first = _cache.Values.FirstOrDefault(query);

            if (!object.Equals(first, default(EntityType)))
                return first;

            lock (_syncStaging)
            {
                var select = _stagingCache.SelectMany(s => s.Values.Where(query)).ToList();

                if (select.Count() > 0)
                   return select.First(query);
            }

            lock (_syncFileFlush)
                return Enumerable.First(_mapFileManager, query);
        }

        public EntityType FirstOrDefault(Func<EntityType, bool> query)
        {
            EntityType first;

            lock (_syncCache)
                first = _cache.Values.FirstOrDefault(query);

            if (!object.Equals(first, default(EntityType)))
                return first;

            lock (_syncStaging)
            {
                var select = _stagingCache.SelectMany(s => s.Values.Where(query)).ToList();

                if (select.Count() > 0)
                    return select.First(query);
            }
            
            lock (_syncFileFlush)
                return Enumerable.FirstOrDefault(_mapFileManager, query);
        }

        public EntityType Last(Func<EntityType, bool> query)
        {
            EntityType last;

            lock (_syncCache)
                last = _cache.Values.LastOrDefault(query);

            if (!object.Equals(last, default(EntityType)))
                return last;

            lock (_syncStaging)
            {
                var select = _stagingCache.SelectMany(s => s.Values.Where(query));

                last = select.LastOrDefault(query);
            }

            if (!object.Equals(last, default(EntityType)))
                return last;

            lock (_syncFileFlush)
                return Enumerable.Last(_mapFileManager, query);
        }

        public EntityType LastOrDefault(Func<EntityType, bool> query)
        {
            EntityType last;

            lock (_syncCache)
                last = _cache.Values.LastOrDefault(query);

            if (!object.Equals(last, default(EntityType)))
                return last;

            lock (_syncStaging)
            {
                var select = _stagingCache.SelectMany(s => s.Values.Where(query));

                last = select.LastOrDefault(query);
            }

            if (!object.Equals(last, default(EntityType)))
                return last;

            lock (_syncFileFlush)
                return Enumerable.LastOrDefault(_mapFileManager, query);
        }

        public IEnumerable<TResult> OfType<TResult>()
        {
            lock (_syncFileFlush)
                lock (_syncCache)
                    lock (_syncStaging)
                        return Enumerable.OfType<TResult>(_mapFileManager)
                            .Concat(_cache.Values.OfType<TResult>())
                            .Concat(_stagingCache.SelectMany(s => s.Values.OfType<TResult>()));
        }

        public IQueryable<EntityType> AsQueryable()
        {
            lock (_syncFileFlush)
                lock (_syncCache)
                    lock (_syncStaging)
                        return _mapFileManager.AsQueryable<EntityType>()
                            .Concat(_cache.Values.AsQueryable<EntityType>())
                            .Concat(_stagingCache.SelectMany(s => s.Values.AsQueryable<EntityType>()));
        }

        #endregion

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

                var diff = (_cache.Count - CacheSize);

                if (diff <= 0)
                    return;

                var r = _cache.Keys.ToList().GetRange(0, diff).ToList();

                r.ForEach(a =>
                {
                    _cacheQueue.RemoveAll(c => _idConverter.Compare(c, a) == 0);
                    _cache.Remove(a);
                });
            }

            _mapFileManager.Sweep();
        }

        public int Count()
        {
            if (_mapFileManager != null)
                return _mapFileManager.Length;

            return 0;
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

        public void CacheItem(IdType id)
        {
            lock (_syncCache)
                _cacheQueue.Add(id);
        }

        public void Detach(IdType id)
        {
            lock (_syncCache)
            {
                if (_cacheQueue.Contains(id))
                    _cacheQueue.Remove(id);

                if (!_cacheQueue.Contains(id) && _cache.ContainsKey(id))
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
