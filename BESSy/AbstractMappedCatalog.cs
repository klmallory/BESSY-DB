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
using System.IO;
using System.Diagnostics;
using BESSy.Serialization.Converters;
using BESSy.Files;
using System.Threading;
using BESSy.Cache;
using BESSy.Factories;
using BESSy.Serialization;

namespace BESSy
{
    public interface IMappedCatalog<EntityType, IdType, PropertyType> : IRepository<EntityType, IdType>, ISweep, IFlush, ILoad
    {
        void DetachCatalog(PropertyType catId);
        IDictionary<IdType, EntityType> GetCache();
    }

    public abstract class AbstractMappedCatalog<EntityType, IdType, PropertyType> : IMappedCatalog<EntityType, IdType, PropertyType>
    {
        protected AbstractMappedCatalog()
        {
        }

        protected IBinConverter<IdType> _idConverter;
        protected IBinConverter<PropertyType> _propertyConverter;

        protected bool _inFlush = false;
        Queue<long> _flushQueue = new Queue<long>();

        protected object _syncFlush = new object();
        protected object _syncFlushQueue = new object();

        protected IIndexRepository<IdType, PropertyType> _index;
        protected IRepositoryCache<PropertyType, IIndexedRepository<EntityType, IdType>> _catalogCache;

        protected IBatchFileManager<EntityType> _fileManager;
        protected ISafeFormatter _mapFormatter;
        protected Func<EntityType, IdType> _getId;
        protected Action<EntityType, IdType> _setId;
        protected Func<EntityType, PropertyType> _getProperty;

        protected string _indexFileNamePath;
        protected IIndexRepositoryFactory<IdType, PropertyType> _indexFactory;
        protected IRepositoryCacheFactory _cacheFactory;

        protected virtual IIndexedRepository<EntityType, IdType> GetCatalog(PropertyType catId)
        {
            if (_catalogCache.Contains(catId))
                return _catalogCache.GetFromCache(catId);

            var cat = GetCatalogFile(catId);

            try
            {
                cat.Load();
            }
            catch (FileNotFoundException fnfEx) { Trace.TraceError(fnfEx.ToString()); return null; }
            catch (FieldAccessException faEx) { Trace.TraceError(faEx.ToString()); return null; }
            catch (FileLoadException flEx) { Trace.TraceError(flEx.ToString()); return null; }

            _catalogCache.UpdateCache(catId, cat, true, false);

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
        protected abstract IIndexedRepository<EntityType, IdType> GetCatalogFile(PropertyType catId);

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

                if (_catalogCache.AsEnumerable().Where(c => c != null).Any(c => c.FileFlushQueueActive))
                    return true;

                return false;
            }
        }

        public virtual int Load()
        {
            if (File.Exists(_indexFileNamePath))
                _index = _indexFactory.Create(_indexFileNamePath);
            else
                _index = _indexFactory.CreateNew(_indexFileNamePath);

            _index.Load();

            this._idConverter = (IBinConverter<IdType>)_index.Seed.IdConverter;
            this._propertyConverter = (IBinConverter<PropertyType>)_index.Seed.PropertyConverter;

            _getId = (Func<EntityType, IdType>)Delegate.CreateDelegate(typeof(Func<EntityType, IdType>), typeof(EntityType).GetProperty(_index.Seed.IdProperty).GetGetMethod());
            _setId = (Action<EntityType, IdType>)Delegate.CreateDelegate(typeof(Action<EntityType, IdType>), typeof(EntityType).GetProperty(_index.Seed.IdProperty).GetSetMethod());
            _getProperty = (Func<EntityType, PropertyType>)Delegate.CreateDelegate(typeof(Func<EntityType, PropertyType>), typeof(EntityType).GetProperty(_index.Seed.CategoryIdProperty).GetGetMethod());

            _catalogCache = _cacheFactory.Create<PropertyType, IIndexedRepository<EntityType, IdType>>(true, _cacheFactory.DefaultCacheSize, _propertyConverter);

            if (_catalogCache.CacheSize < 1)
                _catalogCache.CacheSize = Caching.DetermineOptimumCacheSize(_index.Seed.Stride);

            return 0;
        }

        public void DetachCatalog(PropertyType catId)
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

                    if (!_catalogCache.Contains(catId))
                        return;

                    var catalog = _catalogCache.GetFromCache(catId);

                    _catalogCache.Detach(catId);

                    if (object.Equals(catalog, default(IIndexedRepository<EntityType, IdType>)))
                        return;

                    catalog.Flush();

                    while (catalog.FileFlushQueueActive)
                        Thread.Sleep(100);

                    catalog.Dispose();
                }
                finally { _inFlush = false; }
            }
        }

        public virtual void Sweep()
        {
            _catalogCache.Sweep();
            _index.Sweep();
        }

        public int Length
        {
            get { return _index.Length; }
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

            var cat = GetCatalog(catId);

            var id = cat.Add(item);

            _index.Add(new IndexPropertyPair<IdType, PropertyType>(id, catId));

            return id;
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
                    throw new InvalidOperationException(string.Format("Entity with prop {0} not Found to update.", id));

                var oldCat = GetCatalog(oldCatId);

                oldCat.Delete(id);

                _index.Delete(id);
            }

            catalog.AddOrUpdate(item, newId);

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

        #region Flush 

        public void Flush()
        {
            lock (_syncFlushQueue)
                _flushQueue.Enqueue(DateTime.Now.Ticks);

            ThreadPool.QueueUserWorkItem(new WaitCallback(BeginFlush));

            while (!_inFlush)
                Thread.Sleep(10);
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

                    var staging = _catalogCache.AsEnumerable();

                    foreach (var cat in staging)
                        cat.Flush();

                    while (staging.Any(s => s.FileFlushQueueActive))
                        Thread.Sleep(100);

                    _index.Flush();

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

        #endregion

        public IDictionary<IdType, EntityType> GetCache()
        {
            return _catalogCache
                .AsEnumerable()
                .Where(c => c != null)
                .SelectMany(c => c.GetCache())
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public void Clear()
        {
            _catalogCache.ClearCache();

            if (_index != null)
                _index.Clear();
        }

        public virtual void Dispose()
        {
            while (FileFlushQueueActive)
                Thread.Sleep(100);

            foreach (var c in _catalogCache.AsEnumerable().Where(c => c != null))
                    c.Dispose();

            lock (_syncFlush)
                if (_index != null)
                    _index.Dispose();

            Clear();

            _index = null;
        }

        
    }
}
