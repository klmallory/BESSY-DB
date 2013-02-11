/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using BESSy.Serialization;
using BESSy.Files;

namespace BESSy
{
    public interface IFileRepository<T, I> : IRepository<T, I>, IFlush
    {
        void Load();
    }

    public abstract class AbstractFileRepository<T, I> : IRepository<T, I>, IFlush
    {
        public AbstractFileRepository
            (string fileName
            , IFileRepository<IList<T>> fileManager)
        {
            _fileName = fileName;
        }

        object _syncRoot = new object();
        Dictionary<I, T> _cache = new Dictionary<I, T>();

        protected IFileRepository<IList<T>> _fileManager {get; set;}
        protected string _fileName { get; set; }
        protected abstract I GetId(T item);

        public void Load()
        {
            lock (_syncRoot)
            {
                Clear();

                var items = _fileManager.LoadFromFile(_fileName);

                foreach (var item in items)
                {
                    var id = GetId(item);

                    _cache.Add(id, item);
                }
            }
        }

        public void Flush()
        {
            lock (_syncRoot)
            {
                _fileManager.SaveToFile(_cache.Values.ToList(), _fileName);
            }
        }

        public T Fetch(I id)
        {
            lock (_syncRoot)
                if (_cache.ContainsKey(id))
                    return _cache[id];

            return default(T);
        }

        public IQueryable<T> AsQueryable()
        {
            return _cache.Values.AsQueryable();
        }

        public void Delete(I id)
        {
            lock (_syncRoot)
                if (_cache.ContainsKey(id))
                    _cache.Remove(id);
        }

        public int Count()
        {
            return _cache.Count;
        }

        public void Clear()
        {
            lock (_syncRoot)
                _cache.Clear();
        }

        public I Add(T item)
        {
            var id = GetId(item);

            lock (_syncRoot)
            {
                if (_cache.ContainsKey(id))
                    return default(I);

                _cache.Add(id, item);
            }

            return id;
        }

        public void AddOrUpdate(T item, I id)
        {
            lock (_syncRoot)
            {
                if (_cache.ContainsKey(id))
                    _cache[id] = item;
                else
                    _cache.Add(id, item);
            }
        }

        public void Update(T item, I id)
        {
            lock (_syncRoot)
            {
                if (_cache.ContainsKey(id))
                    _cache[id] = item;
                else
                    throw new InvalidOperationException(String.Format("No item with id {0} to update", id));
            }
        }

        public void Dispose()
        {
            if (_cache != null)
                _cache.Clear();
        }
    }
}
