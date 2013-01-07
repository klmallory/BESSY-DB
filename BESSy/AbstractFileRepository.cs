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

namespace BESSy
{
    public abstract class AbstractXmlFileRepository<T, I> : IRepository<T, I>
    {
        public AbstractXmlFileRepository(string fileName, string workingFolder, bool createNew)
        {
            var file = Path.Combine(workingFolder, fileName);

            if (!File.Exists(file))
                if (createNew)
                    File.Create(file);
                else
                    throw new InvalidDataException(String.Format("file missing: {0}", file));

            _workingFolder = workingFolder;
            _fileName = fileName;
        }

        Dictionary<I, T> _cache = new Dictionary<I, T>();

        protected string _workingFolder { get; private set; }
        protected string _fileName { get; private set; }

        public abstract void LoadFile();
        public abstract void LoadFile(string fileName, string workingFolder);
        public abstract void Flush(IList<T> dataSource);
        public abstract I GetId(T item);

        public T Fetch(I id)
        {
            if (_cache.ContainsKey(id))
                return _cache[id];

            return default(T);
        }

        public T Find(Func<T, bool> func)
        {
            return _cache.Values.FirstOrDefault(func);
        }

        public IList<T> FindAll(Func<T, bool> func)
        {
            return _cache.Values.Where(func).ToList();
        }

        public IQueryable<T> AsQueryable()
        {
            return _cache.Values.AsQueryable();
        }

        public void Delete(I id)
        {
            if (_cache.ContainsKey(id))
                _cache.Remove(id);
        }

        public int Count()
        {
            return _cache.Count;
        }

        public void Clear()
        {
            _cache.Clear();
        }

        public void LoadContent(T item)
        {
            var id = GetId(item);

            if (!_cache.ContainsKey(id))
                _cache.Add(id, item);
        }

        public void LoadContent(IList<T> dataSource)
        {
            foreach (var item in dataSource)
                LoadContent(item);
        }

        public I Add(T item)
        {
            var id = GetId(item);

            if (_cache.ContainsKey(id))
                return default(I);

            _cache.Add(id, item);

            return id;
        }

        public void AddOrUpdate(T item, I id)
        {
            if (_cache.ContainsKey(id))
                _cache[id] = item;
            else
                _cache.Add(id, item);
        }

        public void Update(T item, I id)
        {
            if (_cache.ContainsKey(id))
                _cache[id] = item;
            else
                throw new InvalidOperationException(String.Format("No item with id {0} to update", id));
        }

        public void Dispose()
        {
            if (_cache != null)
                _cache.Clear();
        }
    }
}
