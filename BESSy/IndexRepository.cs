using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Security.AccessControl;
using BESSy.Seeding;
using BESSy.Serialization.Converters;
using BESSy.Files;
using BESSy.Extensions;

namespace BESSy
{
    public interface IIndexRepository<IdType, PropertyType> : IMappedRepository<IndexPropertyPair<IdType, PropertyType>, IdType>
    {
        ISeed<IdType> Seed { get; }
        IList<IdType> RidLookup(PropertyType property);
        void FlushSeed();
    }

    public class IndexRepository<IdType, PropertyType> : IIndexRepository<IdType, PropertyType>
    {
        public IndexRepository
            (bool createIfNotExistant,
            int cacheSize,
            string fileName, 
            ISeed<IdType> seed,
            IBinConverter<IdType> idConverter,
            IBatchFileManager<IndexPropertyPair<IdType, PropertyType>> fileManager,
            IIndexMapManager<IdType, PropertyType> mapFileManager)
        {
            _create = createIfNotExistant;
            _fileName = fileName;
            _mapFileName = fileName + ".mapping";
            _fileManager = fileManager;
            _mapFileManager = mapFileManager;
            _idConverter = idConverter;

            AutoCache = true;
            Seed = seed;

            if (cacheSize < 0)
                DetermineOptimumCacheSize();
            else
                CacheSize = cacheSize;

            _mapFileManager.OnFlushCompleted += new FlushCompleted<PropertyType, IdType>(HandleOnFlushCompleted);
        }
        
        List<IdType> _cacheQueue = new List<IdType>();
        IDictionary<IdType, PropertyType> _cache = new SortedDictionary<IdType, PropertyType>();
        List<IDictionary<IdType, PropertyType>> _stagingCache = new List<IDictionary<IdType, PropertyType>>();
        Queue<long> _fileFlushQueue = new Queue<long>();

        bool _inFlush;
        bool _create;
        string _fileName;
        string _mapFileName;
        IBatchFileManager<IndexPropertyPair<IdType, PropertyType>> _fileManager;
        IIndexMapManager<IdType, PropertyType> _mapFileManager;
        IBinConverter<IdType> _idConverter;

        protected bool _cacheIsDirty { get; set; }
        protected object _syncCache = new object();
        protected object _syncStaging = new object();
        protected object _syncFileQueue = new object();
        protected object _syncFileFlush = new object();

        protected virtual void DetermineOptimumCacheSize()
        {
            if (Environment.Is64BitProcess)
                CacheSize = 512000000 / (((int)(Math.Ceiling(Seed.Stride / (double)Environment.SystemPageSize))) * Environment.SystemPageSize).Clamp(1, int.MaxValue);
            else
                CacheSize = 256000000 / (((int)(Math.Ceiling(Seed.Stride / (double)Environment.SystemPageSize))) * Environment.SystemPageSize).Clamp(1, int.MaxValue);
        }

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


        protected virtual void UpdateCache(IdType id, PropertyType property, bool isDirty, bool forceCache = false)
        {
            _cacheIsDirty |= isDirty;

            if (_idConverter.Compare(id, default(IdType)) == 0)
                return;

            if (_cache.ContainsKey(id))
                _cache[id] = property;

            else if (_cacheQueue.Contains(id))
                _cache.Add(id, property);

            else if (AutoCache || forceCache)
                _cache.Add(id, property);

            if (AutoCache && _cache.Count > CacheSize)
                Sweep();
        }

        public virtual int CacheSize { get; set; }

        public virtual bool AutoCache { get; set; }

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

                lock (_syncCache)
                {

                    _cache.Clear();

                    _cacheQueue.Clear();

                    _cacheIsDirty = false;

                    lock (_syncStaging)
                        _stagingCache.Clear();
                }

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
                        Seed = seed;

                    _mapFileManager.OpenOrCreate(_mapFileName, count, Seed.Stride);

                    var batch = LoadBatchFromFile(stream);

                    var seg = 0;

