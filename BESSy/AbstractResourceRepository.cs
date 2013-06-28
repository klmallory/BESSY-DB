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

namespace BESSy
{
    public abstract class AbstractResourceRepository<T> : IReadOnlyRepository<T, string>
    {
        protected ResourceManager _manager { get; set; }

        public AbstractResourceRepository(ResourceManager manager)
        {
            if (manager == null)
                throw new ArgumentNullException("manager", "The ResourceManager was null.");

            _manager = manager;
        }

        protected Stream GetFileStream(string id)
        {
            var bytes = (byte[])_manager.GetObject(id);

            var stream = new MemoryStream(bytes);

            if (stream == null)
            {
                Trace.TraceError(string.Format("File not found : '{0}'", id));
#if DEBUG
                throw new ContentNotFoundException(id, id);
#endif
            }


            return stream;
        }

        protected byte[] GetFileContents(string id)
        {
            Byte[] file = _manager.GetObject(id) as Byte[];

            if (file == null)
            {
                Trace.TraceError(string.Format("File not found : '{0}'", id));
#if DEBUG
                throw new ContentNotFoundException(id, id);
#endif
            }


            return file;
        }

        public abstract T Fetch(string id);

        public virtual void Sweep()
        {

        }

        public virtual int Length
        {
            get
            {
                //TODO: figure out how to do this with resource files.
                //return _manager.GetResourceSet(CultureInfo.CurrentCulture, true, false).AsQueryable().OfType<T>().Count();
                return 0;
            }
        }

        public virtual void Clear()
        {
            _manager.ReleaseAllResources();
        }

        public virtual void Dispose()
        {
            if (_manager != null)
                _manager.ReleaseAllResources();
        }
    }
}
