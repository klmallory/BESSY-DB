/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
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
        public MapManager(ISafeFormatter formatter)
        {
            _formatter = formatter;

            Synchronizer = new RowSynchronizer<int>(new BinConverter32());
        }

        protected object _syncFlush = new object();
        protected object _syncMap = new object();

        protected bool _inFlush = false;
        protected ISafeFormatter _formatter { get; set; }
        protected MemoryMappedFile _file { get; set; }

        protected string _handle;
        protected string _fileName;

        protected int _currentEnumeratorSegment = -1;

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

                var handle = @"Global\" + Guid.NewGuid().ToString();
                
                _file = MemoryMappedFile.CreateOrOpen
                    (handle
                    , Stride * len
                    , MemoryMappedFileAccess.ReadWriteExecute
                    , MemoryMappedFileOptions.None
                    , new MemoryMappedFileSecurity()
                    , HandleInheritability.Inheritable);

                var fi = new FileInfo(fileName);

                _fileName = fileName;

                _currentEnumeratorSegment = 0;

                Length = length;
            }
        }

        public virtual bool SaveToFile(EntityType obj, int segment)
        {
            byte[] buffer;

            if (!_formatter.TryFormatObj(obj, out buffer))
                buffer = new byte[Stride];

            if (buffer.Length > Stride)
                throw new InvalidDataException("this object is too large for this file format.");

            Array.Resize(ref buffer, Stride);

            using (var lck = Synchronizer.Lock(segment))
            {
                using (var view = _file.CreateViewStream(Stride * segment, Stride, MemoryMappedFileAccess.ReadWriteExecute))
                {
                    view.Write(buffer, 0, buffer.Length);

                    view.Flush();
                    view.Close();
                }
            }

            return true;
        }

        public virtual int SaveBatchToFile(IList<EntityType> objs, int segmentStart)
        {
            if (objs.IsNullOrEmpty())
                return 0;

            byte[] buffer;

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
                            if (!_formatter.TryFormatObj(obj, out buffer))
                                buffer = new byte[Stride];

                            Array.Resize(ref buffer, Stride);

#if DEBUG
                            if (buffer.Length > Stride)
                                throw new InvalidDataException("this object is too large for this file format.");
#endif
                            view.Write(buffer, 0, buffer.Length);
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

        public virtual void Dispose()
        {
            if (_file != null)
            {
                if (_file.SafeMemoryMappedFileHandle != null)
                    _file.SafeMemoryMappedFileHandle.Close();

                _file.Dispose();
            };

            GC.Collect();
        }
    }
}
