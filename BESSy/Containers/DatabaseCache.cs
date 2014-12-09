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
using BESSy.Transactions;

namespace BESSy.Cache
{
    public interface IDatabaseCache<IdType, EntityType> : IAutoCache<IdType, EntityType>, IDisposable
    {
        bool IsDirty { get; }
        int DirtyCount { get; }
        int Count { get; }

        void UpdateCache(IdType id, EntityType entity, bool forceCache, bool dirty);

        IDictionary<IdType, EntityType> UnloadDirtyItems();
        IDictionary<IdType, EntityType> GetCache();
        event EventHandler FlushRequested;
        IEnumerable<EntityType> AsEnumerable();
 }

    public class DatabaseCache<IdType, EntityType> : IDatabaseCache<IdType, EntityType>
    {
        public DatabaseCache(bool autoCache, int cacheSize, IBinConverter<IdType> idConverter)
        {
            _idConverter = idConverter;

            CacheSize = cacheSize;
            AutoCache = autoCache;
        }

        List<IdType> _dirtyIds = new List<IdType>();
        List<IdType> _deferredCache = new List<IdType>();
        Dictionary<IdType, EntityType> _cache = new Dictionary<IdType, EntityType>();

        IBinConverter<IdType> _idConverter;

        public bool AutoCache { get; set; }
        public int CacheSize { get; set; }

        public bool IsDirty { get { return _dirtyIds.Count > 0; } }
        public int DirtyCount { get { return _dirtyIds.Count; } }
        public int Count { get { return _cache.Count; } }

        public bool IsNew(IdType id)
        {
            return false;
        }

        public bool Contains(IdType id)
        {
             return _cache.ContainsKey(id);
        }

        public EntityType GetFromCache(IdType id)
        {
            if (_cache.ContainsKey(id))
                return _cache[id];

            return default(EntityType);
        }

        public void CacheItem(IdType id)
        {
            lock (this)
                _deferredCache.Add(id);
        }

        public void Detach(IdType id)
        {
            lock (this)
            {
                _cache.Remove(id);
                _dirtyIds.RemoveAll(d => _idConverter.Compare(d, id) == 0);
                _deferredCache.RemoveAll(c => _idConverter.Compare(c, id) == 0);
            }
        }

        public void ClearCache()
        {
            lock (this)
            {
                _cache.Clear();
                _deferredCache.Clear();
                _dirtyIds.Clear();
            }
        }

        public void Sweep()
        {
            bool requestFlush = false;

            lock (this)
            {
                if (_cache.Count < CacheSize)
                    return;

                if (_dirtyIds.Count > 0)
                    requestFlush = true;
                else
                {
                    //clear enough cache for smooth operations.
                    var toRemove = _deferredCache.Distinct().Take(_cache.Count - (CacheSize / 2)).ToList();
                    _deferredCache = _deferredCache.Distinct().Skip(_cache.Count - (CacheSize / 2)).ToList();
                    
                    foreach (var id in toRemove)
                    {
                        //_deferredCache.RemoveAll(c => IdConverter.Compare(c, id) == 0);
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

        #region IDatabaseCache<EntityType,SeedType> Members

        public void UpdateCache(IdType id, EntityType entity, bool forceCache, bool dirty)
        {
            lock (this)
            {
                if (AutoCache || forceCache)
                    if (_cache.Count > CacheSize)
                        Sweep();

                if (_cache.ContainsKey(id))
                    _cache[id] = entity;

                else if (_deferredCache.Contains(id))
                    _cache.Add(id, entity);

                else if (AutoCache || forceCache)
                {
                    _deferredCache.Add(id);
                    _cache.Add(id, entity);
                }
                else
                    return;

                if (dirty && !_dirtyIds.Contains(id))
                    _dirtyIds.Add(id);
            }
        }

        public IDictionary<IdType, EntityType> UnloadDirtyItems()
        {
            var dirty = new Dictionary<IdType, EntityType>();

            lock (this)
            {
                foreach (var d in _dirtyIds)
                    dirty.Add(d, _cache[d]);

                foreach (var id in dirty.Keys)
                {
                    _cache.Remove(id);
                    _deferredCache.RemoveAll(c => _idConverter.Compare(c, id) == 0);
                }

                _dirtyIds.Clear();
            }

            return dirty;
        }

        public IDictionary<IdType, EntityType> GetCache()
        {
            return _cache;
        }

        public IEnumerable<EntityType> AsEnumerable()
        {
            return _cache.Values.AsEnumerable();
        }

        #endregion

        public void Dispose()
        {

        }


    }
}