                    while (seg < count && batch.IsNotNullAndNotEmpty())
                    {
                        seg = _mapFileManager.SaveBatchToFile(batch, seg);

                        batch = LoadBatchFromFile(stream);
                    }
                }
            }

            return Count();
        }

        protected virtual IList<IndexPropertyPair<IdType, PropertyType>> LoadBatchFromFile(FileStream stream)
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
            lock (_syncCache)
            {
                if (_stagingCache.Count > 0)
                    lock (_syncStaging)
                        _stagingCache.RemoveAll(s => s.Count == 0);

                IDictionary<IdType, PropertyType> sort = new SortedDictionary<IdType, PropertyType>(_cache);

                lock (_syncStaging)
                    _stagingCache.Add(sort);

                _mapFileManager.Flush(sort);

                ClearCache();

                _cacheIsDirty = false;
            }
        }

        public void Flush(IList<IndexPropertyPair<IdType, PropertyType>> dataSource)
        {
            throw new NotImplementedException();
        }

        public virtual void Flush()
        {
            if (_cacheIsDirty)
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

                            if (batch.Count > CacheSize)
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

        public virtual void FlushSeed()
        {
            lock (_syncFileFlush)
            {
                Trace.TraceInformation("_syncFileFlush entered.");

                _inFlush = true;

                var tempFileName = _fileName + Guid.NewGuid().ToString();

                using (var f = _fileManager.GetWritableFileStream(tempFileName))
                {
                    long position = _fileManager.SaveSeed<IdType>(f, Seed);

                    f.Flush();
                    f.Close();

                    GC.Collect();

                    GC.WaitForFullGCComplete(5000);

                    FileInfo fi = new FileInfo(tempFileName);

                    fi.Replace(_fileName, _fileName + ".old", true);
                }

                Trace.TraceInformation("_syncFileFlush exited.");

                _inFlush = false;
            }
        }

        public virtual IdType Add(IndexPropertyPair<IdType, PropertyType> item)
        {
            if (_idConverter.Compare(item.Id, default(IdType)) == 0)
                return default(IdType);

            UpdateCache(item.Id, item.Property, true, true);

            return item.Id;
        }

        public void AddOrUpdate(IndexPropertyPair<IdType, PropertyType> item, IdType id)
        {
            if (object.Equals(item, default(IndexPropertyPair<IdType, PropertyType>)))
                throw new ArgumentException("Indexer was null.", "item");

            if (_idConverter.Compare(item.Id, id) != 0)
                Delete(id);

            UpdateCache(item.Id, item.Property, true, true);
        }

        public void Update(IndexPropertyPair<IdType, PropertyType> item, IdType id)
        {
            if (_idConverter.Compare(id, default(IdType)) == 0)
                id = item.Id;

            if (_idConverter.Compare(item.Id, id) != 0)
                Delete(id);

            UpdateCache(item.Id, item.Property, true, true);
        }

        public IndexPropertyPair<IdType, PropertyType> Fetch(IdType id)
        {
            IndexPropertyPair<IdType, PropertyType> item; // = default(IndexPropertyPair<IdType, PropertyType>);

            if (Contains(id))
                item = GetFromCache(id);
            else
                item = new IndexPropertyPair<IdType, PropertyType>(id, _mapFileManager.Load(id));

            UpdateCache(id, item.Property, false);

            return item;
        }

        public IList<IdType> RidLookup(PropertyType property)
        {
            var ids = _mapFileManager.RidLookup(property);

            return ids;
        }

        public virtual void Delete(IdType id)
        {
            if (_mapFileManager.Save(default(PropertyType), id))
                _mapFileManager.Detach(id);

            UpdateCache(id, default(PropertyType), true, true);
        }

        #region ILinqRepository<PropertyType,IdType> Members

        public IEnumerable<IndexPropertyPair<IdType, PropertyType>> Take(int count)
        {
            lock (_syncFileFlush)
                return Enumerable.Take(_mapFileManager, count);
        }

        public IEnumerable<IndexPropertyPair<IdType, PropertyType>> Skip(int count)
        {
            if (count > this._mapFileManager.Length)
                throw new ArgumentException("items to skip is greater than the size of the repository.");

            lock (_syncFileFlush)
                return Enumerable.Skip(_mapFileManager, count);
        }

        public IEnumerable<IndexPropertyPair<IdType, PropertyType>> Where(Func<IndexPropertyPair<IdType, PropertyType>, bool> query)
        {
            lock (_syncFileFlush)
                return Enumerable.Where(_mapFileManager, query);
        }

        public IndexPropertyPair<IdType, PropertyType> First(Func<IndexPropertyPair<IdType, PropertyType>, bool> query)
        {
            lock (_syncFileFlush)
                return Enumerable.First(_mapFileManager, query);
        }

        public IndexPropertyPair<IdType, PropertyType> FirstOrDefault(Func<IndexPropertyPair<IdType, PropertyType>, bool> query)
        {
            lock (_syncFileFlush)
                return Enumerable.FirstOrDefault(_mapFileManager, query);
        }

        public IndexPropertyPair<IdType, PropertyType> Last(Func<IndexPropertyPair<IdType, PropertyType>, bool> query)
        {
            lock (_syncFileFlush)
                return Enumerable.Last(_mapFileManager, query);
        }

        public IndexPropertyPair<IdType, PropertyType> LastOrDefault(Func<IndexPropertyPair<IdType, PropertyType>, bool> query)
        {
            lock (_syncFileFlush)
                return Enumerable.LastOrDefault(_mapFileManager, query);
        }

        public IEnumerable<TResult> OfType<TResult>()
        {
            lock (_syncFileFlush)
                return Enumerable.OfType<IndexPropertyPair<IdType, TResult>>(_mapFileManager).Select(m => m.Property);
        }

        public IQueryable<IndexPropertyPair<IdType, PropertyType>> AsQueryable()
        {
            lock (_syncFileFlush)
                return _mapFileManager.AsQueryable<IndexPropertyPair<IdType, PropertyType>>();
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

        public IndexPropertyPair<IdType, PropertyType> GetFromCache(IdType id)
        {
            lock (_syncCache)
            {
                lock (_syncStaging)
                {
                    if (_cache.ContainsKey(id))
                        return new IndexPropertyPair<IdType, PropertyType>(id, _cache[id]);
                    else if (_stagingCache.Any(c => c.ContainsKey(id)))
                        return new IndexPropertyPair<IdType, PropertyType>
                            (id, _stagingCache.Last(s => s.ContainsKey(id))[id]);
                }
            }

            return default(IndexPropertyPair<IdType, PropertyType>);
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
                _mapFileManager.OnFlushCompleted -= new FlushCompleted<PropertyType, IdType>(HandleOnFlushCompleted);

                _mapFileManager.Dispose();
            }
        }
    }
}
