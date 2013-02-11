/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using BESSy.Parallelization;
using BESSy.Serialization.Converters;
using BESSy.Extensions;
using BESSy.Synchronization;

namespace BESSy.Files
{
    public struct IndexPropertyPair<IdType, PropertyType>
    {
        public IndexPropertyPair(IdType id, PropertyType property)
        {
            Id = id;
            Property = property;
        }

        public IdType Id;
        public PropertyType Property;
    }

    public interface IIndexMapManager<IdType, PropertyType> : 
        IIndexedMapManager<PropertyType, IdType>
        , IEntityMapManager<IndexPropertyPair<IdType, PropertyType>>
        , ICache<PropertyType, IdType> 
        , IEnumerable<IndexPropertyPair<IdType, PropertyType>>
    {
        PropertyType LoadPropertyFromSegment(int segment);
        List<IndexingCPUGroup<IdType>> GetCPUGroupsForLookup(int strideToUse);
        IList<IdType> RidLookup(PropertyType property);
    }

    public class IndexMapManager<IdType, PropertyType>
        : IIndexMapManager<IdType, PropertyType>
    {

        public IndexMapManager
            (string name
            , IBinConverter<IdType> idConverter
            , IBinConverter<PropertyType> propertyConverter)
        {
            _name = name + ".mapping";

            _idConverter = idConverter;
            _propertyConverter = propertyConverter;

            Synchronizer = new RowSynchronizer<int>(new BinConverter32());
        }

        string _name;
        string _fileName;

        object _syncCache = new object();
        object _syncHints = new object();
        object _syncQueue = new object();
        object _syncFlush = new object();
        object _syncFile = new object();

        bool _inFlush = false;

        int _segmentsPerHint = Environment.SystemPageSize;
        int _segmentsPerBlock = 1;
        int _blockSize = 0;

        IBinConverter<IdType> _idConverter;
        IBinConverter<PropertyType> _propertyConverter;

        Queue<IDictionary<IdType, PropertyType>> _flushQueue = new Queue<IDictionary<IdType, PropertyType>>();

        Dictionary<IdType, int> _hints = new Dictionary<IdType, int>();
        Dictionary<int, IDictionary<IdType, int>> _indexCache = new Dictionary<int, IDictionary<IdType, int>>();

        MemoryMappedFile _mapFile;

        List<IndexingCPUGroup<IdType>> _lookupGroups;

        protected void InitBlockSize()
        {
            var pagingSize = Environment.SystemPageSize.Clamp(2048, 10240);

            if (Stride > pagingSize)
            {
                var div = (Stride / pagingSize);

                _blockSize = Stride * div;

                _segmentsPerBlock = div;
            }
            else
            {
                var div = (pagingSize / Stride);

                _blockSize = Stride * div;

                _segmentsPerBlock = 1;
            }
        }

        public IRowSynchronizer<int> Synchronizer { get; private set; }

        public int Stride { get; protected set; }

        public int Length { get; protected set; }

        public bool FlushQueueActive
        {
            get
            {
                if (_inFlush)
                    return true;

                if (_flushQueue.Count > 0)
                    return true;

                return false;
            }
        }

        public void OpenOrCreate(string fileName, int length, int stride)
        {
            lock (_syncFile)
            {
                using (var lck = Synchronizer.LockAll())
                {
                    Trace.TraceInformation("_syncIndex entered.");

                    Length = length;

                    var len = length.Clamp(1, int.MaxValue);

                    Stride = _idConverter.Length + _propertyConverter.Length;

                    InitBlockSize();

                    _fileName = fileName + "." + _name + ".index";

                    _mapFile = MemoryMappedFile.CreateOrOpen
                        (@"Global\" + Guid.NewGuid().ToString()
                        , Stride * len
                        , MemoryMappedFileAccess.ReadWriteExecute
                        , MemoryMappedFileOptions.None
                        , new MemoryMappedFileSecurity()
                        , HandleInheritability.Inheritable);

                    _segmentsPerHint = ((len * Stride) / 10000).Clamp(Environment.SystemPageSize, int.MaxValue);

                    _lookupGroups = GetCPUGroupsForLookup(Stride);

                    Trace.TraceInformation("_syncIndex exited.");
                }
            }

            ClearCache();
        }

        #region Flush

        public void FlushNew(IDictionary<IdType, PropertyType> items)
        {
            lock (_syncQueue)
            {
                _flushQueue.Clear();
                _flushQueue.Enqueue(items);

                ThreadPool.QueueUserWorkItem(new WaitCallback(BeginFlushNew));
            }
        }

        protected virtual void BeginFlushNew(object state)
        {
            IDictionary<IdType, PropertyType> items = null;

            lock (_syncQueue)
                if (_flushQueue.Count > 0)
                    items = _flushQueue.Dequeue();
                else
                    return;

            Monitor.Enter(_syncFlush);

            try
            {
                Trace.TraceInformation("_syncFlush entered.");

                _inFlush = true;

#if DEBUG
                if (items == null || items.Count < 1)
                    throw new ArgumentNullException("Cached items list to flush was null.");
#endif

                var newIndexMap = MemoryMappedFile.CreateOrOpen
                    (@"Global\" + Guid.NewGuid().ToString()
                    , Stride * items.Count
                    , MemoryMappedFileAccess.ReadWriteExecute
                    , MemoryMappedFileOptions.None
                    , new MemoryMappedFileSecurity()
                    , HandleInheritability.Inheritable);

                var seg = 0;

                using (var newIndexView = newIndexMap.CreateViewStream
                    (seg
                    , items.Count * Stride
                    , MemoryMappedFileAccess.ReadWriteExecute))
                {
                    foreach (var item in items)
                    {
                        newIndexView.Write(_idConverter.ToBytes(item.Key), 0, _idConverter.Length);
                        newIndexView.Write(_propertyConverter.ToBytes(item.Value), 0, _propertyConverter.Length);

                        if (seg % this._segmentsPerHint == 0 && !_hints.ContainsKey(item.Key))
                            lock (_syncHints)
                                _hints.Add(item.Key, seg);

                        seg++;
                    }

                    var max = seg;

                    if (seg > Length)
                        Length = seg;

                    newIndexView.Flush();
                    newIndexView.Close();
                }

                lock (_syncFile)
                {
                    Trace.TraceInformation("_syncIndex entered.");

                    _mapFile.Dispose();

                    _mapFile = newIndexMap;

                    _lookupGroups = GetCPUGroupsForLookup(Stride);

                    Trace.TraceInformation("_syncIndex exited.");
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                throw;
            }
            finally
            {
                Monitor.Exit(_syncFlush);

                Trace.TraceInformation("_syncFlush exited.");

                _inFlush = false;

                GC.Collect();

                InvokeOnFlushCompleted(items);
            }

            //Launch a new flush call in case we were blocking another thread with updated information.
            int queueCount = 0;
            lock (_syncQueue)
                queueCount = _flushQueue.Count;

            if (queueCount > 0)
                BeginFlush(state);
        }

        public void Flush(IDictionary<IdType, PropertyType> items)
        {
            if (Length <= 0 && !FlushQueueActive)
            {
                this.FlushNew(items);

                return;
            }
            else
            {
                var sw = new Stopwatch();
                sw.Start();

                while (Length == 0 && FlushQueueActive && sw.Elapsed.Milliseconds < 50000)
                    Thread.Sleep(100);

                sw.Stop();
            }

            lock (_flushQueue)
                _flushQueue.Enqueue(items);

            ThreadPool.QueueUserWorkItem(new WaitCallback(BeginFlush));
        }

        protected virtual void BeginFlush(object state)
        {
            IDictionary<IdType, PropertyType> items = null;

            lock (_syncQueue)
                if (_flushQueue.Count > 0)
                    items = _flushQueue.Dequeue();

            while (items != null && items.Count > 0)
            {
                var hintSync = new object();
                var newHints = new Dictionary<IdType, int>();
                var newGroups = GetCPUGroupsForFlush(items);
                var length = newGroups.Max(g => g.Inserts.Max(i => i.EndNewSegment)) + 1;
                var newIndexMap = MemoryMappedFile.CreateOrOpen(@"Global\" + Guid.NewGuid().ToString(), Stride * length, MemoryMappedFileAccess.ReadWriteExecute);


                if (!Monitor.TryEnter(_syncFlush, 500))
                    return;

                try
                {
                    Trace.TraceInformation("_syncFlush entered.");

                    _inFlush = true;

                    Parallel.ForEach(newGroups, delegate(IndexingCPUGroup<IdType> group)
                    {
                        try
                        {
                            int segment = group.StartSegment;
                            int newSegment = segment;

                            using (var rows = Synchronizer.Lock(new Range<int>(group.StartSegment, group.EndSegment)))
                            {
                                using (var indexView = _mapFile.CreateViewStream
                                    (group.StartSegment * Stride
                                    , ((group.EndSegment - group.StartSegment) + 1) * Stride
                                    , MemoryMappedFileAccess.Read))
                                {
                                    foreach (var subset in group.Inserts)
                                    {
                                        using (var newIndexView = newIndexMap.CreateViewStream
                                            (subset.StartNewSegment * Stride
                                            , ((subset.EndNewSegment - subset.StartNewSegment) + 1) * Stride
                                            , MemoryMappedFileAccess.ReadWriteExecute))
                                        {
                                            byte[] idReadBuffer = new byte[_idConverter.Length];
                                            byte[] propReadBuffer = new byte[_propertyConverter.Length];

                                            //read id
                                            var read = indexView.Read(idReadBuffer, 0, idReadBuffer.Length);
                                            var id = _idConverter.FromBytes(idReadBuffer);
                                            var nextId = default(IdType);

                                            //read property
                                            read = indexView.Read(propReadBuffer, 0, propReadBuffer.Length);
                                            var prop = _propertyConverter.FromBytes(propReadBuffer);

                                            byte[] copyBuffer = new byte[Stride];

                                            List<IdType> toWriteFromCache = new List<IdType>();
                                            List<IdType> updates = new List<IdType>();

                                            while (newSegment <= subset.EndNewSegment)
                                            {
                                                if (segment == group.StartSegment)
                                                {
                                                    toWriteFromCache = subset.IdsToAdd.Where
                                                        (a => _idConverter.Compare(a, id) < 0
                                                        && !updates.Contains(a)).ToList();

                                                    if (toWriteFromCache.Count > 0)
                                                    {
                                                        lock (hintSync)
                                                            WriteInsertHints(toWriteFromCache, newHints, newSegment);

                                                        newSegment = WriteInserts(toWriteFromCache, newSegment, idReadBuffer, propReadBuffer, subset, newIndexView, items);
                                                    }
                                                }

                                                if (segment <= group.EndSegment)
                                                {
                                                    if (items.ContainsKey(id))
                                                    {
                                                        //update
                                                        WriteUpdate(id, items[id], idReadBuffer, propReadBuffer, indexView, newIndexView);

                                                        updates.Add(id);

                                                        lock (hintSync)
                                                            if (newSegment % _segmentsPerHint == 0 && !newHints.ContainsKey(id))
                                                                newHints.Add(id, newSegment);

                                                        segment++;
                                                        newSegment++;
                                                    }
                                                    else
                                                    {
                                                        //copy
                                                        WriteOld(id, idReadBuffer, propReadBuffer, indexView, newIndexView);

                                                        lock (hintSync)
                                                            if (newSegment % _segmentsPerHint == 0 && !newHints.ContainsKey(id))
                                                                newHints.Add(id, newSegment);

                                                        segment++;
                                                        newSegment++;
                                                    }

                                                    if (segment <= group.EndSegment)
                                                    {
                                                        //read nextId
                                                        read = indexView.Read(idReadBuffer, 0, idReadBuffer.Length);
                                                        //skip property
                                                        indexView.Position += _propertyConverter.Length;

                                                        nextId = _idConverter.FromBytes(idReadBuffer);

                                                        //read nextId until valid Id is found.
                                                        while (read > 0 && _idConverter.Compare(nextId, default(IdType)) == 0 && segment <= group.EndSegment)
                                                        {
                                                            read = indexView.Read(idReadBuffer, 0, idReadBuffer.Length);
                                                            //skip property
                                                            indexView.Position += _propertyConverter.Length;

                                                            nextId = _idConverter.FromBytes(idReadBuffer);

                                                            segment++;
                                                            newSegment++;
                                                        }

                                                        //insert between.
                                                        if (subset.IdsToAdd.Count - updates.Count > 0 &&
                                                            _idConverter.Compare(nextId, default(IdType)) != 0)
                                                        {
                                                            toWriteFromCache = subset.IdsToAdd.Where
                                                                (a => _idConverter.Compare(a, id) > 0
                                                                    && (_idConverter.Compare(a, nextId) < 0)
                                                                    && !updates.Contains(a)).ToList();

                                                            if (toWriteFromCache.Count > 0)
                                                            {
                                                                lock (hintSync)
                                                                    WriteInsertHints(toWriteFromCache, newHints, newSegment);

                                                                newSegment = WriteInserts(toWriteFromCache, newSegment, idReadBuffer, propReadBuffer, subset, newIndexView, items);
                                                            }
                                                        }
                                                    }

                                                    id = nextId;
                                                }
                                                else
                                                {
                                                    if (subset.IdsToAdd.Count > 0)
                                                    {
                                                        toWriteFromCache = subset.IdsToAdd.Where
                                                            (a => _idConverter.Compare(a, id) > 0
                                                           && !updates.Contains(a)).ToList();
                                                    }

                                                    if (toWriteFromCache.Count > 0)
                                                    {
                                                        lock (hintSync)
                                                            WriteInsertHints(toWriteFromCache, newHints, newSegment);

                                                        newSegment = WriteInserts(toWriteFromCache, newSegment, idReadBuffer, propReadBuffer, subset, newIndexView, items);
                                                    }

                                                    //we're done with this subset
                                                    break;
                                                }
                                            }

                                            newIndexView.Flush();
                                            newIndexView.Close();
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Trace.TraceError(ex.ToString());
                            throw;
                        }
                    });

                    using (var lck = Synchronizer.LockAll())
                    {
                        _mapFile.Dispose();
                        _mapFile = newIndexMap;

                        ClearCache();

                        Length = length;

                        _lookupGroups = GetCPUGroupsForLookup(Stride);

                        lock (_syncHints)
                            _hints = newHints;
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.ToString());
                    throw;
                }
                finally
                {
                    _inFlush = false;

                    Monitor.Exit(_syncFlush);

                    Trace.TraceInformation("_syncFlush exited.");

                    GC.Collect();

                    //alert the calling repository that these items can 
                    //be released from the staging cache.
                    InvokeOnFlushCompleted(items);
                }

                lock (_syncQueue)
                    if (_flushQueue.Count > 0)
                        items = _flushQueue.Dequeue();
                    else
                        items = null;
            }
        }

        protected virtual void WriteInsertHints(List<IdType> toWriteFromCache, Dictionary<IdType,int> newHints, int newSegment)
        {
            int tmpSeg = newSegment;
                
            toWriteFromCache.ForEach(delegate(IdType i)
            {
                if (newSegment % _segmentsPerHint == 0 && !newHints.ContainsKey(i))
                    newHints.Add(i, newSegment);

                tmpSeg++;
            });
        }

        protected virtual void WriteOld(IdType id, byte[] idReadBuffer, byte[] propReadBuffer, Stream indexView, Stream newIndexView)
        {
            //read old index.
            indexView.Read(idReadBuffer, 0, _idConverter.Length);
            indexView.Read(propReadBuffer, 0, _propertyConverter.Length);

            //write new Index
            newIndexView.Write(idReadBuffer, 0, idReadBuffer.Length);
            newIndexView.Write(propReadBuffer, 0, propReadBuffer.Length);
        }

        protected virtual void WriteUpdate(IdType id
            , PropertyType item
            , byte[] idReadBuffer
            , byte[] propBuffer
            , Stream indexView
            , Stream newIndexView)
        {
            idReadBuffer = _idConverter.ToBytes(id);
            propBuffer = _propertyConverter.ToBytes(item);

            //write new Index
            newIndexView.Write(idReadBuffer, 0, idReadBuffer.Length);
            newIndexView.Write(propBuffer, 0, propBuffer.Length);

            indexView.Position += Stride;
        }

        protected virtual int WriteInserts(List<IdType> toWriteFromCache
            , int newSegment
            , byte[] idReadBuffer
            , byte[] propReadBuffer
            , IndexingInsertSubset<IdType> subset
            , Stream newIndexView
            , IDictionary<IdType, PropertyType> items)
        {
            toWriteFromCache.ForEach(delegate(IdType id)
            {
#if DEBUG
                if (_idConverter.Compare(id, default(IdType)) == 0)
                    return;

                if (newIndexView.Length == newIndexView.Position)
                    throw new InvalidOperationException("View stream capacity exceeded.");

                if (newSegment > subset.EndNewSegment)
                    throw new InvalidOperationException("View stream capacity exceeded.");
#endif

                WriteInsert(id, items[id], newSegment, idReadBuffer, propReadBuffer, newIndexView);

                newSegment++;
            });

            toWriteFromCache.Clear();

            return newSegment;
        }


        protected virtual void WriteInsert(IdType id
            , PropertyType property
            , int newSegment
            , byte[] idReadBuffer
            , byte[] propReadBuffer
            , Stream newIndexView)
        {
            idReadBuffer = _idConverter.ToBytes(id);
            propReadBuffer = _propertyConverter.ToBytes(property);

            newIndexView.Write(idReadBuffer, 0, idReadBuffer.Length);
            newIndexView.Write(propReadBuffer, 0, propReadBuffer.Length);
        }
    

        protected virtual List<IndexingCPUGroup<IdType>> GetCPUGroupsForFlush(IDictionary<IdType, PropertyType> items)
        {
            var newGroups = new List<IndexingCPUGroup<IdType>>();

            if (Length < _blockSize)
            {
                newGroups.Add(new IndexingCPUGroup<IdType>()
                {
                    StartSegment = 0,
                    EndSegment = Length - 1,

                    Inserts = new List<IndexingInsertSubset<IdType>>()
                    {
                        new IndexingInsertSubset<IdType>
                        {
                            StartNewSegment = 0,
                            EndNewSegment = Length + items.Count - 1,
                            IdsToAdd = items.Keys.ToList()
                        }
                    }
                });

                return newGroups;
            }

            var paras = TaskGrouping.GetSegmentedTaskGroups(Length, Stride);

            List<int> toRemove = new List<int>();

            //find the former segment with a valid Id.
            for (var i = 0; i < paras.Count; i++)
            {
                while (_idConverter.Compare(LookupFromSegment(paras[i]), default(IdType)) == 0)
                    paras[i] = paras[i] - 1;

                if (paras[i] <= 0 || (i > 0 && paras[i] <= paras[i - 1]))
                    toRemove.Add(i);
            }

            //remove invalid parallel operations.
            if (toRemove.Count > 0)
                foreach (var r in toRemove.OrderByDescending(d => d))
                    paras.RemoveAt(r);

            var groups = GetSegments(paras);

            newGroups = TaskGrouping.GetCPUGroupsFor(items, groups, _idConverter, Stride, Stride);

            return newGroups;
        }

        public List<IndexingCPUGroup<IdType>> GetCPUGroupsForLookup(int strideToUse)
        {
            var newGroups = new List<IndexingCPUGroup<IdType>>();

            if (Length < _blockSize)
            {
                newGroups.Add(new IndexingCPUGroup<IdType>()
                {
                    StartSegment = 0,
                    EndSegment = Length - 1,
                    Inserts = new List<IndexingInsertSubset<IdType>>()
                    {
                        new IndexingInsertSubset<IdType>
                        {
                            StartNewSegment = 0,
                            EndNewSegment = Length - 1,
                            IdsToAdd = new List<IdType>()
                        }
                    }
                });

                return newGroups;
            }

            var paras = TaskGrouping.GetSegmentedTaskGroups(Length, strideToUse);

            List<int> toRemove = new List<int>();

            //find the former segment with a valid Id.
            for (var i = 0; i < paras.Count; i++)
            {
                while (_idConverter.Compare(LookupFromSegment(paras[i]), default(IdType)) == 0)
                    paras[i] = paras[i] - 1;

                if (paras[i] <= 0 || (i > 0 && paras[i] <= paras[i - 1]))
                    toRemove.Add(i);
            }

            //remove invalid parallel operations.
            if (toRemove.Count > 0)
                foreach (var r in toRemove.OrderByDescending(d => d))
                    paras.RemoveAt(r);

            var groups = GetSegments(paras);

            newGroups = TaskGrouping.GetCPUGroupsFor(groups);

            return newGroups;
        }

        protected IDictionary<int, IdType> GetSegments(List<int> segments)
        {
            var indicies = new Dictionary<int, IdType>();

            //TODO: optimize later.
            segments.ForEach(delegate(int segment)
            {
                if (segment > Length)
                    return;

                if (segment < 0)
                    return;

                var offset = segment * Stride;

                using (var rows = Synchronizer.Lock(segment))
                {
                    using (var view = _mapFile.CreateViewStream
                        (offset, Stride, MemoryMappedFileAccess.Read))
                    {
                        byte[] idBuf = new byte[_idConverter.Length];

                        var read = view.Read(idBuf, 0, idBuf.Length);
                        var rid = _idConverter.FromBytes(idBuf);

                        indicies.Add(segment, rid);
                    }
                }
            });

            return indicies;
        }

        #endregion

        #region ICache Members

        public bool IsNew(IdType id)
        {
            return false;
        }

        public bool Contains(IdType id)
        {
            return _indexCache.Any(c => c.Value.ContainsKey(id));
        }

        public PropertyType GetFromCache(IdType id)
        {
            foreach (var i in _indexCache)
                if (i.Value.ContainsKey(id))
                    return LoadPropertyFromSegment(i.Value[id]);

            return default(PropertyType);
        }

        public void CacheItem(IdType id)
        {
            //TODO: see if I should implement cache requests.
        }

        public void Detach(IdType id)
        {
            if (_indexCache.Count >= TaskGrouping.ReadLimit)
                lock (_syncCache)
                    foreach (var k in _indexCache.Keys)
                        _indexCache.Remove(k);
        }

        public void ClearCache()
        {
            if (_indexCache != null)
            {
                lock (_syncCache)
                    _indexCache.Clear();
            }
        }

        public void Sweep()
        {
            if (_indexCache.Count >= TaskGrouping.ReadLimit)
                lock (_syncCache)
                    foreach (var k in _indexCache.Keys.Skip(TaskGrouping.ReadLimit))
                        _indexCache.Remove(k);
        }

        #endregion

        public IList<IdType> RidLookup(PropertyType property)
        {
            object sync = new object();

            var ids = new List<IdType>();

            Trace.TraceInformation("_syncIndex entered.");

            Parallel.ForEach(_lookupGroups, delegate(IndexingCPUGroup<IdType> group)
            {
                try
                {
                    using (var lck = Synchronizer.Lock(new Range<int>(group.StartSegment, group.EndSegment)))
                    {
                        using (var view = _mapFile.CreateViewStream
                            (group.StartSegment * Stride,
                            (group.EndSegment * Stride) - group.StartSegment * Stride,
                            MemoryMappedFileAccess.Read))
                        {
                            byte[] idReadBuffer = new byte[_idConverter.Length];
                            byte[] propReadBuffer = new byte[_propertyConverter.Length];

                            //read id
                            var read = view.Read(idReadBuffer, 0, idReadBuffer.Length);
                            var id = _idConverter.FromBytes(idReadBuffer);

                            //read property
                            read = view.Read(propReadBuffer, 0, propReadBuffer.Length);
                            var prop = _propertyConverter.FromBytes(propReadBuffer);

                            while (read > 0)
                            {
                                if (_propertyConverter.Compare(prop, property) == 0)
                                    lock (sync)
                                        ids.Add(id);

                                //read id
                                read = view.Read(idReadBuffer, 0, idReadBuffer.Length);
                                id = _idConverter.FromBytes(idReadBuffer);

                                //read property
                                read = view.Read(propReadBuffer, 0, propReadBuffer.Length);
                                prop = _propertyConverter.FromBytes(propReadBuffer);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.ToString());
                    throw ex;
                }
            });

            Trace.TraceInformation("_syncIndex exited.");

            return ids;
        }

        public PropertyType Load(IdType id)
        {
            if (Length == 0)
                return default(PropertyType);

            int seg = -1;

            foreach (var i in _indexCache)
                if (i.Value.ContainsKey(id))
                    seg = i.Value[id];

            if (seg > -1)
                return LoadPropertyFromSegment(seg);

            var segStart = GuessBlockStart(id);

            seg = IndexSeek(segStart, id);

            if (seg > -1)
                return LoadPropertyFromSegment(seg);

            return default(PropertyType);
        }

        protected int GuessBlockStart(IdType id)
        {
            lock (_hints)
            {
                if (_hints.ContainsKey(id))
                    return _hints[id];


                var first = _hints.AsParallel().LastOrDefault(f => _idConverter.Compare(f.Key, id) <= 0);

                return first.Value;
            }
        }

        public virtual PropertyType LoadPropertyFromSegment(int segment)
        {
#if DEBUG
            if (segment < 0 || segment > Length)
                throw new ArgumentException("segment out of bounds of the file.", "segment");
#endif

            using (var lck = Synchronizer.Lock(segment))
            {
                using (var view = _mapFile.CreateViewStream(
                    (segment * Stride) + _idConverter.Length
                    , Stride - _idConverter.Length
                    , MemoryMappedFileAccess.Read))
                {
                    byte[] propBuf = new byte[_propertyConverter.Length];

                    var read = view.Read(propBuf, 0, propBuf.Length);

                    var prop = _propertyConverter.FromBytes(propBuf);

                    return prop;
                }
            }
        }

        public virtual IndexPropertyPair<IdType, PropertyType> LoadFromSegment(int segment)
        {
#if DEBUG
            if (segment < 0 || segment > Length)
                throw new ArgumentException("segment out of bounds of the file.", "segment");
#endif

            using (var lck = Synchronizer.Lock(segment))
            {
                using (var view = _mapFile.CreateViewStream(
                    (segment * Stride)
                    , Stride
                    , MemoryMappedFileAccess.Read))
                {
                    byte[] idBuf = new byte[_idConverter.Length];
                    byte[] propBuf = new byte[_propertyConverter.Length];

                    var read = view.Read(idBuf, 0, idBuf.Length);
                    read += view.Read(propBuf, 0, propBuf.Length);

                    var index = new IndexPropertyPair<IdType, PropertyType>
                        (_idConverter.FromBytes(idBuf), _propertyConverter.FromBytes(propBuf));

                    return index;
                }
            }
        }

        protected int IndexSeek(int segmentStart, IdType id)
        {
            int segmentFound = -1;

            Parallel.For(0, 2, delegate(int segMod)
            {
                segMod = segMod - 1;

                var segStart = segMod * _segmentsPerBlock;

                if (segStart > Length)
                    return;

                if (segStart < 0)
                    return;

                var count = (Length - segMod);
                var size = _blockSize.Clamp(Stride, count * Stride);

                using (var lck = Synchronizer.Lock(new Range<int>(segStart, segStart + count)))
                {
                    using (var view = _mapFile.CreateViewStream
                        (segStart * Stride
                        , size, MemoryMappedFileAccess.Read))
                    {
                        byte[] idBuf = new byte[_idConverter.Length];

                        var read = view.Read(idBuf, 0, idBuf.Length);
                        var rid = _idConverter.FromBytes(idBuf);

                        int seg = 0;

                        while (read > 0)
                        {
                            if (segmentFound > -1)
                                break;

                            if (_idConverter.Compare(rid, id) == 0)
                            {
                                segmentFound = seg;

                                break;
                            }

                            view.Position += _propertyConverter.Length;

                            read = view.Read(idBuf, 0, idBuf.Length);
                            rid = _idConverter.FromBytes(idBuf);

                            seg++;
                        }
                    }
                }
            });

            if (segmentFound > -1)
            {
                System.Threading.ThreadPool.QueueUserWorkItem
                    (new WaitCallback(StartCachingBlock), segmentFound);

                return segmentFound;
            }
            else
                return IndexScan(id);
        }

        protected int IndexScan(IdType id)
        {
            var segments = (Length / _segmentsPerBlock) + 1;
            var segmentFound = -1;
            CancellationTokenSource source = new CancellationTokenSource();

            try
            {
                Parallel.ForEach(_lookupGroups, new ParallelOptions() { CancellationToken = source.Token }, delegate(IndexingCPUGroup<IdType> group)
                {
                    try
                    {
                        using (var lck = Synchronizer.Lock(new Range<int>(group.StartSegment, group.EndSegment)))
                        {
                            using (var view = _mapFile.CreateViewStream
                                (group.StartSegment * Stride
                                , group.EndSegment * Stride - group.StartSegment * Stride
                                , MemoryMappedFileAccess.Read))
                            {
                                if (segmentFound > -1)
                                    return;

                                byte[] idBuf = new byte[_idConverter.Length];
                                byte[] propBuf = new byte[_propertyConverter.Length];

                                var read = view.Read(idBuf, 0, idBuf.Length);
                                var rid = _idConverter.FromBytes(idBuf);
                                var seg = group.StartSegment;

                                while (read > 0)
                                {
                                    if (segmentFound > 0)
                                        break;

                                    if (_idConverter.Compare(rid, id) == 0)
                                    {
                                        segmentFound = seg;

                                        if (!_hints.ContainsKey(id))
                                            lock (_syncHints)
                                                _hints.Add(rid, segmentFound);

                                        System.Threading.ThreadPool.QueueUserWorkItem
                                            (new WaitCallback(StartCachingBlock), segmentFound);

                                        source.Cancel();

                                        break;
                                    }

                                    view.Position += _propertyConverter.Length;

                                    read = view.Read(idBuf, 0, idBuf.Length);
                                    rid = _idConverter.FromBytes(idBuf);

                                    seg++;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        //.NET does not throw up this exception to the calling thread, 
                        // so we need to log this before the parallel operation exits.
                        Trace.TraceError(ex.ToString());
                        throw;
                    }
                });
            }
            //This is expected behavior, not sure why .NET throws an exception.
            catch (OperationCanceledException) { }

            return segmentFound;
        }

        public IdType LookupFromSegment(int segment)
        {
#if DEBUG
            if (segment < 0 || segment > Length)
                throw new ArgumentException("segment out of bounds of the file.", "segment");
#endif

            using (var lck = Synchronizer.Lock(segment))
            {
                using (var view = _mapFile.CreateViewStream
                    (segment * Stride
                    , Stride
                    , MemoryMappedFileAccess.Read))
                {
                    byte[] idBuf = new byte[_idConverter.Length];

                    var read = view.Read(idBuf, 0, idBuf.Length);

                    var rid = _idConverter.FromBytes(idBuf);

                    return rid;
                }
            }
        }

        public bool Save(PropertyType obj, IdType id)
        {
            if (Length == 0)
                return false;

            int seg = -1;

            foreach (var i in _indexCache)
                if (i.Value.ContainsKey(id))
                    seg = i.Value[id];

            if (seg > -1)
                return Save(obj, id, seg);

            var segStart = GuessBlockStart(id);

            seg = IndexSeek(segStart, id);

            if (seg > -1)
                return Save(obj, id, seg);

            return false;
        }

        public bool Save(PropertyType obj, IdType id, int segment)
        {
            if (segment < 0 || _idConverter.Compare(id, default(IdType)) == 0)
                return false;

            SaveCatalogIndex(obj, id, segment);

            return true;
        }

        public bool SaveToFile(IndexPropertyPair<IdType, PropertyType> index, int segment)
        {
            SaveCatalogIndex(index.Property, index.Id, segment);

            return true;
        }

        protected void SaveCatalogIndex(PropertyType obj, IdType id, int segment)
        {
            lock (_syncFlush)
            {
                Trace.TraceInformation("_syncFlush entered.");

                using (var lck = Synchronizer.Lock(segment))
                {
                    using (var view = _mapFile.CreateViewStream
                        (segment * Stride, Stride
                        , MemoryMappedFileAccess.ReadWriteExecute))
                    {
                        view.Write(_idConverter.ToBytes(id), 0, _idConverter.Length);
                        view.Write(_propertyConverter.ToBytes(obj), 0, _propertyConverter.Length);

                        view.Flush();
                    }

                    if (segment % this._segmentsPerHint == 0 && !_hints.ContainsKey(id))
                        lock (_syncHints)
                            _hints[id] = segment;

                    if (segment > Length)
                        Length = segment;
                }

                Trace.TraceInformation("_syncFlush exited.");
            }
        }

        #region CacheBlock

        protected void StartCachingBlock(object state)
        {
            int segmentStart = (int)state;

            CacheBlock(segmentStart);
        }

        protected void CacheBlock(int segmentStart)
        {
            if (_mapFile.SafeMemoryMappedFileHandle.IsClosed)
                return;

            lock (_syncFile)
            {
                var segmentKey = segmentStart / _segmentsPerBlock;

                if (_indexCache.ContainsKey(segmentKey))
                    return;

                var cache = new Dictionary<IdType, int>();

                var count = (Length - segmentKey);

                var size = _blockSize.Clamp(Stride, count * Stride);

                if (size < Stride)
                    return;

                using (var lck = Synchronizer.Lock(new Range<int>(segmentKey, segmentKey + count)))
                {
                    using (var view = _mapFile.CreateViewStream
                        (segmentKey * Stride
                        , size, MemoryMappedFileAccess.Read))
                    {
                        byte[] tmp = new byte[_idConverter.Length];

                        var read = view.Read(tmp, 0, tmp.Length);
                        var rid = _idConverter.FromBytes(tmp);
                        var seg = segmentKey;

                        while (read > 0)
                        {
                            if (_idConverter.Compare(rid, (default(IdType))) != 0)
                                if (!cache.ContainsKey(rid))
                                    cache.Add(rid, seg);

                            view.Position += _propertyConverter.Length;

                            read = view.Read(tmp, 0, tmp.Length);
                            rid = _idConverter.FromBytes(tmp);
                            seg++;
                        }
                    }
                }

                lock (_syncCache)
                {
                    if (!_indexCache.ContainsKey(segmentKey))
                        _indexCache.Add(segmentKey, cache);
                }
            }
        }

        #endregion

        public int SaveBatchToFile(IDictionary<IdType, PropertyType> items, int segmentStart)
        {
            if (items == null)
                return segmentStart;

            lock (_syncFlush)
            {
                try
                {
                    Trace.TraceInformation("_syncFlush entered.");

                    _inFlush = true;

                    var seg = segmentStart;

                    var key = items.First().Key;

                    if (!_hints.ContainsKey(key))
                        lock (_syncHints)
                            _hints.Add(key, seg);

                    using (var lck = Synchronizer.Lock(new Range<int>(seg, seg + items.Count)))
                    {
                        using (var view = _mapFile.CreateViewStream
                            (seg * Stride, items.Count * Stride
                            , MemoryMappedFileAccess.ReadWriteExecute))
                        {
                            foreach (var item in items)
                            {
                                view.Write(_idConverter.ToBytes(item.Key), 0, _idConverter.Length);
                                view.Write(_propertyConverter.ToBytes(item.Value), 0, _propertyConverter.Length);

                                seg++;
                            }

                            view.Flush();
                        }

                        return seg;
                    }
                }
                finally
                {
                    _inFlush = false;

                    Trace.TraceInformation("_syncFlush exited.");
                }
            }
        }

        public virtual bool TryLoadFromSegment(int segment, out IndexPropertyPair<IdType, PropertyType> index)
        {
            index = new IndexPropertyPair<IdType, PropertyType>();

            try
            {
                index = LoadFromSegment(segment);

                return true;
            }
            catch (JsonSerializationException) { return false; }
            catch (SystemException) { return false; }
        }

        public int SaveBatchToFile(IList<IndexPropertyPair<IdType, PropertyType>> items, int segmentStart)
        {
            if (items.IsNullOrEmpty())
                return 0;

            lock (_syncFlush)
            {
                Trace.TraceInformation("_syncFlush entered.");

                using (var lck = Synchronizer.Lock(new Range<int>(segmentStart, segmentStart + items.Count)))
                {
                    using (var view = _mapFile.CreateViewStream(Stride * segmentStart, items.Count * Stride, MemoryMappedFileAccess.ReadWriteExecute))
                    {
                        foreach (var item in items)
                        {
                            view.Write(_idConverter.ToBytes(item.Id), 0, _idConverter.Length);
                            view.Write(_propertyConverter.ToBytes(item.Property), 0, _propertyConverter.Length);
                        }

                        view.Flush();
                        view.Close();
                    }
                }

                Trace.TraceInformation("_syncFlush exited.");
            }

            return segmentStart + items.Count;
        }

        public IEnumerator<IndexPropertyPair<IdType, PropertyType>> GetEnumerator()
        {
            return new IndexEnumerator<IdType, PropertyType>(this, _idConverter);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        protected void InvokeOnFlushCompleted(IDictionary<IdType, PropertyType> items)
        {
            if (OnFlushCompleted != null)
                OnFlushCompleted(items);
        }

        public event FlushCompleted<PropertyType, IdType> OnFlushCompleted;

        public void Dispose()
        {
            while (FlushQueueActive)
                Thread.Sleep(100);

            lock (_syncFile)
                if (_mapFile != null)
                    _mapFile.Dispose();
        }
    }
}
