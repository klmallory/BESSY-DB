/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using BESSy.Serialization.Converters;
using BESSy.Files;
using System.Threading;
using BESSy.Cache;

namespace BESSy
{
    public interface IMappedCatalog<EntityType, IdType, PropertyType> : IRepository<EntityType, IdType>
        , ICache<EntityType, IdType>
        , ICache<IMappedRepository<EntityType, IdType>, PropertyType>
    {
        void Load();
        void Load(IList<PropertyType> catalogs);
        int CacheSize { get; set; }
        bool AutoCache { get; set; }
        bool FileFlushQueueActive { get; }
    }

    public abstract class AbstractMappedCatalog<EntityType, IdType, PropertyType> : IMappedCatalog<EntityType, IdType, PropertyType>
    {
        protected AbstractMappedCatalog()
        {
            AutoCache = true;
        }

        protected IBinConverter<IdType> _idConverter;
        protected IBinConverter<PropertyType> _propertyConverter;

        protected bool _inFlush = false;
        Queue<long> _flushQueue = new Queue<long>();
        List<PropertyType> _catalogDeferredCache = new List<PropertyType>();

        Dictionary<PropertyType, IMappedRepository<EntityType, IdType>> _catalogCache = 
            new Dictionary<PropertyType, IMappedRepository<EntityType, IdType>>();

        protected object _syncFlush = new object();
        protected object _syncFlushQueue = new object();
        protected object _syncCache = new object();
        protected IIndexRepository<IdType, PropertyType> _index;

        protected virtual void UpdateCache(PropertyType catId, IMappedRepository<EntityType, IdType> catalog)
        {
            lock (_syncCache)
            {
                if (AutoCache && _catalogCache.Count > CacheSize)
                    Sweep();

                if (_catalogCache.ContainsKey(catId))
                    _catalogCache[catId] = catalog;

                else if (_catalogDeferredCache.Contains(catId))
                    _catalogCache.Add(catId, catalog);

                else if (AutoCache)
                {
                    _catalogDeferredCache.Add(catId);
                    _catalogCache.Add(catId, catalog);
                }
            }
        }

        protected virtual IMappedRepository<EntityType, IdType> GetCatalog(PropertyType catId)
        {
            lock (_syncCache)
                if (_catalogCache.ContainsKey(catId))
                    return _catalogCache[catId];

            var cat = GetCatalogFile(catId);

            cat.Load();

            UpdateCache(catId, cat);

            return cat;
        }

        protected virtual EntityType GetFromCatalog(IdType id)
        {
            PropertyType catId = GetCatalogIdFor(id);

            if (object.Equals(catId, default(PropertyType)))
                return default(EntityType);

            var catalog = GetCatalog(catId);

            var item = catalog.Fetch(id);

            return item;
        }

        protected virtual PropertyType GetCatalogIdFor(IdType id)
        {
            return _index.Fetch(id).Property;
        }

        protected abstract PropertyType GetCatalogIdFrom(EntityType item);
        protected abstract IdType GetIdFrom(EntityType item);
        protected abstract IMappedRepository<EntityType, IdType> GetCatalogFile(PropertyType catId);

        public virtual int CacheSize { get; set; }
        public virtual bool AutoCache { get; set; }

        public virtual bool FileFlushQueueActive
        {
            get
            {
                if (_inFlush)
                    return true;

                if (_flushQueue.Count > 0)
                    return true;

                if (_index.FileFlushQueueActive)
                    return true;

                lock (_syncCache)
                    if (_catalogCache.Any(c => c.Value.FileFlushQueueActive))
                        return true;

                return false;
            }
        }

        public virtual void Load()
        {
            _index.Load();

            this._idConverter = (IBinConverter<IdType>)_index.Seed.IdConverter;
            this._propertyConverter = (IBinConverter<PropertyType>)_index.Seed.PropertyConverter;
        }

        public virtual void Load(IList<PropertyType> catalogs)
        {
            this.Load();

            foreach (var catId in catalogs)
            {
                GetCatalog(catId);
            }
        }

        #region ICache<IMappedRepository<EntityType,IdType>,PropertyType> Members

        bool ICache<IMappedRepository<EntityType, IdType>, PropertyType>.IsNew(PropertyType id)
        {
            return false;
        }

