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
    public abstract class AbstractResourceRepository<T> : IReadOnlyRepository<T, string>
    {
        //TODO: Reimplement with dynamic caching, and a full write repository.

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

        public virtual int Count()
        {
            //TODO: figure out how to do this with resource files.
            //return _manager.GetResourceSet(CultureInfo.CurrentCulture, true, false).AsQueryable().OfType<T>().Count();
            return 0;
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
