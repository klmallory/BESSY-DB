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
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BESSy.Json;
using System.Diagnostics;
using System.Security.AccessControl;
using BESSy.Serialization;
using BESSy.Extensions;
using BESSy.Synchronization;
using BESSy.Serialization.Converters;

namespace BESSy.Files
{
    public abstract class MapManager<EntityType> : IEntityMapManager<EntityType>
    {
        public MapManager(IQueryableFormatter formatter)
        {
            _formatter = formatter;

            Synchronizer = new RowSynchronizer<int>(new BinConverter32());
        }

        protected object _syncFlush = new object();
        protected object _syncMap = new object();

        protected bool _inFlush = false;
        protected IQueryableFormatter _formatter { get; set; }
        protected MemoryMappedFile _file { get; set; }

        protected string _handle;
        protected string _fileName;

        public int Stride { get; protected set; }
        public int Length { get; protected set; }
        public IRowSynchronizer<int> Synchronizer { get; protected set; }

        public virtual bool FlushQueueActive
        {
            get
            {
                return false;
            }
        }

        public virtual void OpenOrCreate(string fileName, int length, int stride)
        {
            lock (_syncMap)
            {
                Stride = stride;

                var len = length.Clamp(1, int.MaxValue);

                var handle = Guid.NewGuid().ToString();
                
                _file = MemoryMappedFile.CreateOrOpen
                    (handle
                    , Stride * len
                    , MemoryMappedFileAccess.ReadWriteExecute
                    , MemoryMappedFileOptions.None
                    , new MemoryMappedFileSecurity()
                    , HandleInheritability.Inheritable);

                var fi = new FileInfo(fileName);

                _fileName = fileName;

                Length = length;
            }
        }

        public virtual bool SaveToFile(EntityType obj, int segment)
        {
            Stream stream;

            if (!_formatter.TryFormatObj(obj, out stream))
                stream = new MemoryStream(new Byte[Stride]);

            if (stream.Length > Stride)
                throw new InvalidDataException("this object is too large for this file format.");

            using (var lck = Synchronizer.Lock(segment))
            {
                using (var view = _file.CreateViewStream(Stride * segment, Stride, MemoryMappedFileAccess.ReadWriteExecute))
                {
                    stream.Position = 0;
                    stream.WriteAllTo(view);

                    view.Flush();
                    view.Close();
                }
            }

            return true;
        }

        public virtual int SaveBatchToFile(IList<EntityType> objs, int segmentStart)
        {
            if (objs == null || objs.Count == 0)
                return 0;

            lock (_syncFlush)
            {
                using (var lck = Synchronizer.Lock(new Range<int>(segmentStart, segmentStart + objs.Count)))
                {
                    using (var view = _file.CreateViewStream(Stride * segmentStart
                        , objs.Count * Stride
                        , MemoryMappedFileAccess.ReadWriteExecute))
                    {
                        foreach (var obj in objs)
                        {
                            Stream stream;
                            if (!_formatter.TryFormatObj(obj, out stream))
                                stream = new MemoryStream();
#if DEBUG
                            if (stream.Length > Stride)
                                throw new InvalidDataException("this object is too large for this file format.");
#endif

                            stream.SetLength(Stride);

                            stream.Position = 0;
                            stream.WriteAllTo(view);
                        }

                        view.Flush();
                        view.Close();
                    }
                }
            }
            return segmentStart + objs.Count;
        }

        public virtual EntityType LoadFromSegment(int segment)
        {
            using (var lck = Synchronizer.Lock(segment))
            {
                using (var view = _file.CreateViewStream(Stride * segment, Stride, MemoryMappedFileAccess.Read))
                {
                    EntityType retVal;

                    if (_formatter.TryUnformatObj<EntityType>(view, out retVal))
                    {
                        view.Close();

                        return retVal;
                    }

                    view.Close();

                    return default(EntityType);
                }
            }
        }

        public virtual bool TryLoadFromSegment(int segment, out EntityType entity)
        {
            entity = default(EntityType);

            using (var lck = Synchronizer.Lock(segment))
            {
                using (var view = _file.CreateViewStream(Stride * segment, Stride, MemoryMappedFileAccess.Read))
                {
                    if (_formatter.TryUnformatObj<EntityType>(view, out entity))
                    {
                        view.Close();

                        return true;
                    }

                    view.Close();

                    return false;
                }
            }
        }

        public void Clear()
        {
            
        }

        public virtual void Dispose()
        {
            lock (_syncFlush)
                lock (_syncMap)
                    if (_file != null)
                        _file.Dispose();

            GC.Collect();
        }
    }
}
