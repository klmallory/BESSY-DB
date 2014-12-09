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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Resources;
using System.Text;
using BESSy.Files;
using BESSy.Containers;

namespace BESSy.Resources
{
    public interface IResourceWrapper<T> : IReadOnlyRepository<T, string>, IEnumerable<KeyValuePair<string, T>>
    {
        T GetFrom(Stream contents);
        T GetFrom(byte[] contents);
        T GetFrom(string value);
        ResourceSet ResourceSet { get; }
    }

    public abstract class AbstractResourceWrapper<T> : IResourceWrapper<T>, IReadOnlyRepository<T, string>, IEnumerable<KeyValuePair<string, T>>
    {
        protected ResourceManager _manager { get; set; }
        protected ResourceSet _set { get; set; }

        public AbstractResourceWrapper(ResourceManager manager)
        {
            if (manager == null)
                throw new ArgumentNullException("manager", "The ResourceManager was null.");

            _manager = manager;

            _set = _manager.GetResourceSet(CultureInfo.CurrentCulture, true, true);
            Length = _set.Cast<DictionaryEntry>().Count();
        }

        protected virtual T GetFileContents(string id)
        {
            var obj = _manager.GetObject(id);

            if (obj == null)
            {
                Trace.TraceError(string.Format("File not found : '{0}'", id));
#if DEBUG
                throw new ContentNotFoundException(id, id);
#endif
            }

            if (obj is byte[])
                return GetFrom((byte[])obj);
            else if (obj is Stream)
                return GetFrom((Stream)obj);
            else
                return (T)obj;
        }

        public R CastContentsAs<R>(string id) where R : class
        {
            var obj = _manager.GetObject(id);

            if (obj == null)
                return null;

            return (R)obj;
        }

        public Stream GetFileStream(string id)
        {
            var stream = _manager.GetStream(id);

            if (stream == null)
            {
                Trace.TraceError(string.Format("File not found : '{0}'", id));
#if DEBUG
                throw new ContentNotFoundException(id, id);
#endif
            }

            return stream;
        }

        public T GetContents(string id)
        {
            return GetFileContents(id);
        }

        public abstract T GetFrom(Stream contents);
        public abstract T GetFrom(string value);
        public abstract T GetFrom(byte[] contents);
        public abstract T Fetch(string id);

        public virtual long Length { get; set; }

        public ResourceSet ResourceSet
        {
            get
            {
                return _set;
            }
        }

        public virtual void Sweep() { }

        public virtual void Clear()
        {
            _manager.ReleaseAllResources();
        }

        public virtual void Dispose()
        {
            if (_manager != null)
                _manager.ReleaseAllResources();
        }

        public IEnumerator<KeyValuePair<string, T>> GetEnumerator()
        {
            return new ResourceEnumerator<T>(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
