using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Serialization.Converters;

namespace BESSy.Cache
{
    public interface IRepositoryCache<IdType, EntityType> : ICache<EntityType, IdType>
    {
        int CacheSize { get; }
        int DirtyCount { get; }
        int Count { get; }

        void UpdateCache(IdType id, EntityType entity, bool autoCache, bool dirty);
        IDictionary<IdType, EntityType> UnloadDirtyItems();
    }

    public class DatabaseCache<IdType, EntityType> : IRepositoryCache<IdType, EntityType>
    {
        public DatabaseCache(int cacheSize, IBinConverter<IdType> idConverter)
        {
            _idConverter = idConverter;
            CacheSize = cacheSize;
        }

        object _syncRoot = new object();
        List<IdType> _dirtyIds = new List<IdType>();
        List<IdType> _deferredCache = new List<IdType>();
        Dictionary<IdType, EntityType> _cache = new Dictionary<IdType, EntityType>();

        IBinConverter<IdType> _idConverter;

        public int CacheSize { get; set; }

        public int DirtyCount
        {
            get
            {
                return _dirtyIds.Count;
            }
        }

        public int Count
        {
            get
            {
                return _cache.Count;
            }
        }

        public bool IsNew(IdType id)
        {
            return false;
        }

        public bool Contains(IdType id)
        {
            lock (_syncRoot)
                return _cache.ContainsKey(id);
        }

        public EntityType GetFromCache(IdType id)
        {
            lock (_syncRoot)
                if (_cache.ContainsKey(id))
                    return _cache[id];

            return default(EntityType);
        }

        public void CacheItem(IdType id)
        {
            lock (_syncRoot)
                _deferredCache.Add(id);
        }

        public void Detach(IdType id)
        {
            lock (_syncRoot)
                _cache.Remove(id);
        }

        public void ClearCache()
        {
            lock (_syncRoot)
                _cache.Clear();
        }

        public void Sweep()
        {
            bool requestFlush = false;

            lock (_cache)
            {
                if (_cache.Count < CacheSize)
                    return;

                if (_dirtyIds.Count > 0)
                    requestFlush = true;
                else
                {
                    //clear enough cache for smooth operations.
                    var toRemove = _deferredCache.Distinct().Take(CacheSize / 2).ToList();

                    foreach (var id in toRemove)
                    {
                        _deferredCache.RemoveAll(c => _idConverter.Compare(c, id) == 0);
                        _cache.Remove(id);
                    }
                }
            }

            if (requestFlush)
                InvokeFlushRequested();
        }

        #region FlushRequested

        protected void InvokeFlushRequested()
        {
            if (FlushRequested != null)
                FlushRequested.DynamicInvoke(this, new EventArgs());
        }

        public event EventHandler FlushRequested;

        #endregion

        #region IDatabaseCache<EntityType,IdType> Members

        public void UpdateCache(IdType id, EntityType entity, bool autoCache, bool dirty)
        {
            lock (_syncRoot)
            {
                if (autoCache)
                    if (_cache.Count > CacheSize)
                        Sweep();

                if (_cache.ContainsKey(id))
                    _cache[id] = entity;

                else if (_deferredCache.Contains(id))
                    _cache.Add(id, entity);

                else if (autoCache)
                {
                    _deferredCache.Add(id);
                    _cache.Add(id, entity);
                }
                else
                    return;

                if (dirty)
                    _dirtyIds.Add(id);
            }
        }

        public IDictionary<IdType, EntityType> UnloadDirtyItems()
        {
            var dirty = new Dictionary<IdType, EntityType>();

            lock (_syncRoot)
            {
                foreach (var d in _dirtyIds)
                {
                    if (!_cache.ContainsKey(d))
                        continue;

                    dirty.Add(d, _cache[d]);
                }

                _dirtyIds.Clear();
            }

            return dirty;
        }

        #endregion
    }
}