        bool ICache<IMappedRepository<EntityType, IdType>, PropertyType>.Contains(PropertyType id)
        {
            lock (_syncCache)
                return _catalogCache.ContainsKey(id);
        }

        IMappedRepository<EntityType, IdType> ICache<IMappedRepository<EntityType, IdType>, PropertyType>.GetFromCache(PropertyType id)
        {
            lock (_syncCache)
                if (_catalogCache.ContainsKey(id))
                    return _catalogCache[id];

            return default(IMappedRepository<EntityType, IdType>);
        }

        void ICache<IMappedRepository<EntityType, IdType>, PropertyType>.CacheItem(PropertyType id)
        {
            lock (_syncCache)
                _catalogDeferredCache.Add(id);
        }

        void ICache<IMappedRepository<EntityType, IdType>, PropertyType>.Detach(PropertyType catId)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(BeginDetach), catId);
        }

        void BeginDetach(object state)
        {
            lock (_syncFlush)
            {
                _inFlush = true;

                try
                {
                    var catId = (PropertyType)state;

                    if (_propertyConverter.Compare(catId, default(PropertyType)) == 0)
                        return;

                    IMappedRepository<EntityType, IdType> catalog = null;

                    lock (_syncCache)
                    {
                        if (!_catalogCache.ContainsKey(catId))
                            return;

                        catalog = _catalogCache[catId];
                    }

                    if (!object.Equals(catalog, default(IMappedRepository<EntityType, IdType>)))
                    {
                        catalog.Flush();

                        while (catalog.FileFlushQueueActive)
                            Thread.Sleep(100);

                        catalog.Dispose();
                    }

                    lock (_syncCache)
                        if (_catalogCache.ContainsKey(catId))
                            _catalogCache.Remove(catId);
                }
                finally
                {
                    _inFlush = false;
                }
            }
        }

        #endregion

        public virtual void Sweep()
        {
            if (_catalogCache.Count <= CacheSize || CacheSize == 0)
                return;

            lock (_syncCache)
            {
                var diff = (_catalogCache.Count - CacheSize);

                if (diff <= 0)
                    return;

                var r = _catalogCache.Keys.ToList().GetRange(0, diff).ToList();

                r.ForEach(a =>
                {
                    _catalogDeferredCache.RemoveAll(d => _propertyConverter.Compare(d, a) == 0);

                    if (_catalogCache.ContainsKey(a))
                    {
                        _catalogCache[a].Flush();
                        _catalogCache.Remove(a);
                    }
                });
            }

            _index.Sweep();
        }

        public int Count()
        {
            return _index.Count();
        }

        public bool IsNew(IdType id)
        {
            return false;
        }

        public bool Contains(IdType id)
        {
            var catId = GetCatalogIdFor(id);

            if (_propertyConverter.Compare(catId, default(PropertyType)) == 0)
                return false;

            lock (_syncCache)
                if (_catalogCache.ContainsKey(catId))
                    return _catalogCache[catId].Contains(id);

            return false;
        }

        public EntityType GetFromCache(IdType id)
        {
            var catId = GetCatalogIdFor(id);

            if (_propertyConverter.Compare(catId, default(PropertyType)) == 0)
                return default(EntityType);

            var cat = GetCatalog(catId);

            return cat.GetFromCache(id);
        }

        public void CacheItem(IdType id)
        {
            _index.CacheItem(id);
        }

        public void Detach(IdType id)
        {
            var catId = GetCatalogIdFor(id);

            //if (_propertyConverter.Compare(catId, default(PropertyType)) == 0)
            //    throw new KeyNotFoundException(string.Format("Catalog with id {0} not found.", catId));

            lock (_syncCache)
                if (_catalogCache.ContainsKey(catId))
                    _catalogCache[catId].Detach(id);

            _index.Detach(id);
        }

        public void ClearCache()
        {
            lock (_syncCache)
                _catalogDeferredCache.Clear();
        }

        public IdType Add(EntityType item)
        {
            return AddNew(item);
        }

        protected IdType AddNew(EntityType item)
        {
            if (object.Equals(item, default(EntityType)))
                throw new ArgumentNullException("Entity to add can not be null.");

            var catId = GetCatalogIdFrom(item);

            if (_propertyConverter.Compare(catId, default(PropertyType)) == 0)
                throw new ArgumentNullException("Catalog property can not be null.");

            lock (_syncCache)
            {
                var cat = GetCatalog(catId);

                var id = cat.Add(item);

                _index.Add(new IndexPropertyPair<IdType, PropertyType>(id, catId));

                UpdateCache(catId, cat);

                return id;
            }
            
        }

        public void AddOrUpdate(EntityType item, IdType id)
        {
            IdType newId = GetIdFrom(item);

            var catId = GetCatalogIdFor(id);

            if (_propertyConverter.Compare(catId, default(PropertyType)) != 0)
                UpdateExisiting(id, newId, item, catId);
            else
                AddNew(item);
        }

        public void Update(EntityType item, IdType id)
        {
            var newId = GetIdFrom(item);

            if (_idConverter.Compare(id, default(IdType)) == 0)
                id = newId;

            UpdateExisiting(id, newId, item);
        }

        protected void UpdateExisiting(IdType id, IdType newId, EntityType item, PropertyType oldCatId = default(PropertyType))
        {
            var catId = GetCatalogIdFrom(item);

            if (_propertyConverter.Compare(catId, default(PropertyType)) == 0)
                throw new ArgumentException("Catalog Id Property can not be null on update.");

            var catalog = GetCatalog(catId);

            if (_idConverter.Compare(id, newId) != 0)
            {
                if (_propertyConverter.Compare(oldCatId, default(PropertyType)) == 0)
                    oldCatId = GetCatalogIdFor(id);

                if (_propertyConverter.Compare(oldCatId, default(PropertyType)) == 0)
                    throw new InvalidOperationException(string.Format("Entity with id {0} not Found to update.", id));

                var oldCat = GetCatalog(oldCatId);

                oldCat.Delete(id);

                _index.Delete(id);
            }

            catalog.AddOrUpdate(item, newId);

            lock (_syncCache)
                UpdateCache(catId, catalog);

            _index.Update(new IndexPropertyPair<IdType, PropertyType>(newId, catId), id);
        }

        public EntityType Fetch(IdType id)
        {
           return GetFromCatalog(id);
        }

        public void Delete(IdType id)
        {
            var catId = GetCatalogIdFor(id);

            _index.Delete(id);

            if (_propertyConverter.Compare(catId, default(PropertyType)) == 0)
                return;

            var cat = GetCatalog(catId);

            if (cat == null)
            {
                Trace.TraceError("Catalog not found for {0}", catId);
                return;
            }

            cat.Delete(id);
        }

        public void Flush()
        {
            lock (_syncFlushQueue)
                _flushQueue.Enqueue(DateTime.Now.Ticks);

            ThreadPool.QueueUserWorkItem(new WaitCallback(BeginFlush));
        }

        private void BeginFlush(object state)
        {
            if (!Monitor.TryEnter(_syncFlush, 500))
                return;

            try
            {
                long queue = 0;

                lock (_syncFlushQueue)
                    if (_flushQueue.Count > 0)
                        queue = _flushQueue.Dequeue();

                while (queue > 0)
                {
                    _inFlush = true;

                    List<IMappedRepository<EntityType, IdType>> staging = null;

                    lock (_syncCache)
                    {
                        staging = _catalogCache.Values.ToList();

                        foreach (var s in staging)
                            s.Flush();

                        while (staging.Any(s => s.FileFlushQueueActive))
                            Thread.Sleep(100);

                        _index.Flush();
                    }

                    while (_index.FileFlushQueueActive)
                        Thread.Sleep(100);

                    lock (_syncFlushQueue)
                        if (_flushQueue.Count > 0)
                            queue = _flushQueue.Dequeue();
                        else
                            queue = 0;
                }
            }
            finally
            {
                Monitor.Exit(_syncFlush);

                _inFlush = false;
            }
        }

        public void Clear()
        {
            ClearCache();

            lock (_syncCache)
                foreach (var c in _catalogCache)
                    if (c.Value != null)
                        c.Value.Clear();

            _index.Clear();
        }

        public virtual void Dispose()
        {
            while (FileFlushQueueActive)
                Thread.Sleep(100);

            

            foreach (var c in _catalogCache)
                if (c.Value != null)
                    c.Value.Dispose();

            lock (_syncFlush)
                if (_index != null)
                    _index.Dispose();

            ClearCache();

            _index = null;
        }

    }
}
