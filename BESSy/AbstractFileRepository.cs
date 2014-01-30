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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Xml.Serialization;
using BESSy.Serialization;
using BESSy.Files;

namespace BESSy
{
    public interface IFileRepository<C, T, I> : IRepository<T, I>, IFlush, ILoad where C : XmlContainer<T>
    {
    }

    public abstract class XmlContainer<T>
    {
        public XmlContainer()
        {
            AsList = new List<T>();
        }

        [XmlIgnore]
        public abstract List<T> AsList { get; set; }
    }

    public abstract class AbstractFileRepository<C, T, I> : IFileRepository<C, T, I> where C: XmlContainer<T>, new()
    {
        public AbstractFileRepository
            (string fileName
            , IFileRepository<C> fileManager)
        {
            _fileName = fileName;
            _fileManager = fileManager;
        }

        object _syncRoot = new object();
        Dictionary<I, T> _cache = new Dictionary<I, T>();
        protected C _container;
        private bool _busy;
        protected IFileRepository<C> _fileManager { get; set; }
        protected string _fileName { get; set; }
        protected abstract I GetId(T item);

        public int Load()
        {
            lock (_syncRoot)
            {
                Clear();

                _container = _fileManager.LoadFromFile(_fileName) ?? new C();

                var list = _container.AsList;

                foreach (var item in list)
                {
                    var id = GetId(item);

                    _cache.Add(id, item);
                }

                return list.Count;
            }
        }

        public bool FileFlushQueueActive { get { return _busy; } }

        public void Flush()
        {
            lock (_syncRoot)
            {
                try
                {
                    _busy = true;

                    _container.AsList = _cache.Values.ToList();

                    _fileManager.SaveToFile(_container, _fileName);
                }
                finally { _busy = false; }
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

        public int Length
        {
            get
            {
                return _cache.Count;
            }
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

        public I AddOrUpdate(T item, I id)
        {
            lock (_syncRoot)
            {
                if (_cache.ContainsKey(id))
                    Update(item, id);
                else
                    _cache.Add(id, item);
            }

            return id;
        }

        public void Update(T item, I id)
        {
            lock (_syncRoot)
            {
                if (_cache.ContainsKey(id))
                    _cache[id] = item;
                else
                    throw new InvalidOperationException(String.Format("No item with prop {0} to update", id));
            }
        }

        public void Dispose()
        {
            if (_cache != null)
                _cache.Clear();
        }
    }
}
