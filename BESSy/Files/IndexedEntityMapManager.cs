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
using BESSy.Extensions;
using BESSy.Parallelization;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using Newtonsoft.Json;

namespace BESSy.Files
{
    delegate void CacheBlockAsync<I>(int startSegment, int segment, I id);

    public delegate void FlushCompleted<T, I>(IDictionary<I, T> itemsFlushed);

    public interface IIndexedEntityMapManager<EntityType, IdType> 
        : IIndexedMapManager<EntityType, IdType>
        , IEntityMapManager<EntityType>
        , ICache<int, IdType>
        , IEnumerable<EntityType>
    {
        //IDictionary<int, IdType> Flush<IdType>(IDictionary<IdType, EntityType> items, int startSegment);
        EntityEnumerator<EntityType, IdType> GetEntityEnumerator();
    }

    public class IndexedEntityMapManager<EntityType, IdType> 
        : MapManager<EntityType>
        , IIndexedEntityMapManager<EntityType, IdType>  
    {
        public IndexedEntityMapManager(IBinConverter<IdType> idConverter, ISafeFormatter formatter)
            : base(formatter)
        {
            _idConverter = idConverter;

            _segConverter = new BinConverter32();

            _segmentStride = idConverter.Length + _segConverter.Length;

            InitBlockSize();
        }

        protected object _syncCache = new object();
        protected object _syncHints = new object();
        protected object _syncIndex = new object();
        protected object _syncQueue = new object();
        protected object _syncFlush = new object();

        protected int _segmentStride = 0;
        protected int _segmentsPerHint = Environment.SystemPageSize;
        protected int _segmentsPerBlock = 1;
        protected int _blockSize = 0;

        protected IBinConverter<IdType> _idConverter;
        protected IBinConverter<int> _segConverter;

        protected Queue<IDictionary<IdType, EntityType>> _flushQueue = new Queue<IDictionary<IdType, EntityType>>();
        protected Dictionary<IdType, int> _hints = new Dictionary<IdType, int>();
        protected IDictionary<int, IDictionary<IdType, int>> _indexCache = new Dictionary<int, IDictionary<IdType, int>>();
        protected MemoryMappedFile _index;

        protected List<IndexingCPUGroup<IdType>> _lookupGroups;

        protected List<IndexingCPUGroup<IdType>> GetCPUGroupsForLookup()
        {
            var newGroups = new List<IndexingCPUGroup<IdType>>();

            if (Length < _blockSize)
            {
                newGroups.Add(new IndexingCPUGroup<IdType>()
                {
                    StartSegment = 0,
                    EndSegment = Length - 1
                });

                return newGroups;
            }

            var paras = TaskGrouping.GetSegmentedTaskGroups(Length, _segmentStride);

            List<int> toRemove = new List<int>();

            //find the former segment with a valid Id.
            for (var i = 0; i < paras.Count; i++)
            {
                while (paras[i] >= 0 && _idConverter.Compare(LookupFromSegment(paras[i]), default(IdType)) == 0)
                    paras[i] = paras[i] - 1;

                if (paras[i] < 0 || (i > 0 && paras[i] <= paras[i - 1]))
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

        protected void InitBlockSize()
        {
            var pagingSize = Environment.SystemPageSize.Clamp(2048, 10240);

            if (_segmentStride > pagingSize)
            {
                var div = (_segmentStride / pagingSize);

                _blockSize = _segmentStride * div;

                _segmentsPerBlock = div;
            }
            else
            {
                var div = (pagingSize / _segmentStride);

                _blockSize = _segmentStride * div;

                _segmentsPerBlock = 1;
            }
        }

        public override void OpenOrCreate(string fileName, int length, int stride)
        {
            lock (_syncIndex)
            {
                Trace.TraceInformation("_syncIndex entered.");

                base.OpenOrCreate(fileName, length, stride);

                var len = length.Clamp(1, int.MaxValue);

                _index = MemoryMappedFile.CreateOrOpen
                    (@"Global\" + Guid.NewGuid().ToString()
                    , _segmentStride * len
                    , MemoryMappedFileAccess.ReadWriteExecute
                    , MemoryMappedFileOptions.None
                    , new MemoryMappedFileSecurity()
                    , HandleInheritability.Inheritable);

                _segmentsPerHint = (len / 40000).Clamp(Environment.SystemPageSize, int.MaxValue);

                _lookupGroups = GetCPUGroupsForLookup();

                Trace.TraceInformation("_syncIndex exited.");
            }

            ClearCache();
        }

        #region Flush 

        public void FlushNew(IDictionary<IdType, EntityType> items)
        {
            lock (_syncQueue)
                _flushQueue.Enqueue(items);

            ThreadPool.QueueUserWorkItem(new WaitCallback(BeginFlushNew));
        }

        protected virtual void BeginFlushNew(object state)
        {
            IDictionary<IdType, EntityType> queue = null;

            Monitor.Enter(_syncFlush);

            try
            {
                Trace.TraceInformation("_syncFlush entered.");
                Monitor.Enter(_syncMap);
                Trace.TraceInformation("_syncMap has entered for {0}.", _fileName);

                _inFlush = true;

                if (_flushQueue.Count < 1)
                    return;

                lock (_syncQueue)
                    queue = _flushQueue.Dequeue();

                if (queue == null || queue.Count < 1)
                    return;

                var subsets = GetInsertTaskGroups(queue.Count);

                int rowSize;
                var items = GetFormattedFrom(subsets, queue, out rowSize);

                subsets = GetInsertTaskGroups(queue.Count);

                object hintSync = new object();
                Dictionary<IdType, int> newHints = new Dictionary<IdType, int>();

                var idReadBuffer = new byte[_idConverter.Length];
                var newSegmentBuffer = new byte[_segConverter.Length];
                var copyBuffer = new byte[Stride];

                var newMap = MemoryMappedFile.CreateOrOpen
                    (@"Global\" + Guid.NewGuid().ToString()
                    , items.Count * rowSize
                    , MemoryMappedFileAccess.ReadWriteExecute
                    , MemoryMappedFileOptions.None
                    , new MemoryMappedFileSecurity()
                    , HandleInheritability.Inheritable);

                var newIndexMap = MemoryMappedFile.CreateOrOpen
                    (@"Global\" + Guid.NewGuid().ToString()
                    , items.Count * _segmentStride
                    , MemoryMappedFileAccess.ReadWriteExecute
                    , MemoryMappedFileOptions.None
                    , new MemoryMappedFileSecurity()
                    , HandleInheritability.Inheritable);

                Parallel.ForEach(subsets, delegate(KeyValuePair<int, int> subset)
                {
                    var seg = subset.Key;

                    var toAdd = items.OrderBy(k => k.Key, _idConverter)
                        .Skip(subset.Key)
                        .Take((subset.Value - subset.Key) + 1)
                        .ToDictionary(s => s.Key, s => s.Value);

                    using (var newView = newMap.CreateViewStream
                        (subset.Key * rowSize
                        , ((subset.Value - subset.Key) + 1) * rowSize
                        , MemoryMappedFileAccess.ReadWriteExecute))
                    {
                        using (var newIndexView = newIndexMap.CreateViewStream
                            (subset.Key * _segmentStride
                            , ((subset.Value - subset.Key) + 1) * _segmentStride
                            , MemoryMappedFileAccess.ReadWriteExecute))
                        {
                            foreach (var item in toAdd)
                            {
                                WriteInsert(item.Key, item.Value, seg, idReadBuffer, newSegmentBuffer, newIndexView, newView, rowSize);

                                lock (hintSync)
                                    if (seg % _segmentsPerHint == 0 && !newHints.ContainsKey(item.Key))
                                        newHints.Add(item.Key, seg);

                                seg++;
                            }

                            newIndexView.Flush();
                            newIndexView.Close();
                        }

                        newView.Flush();
                        newView.Close();
                    }
                });

                lock (_syncIndex)
                {
                    Stride = rowSize;

                    _file.Dispose();
                    _index.Dispose();

                    _file = newMap;
                    _index = newIndexMap;

                    Length = items.Count;

                    _lookupGroups = GetCPUGroupsForLookup();

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
                Monitor.Exit(_syncMap);
                Trace.TraceInformation("_syncMap has exited for {0}.", _fileName);

                Monitor.Exit(_syncFlush);
                Trace.TraceInformation("_syncFlush exited.");

                _inFlush = false;

                GC.Collect();

                //alert the calling repository that these items can 
                //be released from the staging cache.
                InvokeFlushCompleted(queue);
            }

            //Launch a new flush call in case we were blocking another thread with updated information.
            int queueCount = 0;
            lock (_syncQueue)
                queueCount = _flushQueue.Count;

            if (queueCount > 0)
                BeginFlush(state);
        }

        protected List<KeyValuePair<int, int>> GetInsertTaskGroups(int count)
        {
            var subsets = new List<KeyValuePair<int, int>>();
            var inserts = TaskGrouping.GetSegmentedTaskGroups(count, Stride);

            for (var i = 0; i < inserts.Count; i++)
            {
                if (i == 0)
                    subsets.Add(new KeyValuePair<int, int>(0, inserts[i]));
                else
                    subsets.Add(new KeyValuePair<int, int>(inserts[i - 1] + 1, inserts[i]));
            }

            return subsets;
        }

        protected IDictionary<IdType, byte[]> GetFormattedFrom(List<KeyValuePair<int, int>> groups, IDictionary<IdType, EntityType> queue, out int rowSize)
        {
            var sync = new object();
            rowSize = Stride;
            var buffers = new Dictionary<IdType, byte[]>();

            var items = queue.OrderBy(q => q.Key, _idConverter).ToList();

            if (items.Count < 1)
                return buffers;

            Parallel.ForEach(groups, delegate(KeyValuePair<int, int> group)
            {
                foreach (var item in items.Skip(group.Key).Take((group.Value - group.Key) + 1))
                {
                    var buffer = new byte[0];

                    _formatter.TryFormatObj(item.Value, out buffer);

                    lock (sync)
                        buffers.Add(item.Key, buffer);
                }
            });

            rowSize = ((buffers.Values.Max(v => v.Length) / 64) + 1) * 64;

            return buffers;
        }

        public void Flush(IDictionary<IdType, EntityType> items)
        {
            if (Length <= 0 && !FlushQueueActive)
            {
                this.FlushNew(items);

                return;
            }

            lock (_flushQueue)
                _flushQueue.Enqueue(items);

            ThreadPool.QueueUserWorkItem(new WaitCallback(BeginFlush));
        }

        protected void BeginFlush(object state)
        {
            IDictionary<IdType, EntityType> queue = null;

            lock (_syncQueue)
            {
                if (_flushQueue.Count > 0)
                    queue = _flushQueue.Dequeue();
            }

            while (queue != null && queue.Count > 0)
            {
                if (!Monitor.TryEnter(_syncFlush, 500))
                    return;

                try
                {
                    Trace.TraceInformation("_syncFlush entered.");

                    _inFlush = true;

#if DEBUG
                    if (queue == null)
                        throw new ArgumentNullException("Cached items list to flush was null.");
#endif

                    var subsets = GetInsertTaskGroups(queue.Count);

                    int rowSize;
                    var items = GetFormattedFrom(subsets, queue, out rowSize);

                    if (rowSize < Stride)
                        rowSize = Stride;

                    List<IndexingCPUGroup<IdType>> newGroups = GetCPUGroupsForFlush(queue, rowSize);

                    var length = newGroups.Max(g => g.Inserts.Max(i => i.EndNewSegment)) + 1;

                    var hintSync = new object();
                    var newHints = new Dictionary<IdType, int>();

                    var newMap = MemoryMappedFile.CreateOrOpen
                       (@"Global\" + Guid.NewGuid().ToString()
                       , length * rowSize
                       , MemoryMappedFileAccess.ReadWriteExecute
                       , MemoryMappedFileOptions.None
                       , new MemoryMappedFileSecurity()
                       , HandleInheritability.Inheritable);

                    var newIndexMap = MemoryMappedFile.CreateOrOpen
                        (@"Global\" + Guid.NewGuid().ToString()
                        , length * _segmentStride
                        , MemoryMappedFileAccess.ReadWriteExecute
                        , MemoryMappedFileOptions.None
                        , new MemoryMappedFileSecurity()
                        , HandleInheritability.Inheritable);

                    Monitor.Enter(_syncMap);

                    try
                    {
                        Trace.TraceInformation("_syncMap has entered for {0}.", _fileName);

                        Parallel.ForEach(newGroups, delegate(IndexingCPUGroup<IdType> group)
                        {
                            try
                            {
                                int segment = group.StartSegment;

                                var groupUpdates = 0;

                                using (var indexView = _index.CreateViewStream
                                    (group.StartSegment * _segmentStride
                                    , ((group.EndSegment - group.StartSegment) + 1) * _segmentStride
                                    , MemoryMappedFileAccess.Read))
                                {
                                    using (var view = _file.CreateViewStream(
                                        group.StartSegment * Stride
                                            , ((group.EndSegment - group.StartSegment) + 1) * Stride
                                            , MemoryMappedFileAccess.Read))
                                    {
                                        foreach (var subset in group.Inserts)
                                        {
                                            int newSegment = subset.StartNewSegment;

                                            using (var newView = newMap.CreateViewStream
                                                (subset.StartNewSegment * rowSize
                                                , ((subset.EndNewSegment - subset.StartNewSegment) + 1) * rowSize
                                                , MemoryMappedFileAccess.ReadWriteExecute))
                                            {
                                                using (var newIndexView = newIndexMap.CreateViewStream
                                                    (subset.StartNewSegment * _segmentStride
                                                    , ((subset.EndNewSegment - subset.StartNewSegment) + 1) * _segmentStride
                                                    , MemoryMappedFileAccess.ReadWriteExecute))
                                                {
                                                    var idReadBuffer = new byte[_idConverter.Length];
                                                    var segReadBuffer = new byte[_segConverter.Length];
                                                    var newSegmentBuffer = new byte[_segConverter.Length];
                                                    var copyBuffer = new byte[Stride];

                                                    //var newSegment = subset.StartNewSegment;

                                                    //read id
                                                    var read = indexView.Read(idReadBuffer, 0, idReadBuffer.Length);
                                                    var id = _idConverter.FromBytes(idReadBuffer);
                                                    var nextId = default(IdType);

                                                    //skip segment
                                                    read = indexView.Read(segReadBuffer, 0, segReadBuffer.Length);
                                                    var seg = _segConverter.FromBytes(segReadBuffer);

                                                    List<IdType> toWriteFromCache = new List<IdType>();
                                                    List<IdType> updates = new List<IdType>();

                                                    while (newSegment <= subset.EndNewSegment)
                                                    {
                                                        //setup segment to be written
                                                        newSegmentBuffer = _segConverter.ToBytes(newSegment);

                                                        //insert before
                                                        if (segment == group.StartSegment)
                                                        {
                                                            toWriteFromCache = subset.IdsToAdd.Where
                                                                (a => _idConverter.Compare(a, id) < 0
                                                                && !updates.Contains(a)).ToList();

                                                            if (toWriteFromCache.Count > 0)
                                                            {
                                                                lock (hintSync)
                                                                    WriteInsertHints(toWriteFromCache, newHints, newSegment);

                                                                newSegment = WriteInserts(toWriteFromCache, newSegment, idReadBuffer, newSegmentBuffer, newIndexView, newView, items, rowSize);
                                                            }
                                                        }

                                                        if (segment <= group.EndSegment)
                                                        {
                                                            if (items.ContainsKey(id))
                                                            {
                                                                //update
                                                                WriteUpdate(id, items[id], idReadBuffer, newSegmentBuffer, newSegmentBuffer, view, newView, newIndexView, rowSize);

                                                                updates.Add(id);

                                                                lock (hintSync)
                                                                    if (segment % _segmentsPerHint == 0 && !newHints.ContainsKey(id))
                                                                        newHints.Add(id, segment);

                                                                segment++;
                                                                newSegment++;
                                                            }
                                                            else
                                                            {
                                                                //copy
                                                                WriteOld(id, idReadBuffer, newSegmentBuffer, copyBuffer, view, newView, newIndexView, rowSize);

                                                                lock (hintSync)
                                                                    if (segment % _segmentsPerHint == 0 && !newHints.ContainsKey(id))
                                                                        newHints.Add(id, segment);

                                                                segment++;
                                                                newSegment++;
                                                            }

                                                            if (segment <= group.EndSegment)
                                                            {
                                                                //read nextId
                                                                read = indexView.Read(idReadBuffer, 0, idReadBuffer.Length);
                                                                //skip segment
                                                                indexView.Position += _segConverter.Length;

                                                                nextId = _idConverter.FromBytes(idReadBuffer);

                                                                //read nextId until valid Id is found.
                                                                while (read > 0 && _idConverter.Compare(nextId, default(IdType)) == 0 && segment <= group.EndSegment)
                                                                {
                                                                    read = indexView.Read(idReadBuffer, 0, idReadBuffer.Length);
                                                                    //skip segment
                                                                    indexView.Position += _segConverter.Length;

                                                                    nextId = _idConverter.FromBytes(idReadBuffer);

                                                                    segment++;
                                                                    newSegment++;
                                                                }

                                                                //insert between.
                                                                if (subset.IdsToAdd.Count - updates.Count() > 0 &&
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

                                                                        newSegment = WriteInserts(toWriteFromCache, newSegment, idReadBuffer, newSegmentBuffer, newIndexView, newView, items, rowSize);
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
                                                                newSegment = WriteInserts(toWriteFromCache, newSegment, idReadBuffer, newSegmentBuffer, newIndexView, newView, items, rowSize);

                                                            //we're done with this subset
                                                            break;
                                                        }
                                                    }

                                                    groupUpdates += updates.Count;

                                                    newView.Flush();
                                                    newView.Close();
                                                    newIndexView.Flush();
                                                    newIndexView.Close();
                                                }
                                            }

                                            GC.Collect();
                                        }
                                    }
                                }

                                if (group.Inserts.Max(i => i.EndNewSegment) == length - 1)
                                    length -= groupUpdates;
                            }
                            catch (Exception ex)
                            {
                                Trace.TraceError(ex.ToString());
                                throw;
                            }
                        });

                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError(ex.ToString());
                        throw;
                    }
                    finally
                    {
                        Monitor.Exit(_syncMap);
                        Trace.TraceInformation("_syncMap has exited for {0}.", _fileName);
                    }

                    lock (_syncIndex)
                    {
                        Stride = rowSize;

                        Trace.TraceInformation("_syncIndex entered.");

                        _index.Dispose();
                        _file.Dispose();

                        _index = newIndexMap;
                        _file = newMap;

                        ClearCache();

                        Length = length;

                        _lookupGroups = GetCPUGroupsForLookup();

                        lock (_syncHints)
                            _hints = newHints;

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

                    //alert the calling repository that these items can 
                    //be released from the staging cache.
                    InvokeFlushCompleted(queue);
                }

                lock (_syncQueue)
                {
                    if (_flushQueue.Count > 0)
                        queue = _flushQueue.Dequeue();
                }
            }
        }

        protected virtual void WriteInsertHints(List<IdType> toWriteFromCache, Dictionary<IdType, int> newHints, int newSegment)
        {
            int tmpSeg = newSegment;

            toWriteFromCache.ForEach(delegate(IdType i)
            {
                if (newSegment % _segmentsPerHint == 0 && !newHints.ContainsKey(i))
                    newHints.Add(i, newSegment);

                tmpSeg++;
            });
        }

        protected virtual void WriteOld
            (IdType id, byte[] idReadBuffer
            , byte[] newSegmentBuffer
            , byte[] copyBuffer
            , Stream view
            , Stream newView
            , Stream newIndexView
            , int newStride)
        {
            //write new Index
            newIndexView.Write(idReadBuffer, 0, idReadBuffer.Length);
            newIndexView.Write(newSegmentBuffer, 0, newSegmentBuffer.Length);

            Array.Resize(ref copyBuffer, Stride);
            view.Read(copyBuffer, 0, copyBuffer.Length);

            Array.Resize(ref copyBuffer, newStride);
            newView.Write(copyBuffer, 0, copyBuffer.Length);
        }

        protected virtual void WriteUpdate(IdType id
            , byte[] item
            , byte[] idReadBuffer
            , byte[] copyBuffer
            , byte[] newSegmentBuffer
            , Stream view
            , Stream newView
            , Stream newIndexView
            , int newStride)
        {
            //write new Index
            newIndexView.Write(idReadBuffer, 0, idReadBuffer.Length);
            newIndexView.Write(newSegmentBuffer, 0, newSegmentBuffer.Length);

            view.Position += Stride;

            Array.Resize(ref item, newStride);
            newView.Write(item, 0, item.Length);
        }

        protected virtual int WriteInserts(List<IdType> toWriteFromCache
            , int newSegment
            , byte[] idReadBuffer
            , byte[] newSegmentBuffer
            , Stream newIndexView
            , Stream newView
            , IDictionary<IdType, byte[]> items
            , int newStride)
        {   
            toWriteFromCache.ForEach(delegate(IdType id)
            {
#if DEBUG
                if (_idConverter.Compare(id, default(IdType)) == 0)
                    return;

                if (newIndexView.Length == newIndexView.Position)
                    throw new InvalidOperationException("View stream capacity exceeded.");

                if (newView.Length == newView.Position)
                    throw new InvalidOperationException("View stream capacity exceeded.");
#endif

                WriteInsert(id, items[id], newSegment, idReadBuffer, newSegmentBuffer, newIndexView, newView, newStride);

                newSegment++;
            });

            toWriteFromCache.Clear();

            return newSegment;
        }

        protected virtual void WriteInsert(IdType id
            , byte[] item
            , int newSegment
            , byte[] idReadBuffer
            , byte[] newSegmentBuffer
            , Stream newIndexView
            , Stream newView
            , int newStride)
        {
            idReadBuffer = _idConverter.ToBytes(id);
            newSegmentBuffer = _segConverter.ToBytes(newSegment);

            newIndexView.Write(idReadBuffer, 0, idReadBuffer.Length);
            newIndexView.Write(newSegmentBuffer, 0, newSegmentBuffer.Length);

            Array.Resize(ref item, newStride);
            newView.Write(item, 0, item.Length);
        }
        
        protected virtual List<IndexingCPUGroup<IdType>> GetCPUGroupsForFlush(IDictionary<IdType, EntityType> items, int newStride)
        {
            var newGroups = new List<IndexingCPUGroup<IdType>>();

            if (Length < _blockSize)
            {
                newGroups.Add(new IndexingCPUGroup<IdType>()
                {
                    StartSegment = 0,
                    EndSegment = Length -1,

                    Inserts = new List<IndexingInsertSubset<IdType>>()
                    {
                        new IndexingInsertSubset<IdType>()
                        {
                            StartNewSegment = 0,
                            EndNewSegment = (Length - 1) + items.Count,
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

            newGroups = TaskGrouping.GetCPUGroupsFor(items, groups, _idConverter, Stride, newStride);

            return newGroups;
        }

        #endregion

        public virtual EntityType Load(IdType id)
        {
            if (Length == 0)
                return default(EntityType);

            var segment = GetFromCache(id);

            if (segment >= 0)
                return LoadFromSegment(segment);

            var segStart = GuessBlockStart(id);

            segment = IndexSeek(segStart, id);

            if (segment < 0)
                return default(EntityType);

            return LoadFromSegment(segment);
        }

        protected virtual int GuessBlockStart(IdType id)
        {
            lock (_syncHints)
            {
                if (_hints.ContainsKey(id))
                    return _hints[id];

                var last = _hints.AsParallel().Last(l => _idConverter.Compare(l.Key, id) <= 0);

                return last.Value;
            }
        }

        protected virtual IDictionary<int, IdType> GetSegments(List<int> segments)
        {
            var indicies = new Dictionary<int, IdType>();

            lock (_syncIndex)
            {
                //TODO: optimize later.
                segments.ForEach(delegate(int segment)
                {
                    if (segment > Length)
                        return;

                    if (segment < 0)
                        return;

                    var offset = segment * _segmentStride;

                    using (var view = _index.CreateViewStream
                        (offset, _segmentStride, MemoryMappedFileAccess.Read))
                    {
                        byte[] idBuf = new byte[_idConverter.Length];

                        var read = view.Read(idBuf, 0, idBuf.Length);
                        var rid = _idConverter.FromBytes(idBuf);

                        indicies.Add(segment, rid);
                    }
                });
            }

            return indicies;
        }

        public virtual IdType LookupFromSegment(int segment)
        {
            if (segment < 0)
                throw new ArgumentException("segment must be a non negative number.");

            lock (_syncIndex)
            {
                using (var view = _index.CreateViewStream
                     (segment * _segmentStride, _idConverter.Length, MemoryMappedFileAccess.Read))
                {
                    byte[] idBuf = new byte[_idConverter.Length];
                    view.Read(idBuf, 0, idBuf.Length);

                    return _idConverter.FromBytes(idBuf);
                }
            }
        }

        protected virtual int IndexSeek(int segmentStart, IdType id)
        {
            int segmentFound = -1;

            lock (_syncIndex)
            {
                Parallel.For(0, 2, delegate(int segMod)
                {
                    segMod = segMod - 1;

                    var segStart = segMod * _segmentsPerBlock;

                    if (segStart > Length)
                        return;

                    if (segStart < 0)
                        return;

                    var size = _blockSize.Clamp(_segmentStride, (Length - segMod) * _segmentStride);

                    using (var view = _index.CreateViewStream
                        (segStart * _segmentStride, size, MemoryMappedFileAccess.Read))
                    {
                        byte[] idBuf = new byte[_idConverter.Length];
                        byte[] segBuf = new byte[_segConverter.Length];
                        var read = view.Read(idBuf, 0, idBuf.Length);
                        var rid = _idConverter.FromBytes(idBuf);

                        int seg = 0;

                        while (read > 0)
                        {
                            if (segmentFound > -1)
                                break;

                            read = view.Read(segBuf, 0, segBuf.Length);
                            seg = _segConverter.FromBytes(segBuf);

                            if (_idConverter.Compare(rid, id) == 0)
                            {
                                segmentFound = seg;

                                break;
                            }

                            read = view.Read(idBuf, 0, idBuf.Length);
                            rid = _idConverter.FromBytes(idBuf);
                        }
                    }
                });
            }

            if (segmentFound > -1)
            {
                System.Threading.ThreadPool.QueueUserWorkItem
                    (new WaitCallback(StartCachingBlock), segmentFound);

                return segmentFound;
            }
            else
                return IndexScan(id);
        }

        protected virtual void StartCachingBlock(object state)
        {
            int segmentStart = (int)state;

            CacheBlock(segmentStart);
        }

        protected virtual void CacheBlock(int segmentStart)
        {
            var segmentKey = segmentStart / _segmentsPerBlock;

            lock (_syncCache)
                if (_indexCache.ContainsKey(segmentKey))
                    return;

            var cache = new Dictionary<IdType, int>();

            var size = _blockSize.Clamp(_segmentStride, (Length - segmentKey) * _segmentStride);

            if (size < _segmentStride)
                return;

            lock (_syncIndex)
            {
                //Trace.TraceInformation("_syncIndex entered.");

                using (var view = _index.CreateViewStream
                    (segmentKey * _segmentStride
                    , size, MemoryMappedFileAccess.Read))
                {
                    byte[] tmp = new byte[_idConverter.Length];
                    byte[] segBuf = new byte[_segConverter.Length];

                    var read = view.Read(tmp, 0, tmp.Length);
                    var rid = _idConverter.FromBytes(tmp);
                    int seg = -1;

                    while (read > 0)
                    {
                        read = view.Read(segBuf, 0, segBuf.Length);
                        seg = _segConverter.FromBytes(segBuf);

                        if (_idConverter.Compare(rid, default(IdType)) != 0)
                            if (!cache.ContainsKey(rid))
                                cache.Add(rid, seg);

                        read = view.Read(tmp, 0, tmp.Length);
                        rid = _idConverter.FromBytes(tmp);
                    }
                }

                //Trace.TraceInformation("_syncIndex exited.");
            }

            lock (_syncCache)
            {
                if (!_indexCache.ContainsKey(segmentKey))
                    _indexCache.Add(segmentKey, cache);
            }
        }

        protected virtual int IndexScan(IdType id)
        {
            var segmentFound = -1;
            CancellationTokenSource source = new CancellationTokenSource();

            lock (_syncIndex)
            {
                try
                {
                    //Trace.TraceInformation("_syncIndex entered.");
                    Parallel.ForEach(_lookupGroups, delegate(IndexingCPUGroup<IdType> group)
                    {
                        using (var view = _index.CreateViewStream
                            (group.StartSegment * _segmentStride
                            , (group.EndSegment - group.StartSegment) * _segmentStride
                            , MemoryMappedFileAccess.Read))
                        {
                            if (segmentFound > -1)
                                return;

                            byte[] idBuf = new byte[_idConverter.Length];
                            byte[] segBuf = new byte[_segConverter.Length];

                            var read = view.Read(idBuf, 0, idBuf.Length);
                            var rid = _idConverter.FromBytes(idBuf);
                            int seg = -1;

                            while (read > 0)
                            {
                                if (segmentFound > 0)
                                    break;

                                read = view.Read(segBuf, 0, segBuf.Length);
                                seg = _segConverter.FromBytes(segBuf);

                                if (_idConverter.Compare(rid, id) == 0)
                                {
                                    segmentFound = seg;

                                    lock (_syncHints)
                                        if (!_hints.ContainsKey(id))
                                            _hints.Add(rid, seg);

                                    System.Threading.ThreadPool.QueueUserWorkItem
                                        (new WaitCallback(StartCachingBlock), segmentFound);

                                    source.Cancel();

                                    break;
                                }

                                read = view.Read(idBuf, 0, idBuf.Length);
                                rid = _idConverter.FromBytes(idBuf);
                            }
                        }

                    });
                }
                //This is expected behavior, not sure why .NET throws an exception.
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.ToString());
                    throw;
                }
                finally
                {
                    //Trace.TraceInformation("_syncIndex exited.");
                }
            }

            return segmentFound;
        }

        public virtual bool Save(EntityType obj, IdType id)
        {
            //Note the order of the locks matches the order in the flush method. 
            //This is important, otherwise, threadlock will occur when flush is occuring.
            lock (_syncFlush)
            {
                lock (_syncIndex)
                {
                    var segment = GetFromCache(id);

                    if (segment > -1)
                        return SaveToFile(obj, id, segment);

                    var segStart = GuessBlockStart(id);

                    segment = IndexSeek(segStart, id);

                    if (segment < 0)
                        return false;

                    return SaveToFile(obj, id, segment);
                }
            }
        }

        public virtual bool Save(EntityType obj, IdType id, int segment)
        {
            return SaveToFile(obj, id, segment);
        }

        [Obsolete]
        public override bool SaveToFile(EntityType obj, int segment)
        {
            throw new NotSupportedException("Use overload instead.");
        }

        public virtual bool SaveToFile(EntityType obj, IdType id, int segment)
        {
            //Note the order of the locks matches the order in the flush method. 
            //This is important, otherwise, threadlock will occur when flush is occuring.
            lock (_syncFlush)
            {
                lock (_syncIndex)
                {

                    SaveIndex(id, segment);

                    return base.SaveToFile(obj, segment);
                }
            }
        }

        protected virtual void SaveIndex(IdType id, int segment)
        {
            using (var view = _index.CreateViewStream
                (segment * _segmentStride, _segmentStride
                , MemoryMappedFileAccess.ReadWriteExecute))
            {
                view.Write(_idConverter.ToBytes(id), 0, _idConverter.Length);
                view.Write(_segConverter.ToBytes(segment), 0, _segConverter.Length);

                view.Flush();
            }

            if (segment % this._segmentsPerHint == 0 && !_hints.ContainsKey(id))
                lock (_syncHints)
                    _hints[id] = segment;

            if (segment > Length)
                Length = segment;
        }

        public virtual int SaveBatchToFile(IDictionary<IdType, EntityType> objs, int segmentStart)
        {
            if (objs.IsNullOrEmpty())
                return segmentStart;

            //Note the order of the locks matches the order in the flush method. 
            //This is important, otherwise, threadlock will occur when flush is occuring.
            lock (_syncFlush)
            {
                lock (_syncIndex)
                {

                    var seg = segmentStart;

                    var key = objs.First().Key;

                    if (!_hints.ContainsKey(key))
                        lock (_syncHints)
                            _hints.Add(key, seg);

                    using (var view = _index.CreateViewStream
                        (seg * _segmentStride, objs.Count * _segmentStride
                        , MemoryMappedFileAccess.ReadWriteExecute))
                    {
                        foreach (var obj in objs)
                        {
                            view.Write(_idConverter.ToBytes(obj.Key), 0, _idConverter.Length);
                            view.Write(_segConverter.ToBytes(seg), 0, _segConverter.Length);

                            seg++;
                        }

                        view.Flush();
                    }

                    return base.SaveBatchToFile(objs.Values.ToList(), segmentStart);
                }
            }
        }

        public override bool FlushQueueActive
        {
            get
            {
                if (_inFlush)
                    return true;

                int count = 0;
                lock (_syncQueue)
                    count = _flushQueue.Count;

                return count > 0;
            }
        }

        #region ICache<int,I> Members

        public bool IsNew(IdType id)
        {
            return false;
        }

        public bool Contains(IdType id)
        {
            lock (_syncCache)
                foreach (var k in _indexCache.Keys)
                    if (_indexCache[k].ContainsKey(id))
                        return true;

            return false;
        }

        public int GetFromCache(IdType id)
        {
            lock (_syncCache)
                foreach (var k in _indexCache.Keys)
                    if (_indexCache[k].ContainsKey(id))
                        return _indexCache[k][id];

            return -1;
        }

        public void CacheItem(IdType id)
        {

        }

        public void Detach(IdType id)
        {
            lock (_syncCache)
                foreach (var k in _indexCache.Keys)
                    if (_indexCache[k].ContainsKey(id))
                        _indexCache[k].Remove(id);
        }

        public void ClearCache()
        {
            lock (_syncCache)
                _indexCache.Clear();

            lock (_syncHints)
                _hints.Clear();
        }

        public void Sweep()
        {
            lock (_syncCache)
                if (_indexCache.Count >= TaskGrouping.ReadLimit)
                    foreach (var k in _indexCache.Keys.Skip(TaskGrouping.ReadLimit))
                        _indexCache.Remove(k);
        }

        #endregion

        #region IEnumerable<T> Members

        public IEnumerator<EntityType> GetEnumerator()
        {
            return GetEntityEnumerator();
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        public EntityEnumerator<EntityType, IdType> GetEntityEnumerator()
        {
            return new EntityEnumerator<EntityType,IdType>(this, _syncMap, _idConverter);
        }

        #region OnFlushCompleted Event

        protected void InvokeFlushCompleted(IDictionary<IdType, EntityType> itemsFlushed)
        {
            if (OnFlushCompleted != null)
            {
                Trace.TraceInformation("FlushCompleted Invoked.");

                OnFlushCompleted(itemsFlushed);
            }
        }

        public event FlushCompleted<EntityType, IdType> OnFlushCompleted;

        #endregion

        public override void Dispose()
        {
            while (FlushQueueActive)
                Thread.Sleep(100);

            base.Dispose();

            if (_index != null)
                _index.Dispose();

            GC.Collect();
        }
    }
}

