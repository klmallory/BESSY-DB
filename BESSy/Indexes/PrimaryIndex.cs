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
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BESSy.Cache;
using BESSy.Extensions;
using BESSy.Factories;
using BESSy.Files;
using BESSy.Parallelization;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using BESSy.Json.Linq;
using BESSy.Transactions;
using System.IO;
using BESSy.Json;

namespace BESSy.Indexes
{
    public struct SegmentMap<DbSegType, IndexSegType>
    {
        [TargetedPatchingOptOut("Performance Critical")]
        public SegmentMap(int dbSegment, int indexSegment) : this()
        {
            DbSegment = dbSegment;
            IndexSegment = indexSegment;
        }

        public int DbSegment;
        public int IndexSegment;
    }

    public interface IIndex<PropertyType, EntityType, SegType> : IFlush, ISweep, ILoadAndRegister<EntityType>, IDisposable
    {
        int Length { get; }
        //SegType Add(IndexType property, SegType segment);
        //void Update(IndexType property, SegType segment);
        //void Delete(IndexType property);
        SegType Fetch(PropertyType property);
        SegType[] FetchBetween(PropertyType start, PropertyType end);
        PropertyType RidLookup(int dbSegment, out int indexSegment);
        long SaveSeed<IdType>();
        void Clear();
        void Rebuild(int newRowSize, int newRows);
    }

    public interface IPrimaryIndex<IdType, EntityType> : IIndex<IdType, EntityType, int>
    {
        ISeed<Int32> SegmentSeed { get; }
    }

    public class PrimaryIndex<IdType, EntityType> : IPrimaryIndex<IdType, EntityType>
    {
        public PrimaryIndex
            (string fileName
            ,string indexToken
            ,IBinConverter<IdType> propertyConverter
            ,IRepositoryCacheFactory cacheFactory
            ,IQueryableFormatter fileFormatter
            ,IIndexFileFactory indexFileFactory
            ,IRowSynchronizer<int> rowSynchonizer)
        {
            _segmentConverter = new BinConverter32();
            
            _fileName = fileName;
            _indexToken = indexToken;
            _propertyConverter = propertyConverter;
            _cacheFactory = cacheFactory;
            _fileFormatter = fileFormatter;
            _indexFileFactory = indexFileFactory;
            _rowSynchronizer = rowSynchonizer;

            if (typeof(IdType) == typeof(Guid))
                _guidWorkaround = true;
        }

        public PrimaryIndex
            (string fileName,
            IQueryableFormatter formatter,
            IRepositoryCacheFactory cacheFactory,
            IIndexFileFactory indexFileFactory,
            IRowSynchronizer<int> rowSynchonizer)
            : this(fileName, null, null, cacheFactory, formatter, indexFileFactory, rowSynchonizer)
        {

        }

        protected object _syncStaging = new object();
        protected object _syncOps = new object();
        protected object _syncCache = new object();
        protected object _syncFile = new object();
        protected object _syncHints = new object();

        protected bool _disposeRequested;
        protected bool _guidWorkaround;
        protected bool _busy;
        protected string _fileName;
        protected string _indexToken;
        protected IBinConverter<IdType> _propertyConverter;
        protected IBinConverter<int> _segmentConverter;
        protected IIndexFileManager<IdType, EntityType, int> _indexFile;
        protected IRepositoryCache<Guid, IDictionary<IdType, JObject>> _indexStaging;
        //protected IRepositoryCache<Guid, IDictionary<int, int>> _reverseStaging;

        protected IRepositoryCacheFactory _cacheFactory;
        protected IQueryableFormatter _fileFormatter;
        protected IIndexFileFactory _indexFileFactory;
        protected IRowSynchronizer<int> _rowSynchronizer;
        protected IAtomicFileManager<EntityType> _databaseFile;

        protected IDictionary<IdType, int> _hints = new Dictionary<IdType, int>();
        protected Stack<int> _operations = new Stack<int>();

        public ISeed<Int32> SegmentSeed { get; private set; }

        protected void CachePage(JObject[] page)
        {
            if (_disposeRequested)
                return;

            ThreadPool.QueueUserWorkItem
                (new WaitCallback(StartCachePage), page);
        }

        protected void CachePage(List<TransactionIndexResult<IdType>> actions)
        {
            if (_disposeRequested)
                return;

            ThreadPool.QueueUserWorkItem
                (new WaitCallback(StartCachePage), actions);
        }

        protected void StartCachePage(object state)
        {
            if (_disposeRequested)
                return;

            Trace.TraceInformation("Page caching started");

            if (state is JObject[])
                StartCachePage((JObject[])state);
            else
                StartCachePage((List<TransactionIndexResult<IdType>>)state);

            Trace.TraceInformation("Page caching completed"); 
        }

        protected void StartCachePage(List<TransactionIndexResult<IdType>> actions)
        {
            if (actions == null) return;

            var page = new Dictionary<IdType, JObject>();
            var reverse = new Dictionary<int, int>();

            var idsToRemove = actions.Where(a => a.Action == Action.Delete).Select(s => s.Index);

            var indexCache = _indexStaging.GetCache();

            var keysToRemove = new List<Guid>();

            foreach (var a in actions)
            {
                if (a.Action != Action.Delete)
                {
                    var j = _fileFormatter.Parse(a.Stream);
                    j.Add<int>("$segment", a.IndexSegment);

                    page.Add(a.Index, j);
                }
                else
                {
                    page.Add(a.Index, new JObject());
                    var keys =  indexCache.Where(c => c.Value.ContainsKey(a.Index)).Select(c => c.Key);

                    if (keys != null && keys.Count() > 0)
                        keysToRemove.AddRange(keys);

                    lock (_syncHints)
                        _hints.Remove(a.Index);
                }
            }

            lock (_syncStaging)
            {
                foreach (var k in keysToRemove)
                    _indexStaging.Detach(k);

                var g = Guid.NewGuid();

                _indexStaging.UpdateCache(g, page, true, false);
            }
        }

        protected void StartCachePage(JObject[] page)
        {
            if (page == null) return;

            var block = page.Where(j => j != null && j.HasValues).ToDictionary(p => p.Value<IdType>("Id"), p => p);

            var reverse = block.Where(b => b.Value.Value<int>("Property") > 0).ToDictionary(b => b.Value.Value<int>("Property"), b => b.Value.Value<int>("$segment"));

            lock (_syncStaging)
            {
                var g = Guid.NewGuid();
                _indexStaging.UpdateCache(g, block, true, false);
            }
        }

        protected virtual void SyncCache(IDictionary<IdType, SegmentMap<int, int>> segments)
        {

        }

        protected int GetSegmentFromCache(IdType prop, out int iSeg)
        {
            iSeg = 0;

            lock (_syncStaging)
            {
                if (_indexStaging.Count > 0)
                {
                    var s = _indexStaging.GetCache().Values.LastOrDefault(v => v.ContainsKey(prop));

                    if (s != null)
                    {
                        var c = s[prop];
                        iSeg = c.Value<int>("$segment"); //IndexSegment;
                        return c.Value<int>("Property");
                    }
                }
            }

            return 0;
        }

        protected int GetSegmentFromFile(IdType prop, out int indexSegment)
        {
            indexSegment = 0;

            var cache = GetSegmentFromCache(prop, out indexSegment);

            if (cache > 0)
                return cache;

            var start = GetPageHint(prop);

            if (start > 0)
            {
                var page = _indexFile.GetPage(start);

                var seg = GetSegmentFromPage(page, prop, out indexSegment);

                if (seg > 0)
                    return seg;
            }

            var count = 0;

            foreach (var page in _indexFile.AsEnumerable())
            {
                var seg = GetSegmentFromPage(page, prop, out indexSegment);

                if (seg > 0)
                {
                    lock (_syncHints)
                        if (_hints.Count < TaskGrouping.ArrayLimit)
                            if (!_hints.ContainsKey(prop))
                                _hints.Add(prop, count);

                    return seg;
                }

                count++;
            }

            return 0;
        }

        protected virtual int GetSegmentFromPage(JObject[] page, IdType prop, out int indexSegment)
        {
            JObject index = null;

            index = page.FirstOrDefault(query => _propertyConverter.Compare(query.Value<IdType>("Id"), prop) == 0);

            indexSegment = 0;

            if (!object.Equals(index, default(JObject)))
            {
                CachePage(page);
                indexSegment = index.Value<int>("$segment");
                return index.Value<int>("Property");
            }

            return indexSegment;
        }

        protected virtual IdType GetSegmentFromPage(JObject[] page, int dbSegment, out int indexSegment)
        {
            JObject index = null;

            index = page.FirstOrDefault(query => _segmentConverter.Compare(query.Value<int>("Property"), dbSegment) == 0);

            if (!object.Equals(index, default(JObject)))
            {
                CachePage(page);
                indexSegment = index.Value<int>("$segment");
                return index.Value<IdType>("Id");
            }

            indexSegment = 0;
            return default(IdType);
        }

        protected int GetPageHint(IdType prop)
        {
            if (_hints.Count < 1)
                return 0;

            if (_hints.ContainsKey(prop))
                return _hints[prop];

            lock (_syncHints)
                return _hints.AsParallel().LastOrDefault(h => _propertyConverter.Compare(h.Key, prop) <= 0).Value;
        }

        public virtual int Load()
        {
            Trace.TraceInformation("Primary index loading");

            lock (_syncFile)
            {
                if (_propertyConverter != null)
                    _indexFile = _indexFileFactory.CreatePrimary<IdType, EntityType>(_fileName, _indexToken, Environment.SystemPageSize, 10240, Environment.SystemPageSize, _propertyConverter, _fileFormatter, _rowSynchronizer);
                else
                    _indexFile = _indexFileFactory.CreatePrimary<IdType, EntityType>(_fileName, Environment.SystemPageSize, _fileFormatter, _rowSynchronizer);

                _indexFile.Rebuilt += new Rebuild<IndexPropertyPair<IdType, int>>(OnRebuilt);
                _indexFile.Load<IdType>();

                SegmentSeed = _indexFile.SegmentSeed;

                _indexToken = SegmentSeed.IdProperty;
                _propertyConverter = (IBinConverter<IdType>)SegmentSeed.IdConverter;

                lock (_syncCache)
                {
                    _indexStaging = _cacheFactory.Create<Guid, IDictionary<IdType, JObject>>(true, TaskGrouping.ArrayLimit, new BinConverterGuid());
                }

                _indexFile.UpdateFailed += new UpdateFailed<IdType>
                    (delegate(IList<TransactionIndexResult<IdType>> results, IDisposable transaction, int newStride, int newLength)
                {
                    Trace.TraceInformation("Primary index rebuild triggered");

                    _indexFile.Rebuild(newStride, newLength, SegmentSeed.MinimumSeedStride);
                });
            }

            InitializeCache();

            return _indexFile.Length;
        }

        void OnRebuilt(Guid transactionId, int newStride, int newLength, int newSeedStride)
        {
            _indexStaging.ClearCache();
            SaveSeed<Int32>();
        }

        protected virtual void InitializeCache()
        {
            Trace.TraceInformation("PrimaryIndex cache initializing");

            lock (_syncHints)
                _hints.Clear();

            //lock (_syncCache)
            //    _indexCache.ClearCache();

            var pages = _indexFile.AsEnumerable().Count().Clamp(1, int.MaxValue);
            var pageCount = (_indexFile.Length / pages).Clamp(1, int.MaxValue);
            var pageSkip = (_indexStaging.CacheSize / pageCount).Clamp(1, TaskGrouping.ArrayLimit);
            var hintSkip = (pages / TaskGrouping.ArrayLimit).Clamp(1, TaskGrouping.ArrayLimit);

            var count = 0;
            foreach (var p in _indexFile.AsEnumerable())
            {
                if (p.Length > 0)
                {
                    if (count % hintSkip == 0)
                    {
                        var index = p[0].Value<IdType>(_indexToken);
                        if (_propertyConverter.Compare(index, default(IdType)) != 0)
                            lock (_syncHints)
                                _hints.Add(index, count);
                    }

                    if (count % pageSkip == 0)
                        CachePage(p);
                }

                count++;
            }
        }

        public bool FileFlushQueueActive { get { return _indexFile.FileFlushQueueActive || _operations.Count > 0; } }

        public int Add(IdType property, int segment)
        {
            throw new NotSupportedException();

            //var seg = _indexFile.SaveSegment(new IndexPropertyPair<IdType, int>(property, segment));

            //StartCachePage(new List<TransactionIndexResult<IdType>>() { new TransactionIndexResult<IdType>(property, Action.Create, segment, seg) });

            //return seg;
        }

        public void Update(IdType property, int segment)
        {
            throw new NotSupportedException();

            //int seg = 0;
            //int iSeg = 0;

            //seg = GetSegmentFromCache(property, out iSeg);

            //if (seg == 0)
            //    seg = GetSegmentFromFile(property, out iSeg);

            //if (seg == 0)
            //    return;

            //StartCachePage(new List<TransactionIndexResult<IdType>>() { new TransactionIndexResult<IdType>(property, Action.Delete, seg, iSeg) });

            //_indexFile.SaveSegment(new IndexPropertyPair<IdType, int>(property, segment), iSeg);
        }

        public void Delete(IdType property)
        {
            throw new NotSupportedException();
            //int seg = 0;
            //int iSeg = 0;

            //seg = GetSegmentFromCache(property, out iSeg);

            //if (seg == 0)
            //    seg = GetSegmentFromFile(property, out iSeg);

            //if (seg == 0)
            //    return;

            //StartCachePage(new List<TransactionIndexResult<IdType>>() { new TransactionIndexResult<IdType>(property, Action.Delete, 0, 0) });

            //_indexFile.DeleteSegment(iSeg);
        }

        public int Fetch(IdType prop)
        {          
            int iSeg = 0;

            var seg = GetSegmentFromCache(prop, out iSeg);

            if (seg > 0)
                return seg;

            seg = GetSegmentFromFile(prop, out iSeg);

            if (seg > 0)
                return seg;

            return 0;
        }

        public int[] FetchBetween(IdType start, IdType end)
        {
            var segments = new List<int>();

            foreach (var p in _indexFile.AsEnumerable())
            {
                segments.AddRange
                    (p.Where(j => _propertyConverter.Compare(j.Value<IdType>(_indexToken), start) >= 0
                        && _propertyConverter.Compare(j.Value<IdType>(_indexToken), end) <= 0)
                    .Select(i => i.Value<int>("Property")));
            }

            return segments.ToArray();
        }

        public IdType RidLookup(int dbSegment, out int indexSegment)
        {
            //Todo: Test

            foreach (var p in _indexFile.AsEnumerable())
            {
                var match = p.FirstOrDefault(o => o.Value<int>("Property") == dbSegment);

                if (match == null)
                    continue;

                indexSegment = match.Value<int>("$segment");

                return match.Value<IdType>("Id");
            }

            indexSegment = 0;

            return default(IdType);
        }

        public IDictionary<int, IndexPropertyPair<IdType, int>> RidLookup(IEnumerable<int> dbSegments)
        {
            var indexes = new Dictionary<int, IndexPropertyPair<IdType, int>>();

            foreach (var p in _indexFile.AsEnumerable())
            {
                var matches = p.Where(o => dbSegments.Contains(o.Value<int>("Property")));

                foreach (var m in matches)
                    indexes.Add(m.Value<int>("Property"), new IndexPropertyPair<IdType, int>(m.Value<IdType>("Id"), m.Value<int>("$segment")));
            }

            return indexes;
        }

        public int Length
        {
            get { return _indexFile.Length; }
        }

        public void Clear()
        {
            lock (_syncStaging)
                _indexStaging.ClearCache();

            lock (_syncHints)
                _hints.Clear();
        }

        public void Sweep()
        {
            lock (_syncStaging)
                _indexStaging.Sweep();

            lock (_syncHints)
            {
                if (_hints.Count() >= TaskGrouping.ArrayLimit * .80)
                {
                    var toRemove = _hints.Take(_hints.Count() / 2).Select(d => d.Key).ToList();

                    toRemove.ForEach(r => _hints.Remove(r));
                }
            }
        }

        public void Register(IAtomicFileManager<EntityType> databaseFile)
        {
            _databaseFile = databaseFile;

            _databaseFile.TransactionCommitted += new Committed<EntityType>(OnTransactionCommit);
            _databaseFile.Reorganized += new Reorganized<EntityType>(OnReorganized);

            if (_databaseFile.Length > Length)
                OnReorganized(_databaseFile.Length);
        }

        protected void OnReorganized(int recordsWritten)
        {
            //This operation needs to be synchronous.
            Clear();

            var results = new List<TransactionIndexResult<IdType>>();

            lock (_syncFile)
            {
                lock (_syncOps)
                    _operations.Push(2);

                try
                {
                    using (var l = _rowSynchronizer.LockAll())
                    {
                        _indexFile.ReinitializeSeed<IdType>(recordsWritten);

                        var pages = _indexFile.AsEnumerable().Count().Clamp(1, int.MaxValue);
                        var pageCount = (_indexFile.Length / pages).Clamp(1, int.MaxValue);
                        var pageSkip = (_indexStaging.CacheSize / pageCount).Clamp(1, TaskGrouping.ArrayLimit);
                        var hintSkip = (pages / TaskGrouping.ArrayLimit).Clamp(1, TaskGrouping.ArrayLimit);

                        var count = 0;
                        var pageNumber=-1;
                        foreach (var page in _databaseFile.AsEnumerable())
                        {
                            pageNumber++;

                            if (page.Count() <= 0)
                                continue;

                            results.Clear();

                            foreach (var o in page)
                            {
                                count++;
                                var index = o.Value<IdType>(_indexToken);
                                var prop = o.Value<int>("Property");
                                var seg = o.Value<int>("$segment");

                                results.Add(new TransactionIndexResult<IdType>(index, Action.Update, seg, count));
                            }

                            if (results.Count > recordsWritten)
                                throw new InvalidOperationException("Reorganization mismatch in index.");

                            _indexFile.UpdateFromTransaction(results);

                            if (pageNumber % hintSkip == 0)
                                lock (_syncHints)
                                {
                                    var id = page[0].Value<IdType>(_indexToken);

                                    if (!_hints.ContainsKey(id))
                                        _hints.Add(id, pageNumber);
                                }

                            if (pageNumber % pageSkip == 0)
                                CachePage(page);
                        }
                    }
                }
                finally { lock (_syncOps) { _operations.Pop(); } }

            }
        }

        protected virtual void OnTransactionCommit(IList<TransactionResult<EntityType>> results, IDisposable transaction)
        {
            int ops = 0;

            lock (_syncOps)
            {
                ops = _operations.Count;

                if (_disposeRequested && ops <= 0)
                    return;
            }

            Trace.TraceInformation("PrimaryIndex detected transaction committed ");

            var state = new Tuple<IList<TransactionResult<EntityType>>, IDisposable>(results, transaction);

            //ThreadPool.QueueUserWorkItem(new WaitCallback(UpdateFromTransaction), state);

            UpdateFromTransaction(state);

            //lock (_syncOps)
            //    ops = _operations.Count;

            ////give the operation a chance to start for concurrency reasons.
            //if (ops <= 0)
            //    Thread.Sleep(100);
        }

        protected virtual void UpdateFromTransaction(object state)
        {
            lock (_syncOps)
                _operations.Push(1);

            try
            {
                var param = ((Tuple<IList<TransactionResult<EntityType>>, IDisposable>)state);

                var results = param.Item1;
                //var transaction = param.Item2 as ITransaction<IdType, EntityType>;
                //var enlisted = transaction.GetEnlistedActions();

                lock (_syncFile)
                    _busy = true;

                var actions = new List<TransactionIndexResult<IdType>>();
                var ids = new List<IdType>();
                var index = default(IdType);

                var deleteIndexes = RidLookup(results.Where(a => a.Action == Action.Delete).Select(s => s.Segment));
                var existingIndexes = RidLookup(results.Where(a => a.Action == Action.Create).Select(s => s.Segment));

                foreach (var item in results.OrderBy(k => k.Action))
                {
                    switch (item.Action)
                    {
                        case Action.Update:
                            index = _indexFile.IndexGet(item.Entity);

                            if (!ids.Contains(index))
                                actions.Add(new TransactionIndexResult<IdType>(index, item.Action, item.Segment, Fetch(index)));
                            else
                                throw new InvalidDataException(string.Format("duplicate index: {0}", index));

                            break;
                        case Action.Create:
                            index = _indexFile.IndexGet(item.Entity);

                            if (!ids.Contains(index) && (!existingIndexes.Values.Any(a => a.Property == item.Segment) || deleteIndexes.Values.Any(a => a.Property == item.Segment)))
                                actions.Add(new TransactionIndexResult<IdType>(index, item.Action, item.Segment, 0));
                            else
                                throw new InvalidDataException(string.Format("duplicate index: {0}", index));

                            break;
                        case Action.Delete:

                            if (!deleteIndexes.ContainsKey(item.Segment))
                                continue;

                            var segment = deleteIndexes[item.Segment];

                            index = segment.Id;

                            if (_propertyConverter.Compare(index, default(IdType)) == 0)
                                throw new InvalidDataException(string.Format("index not found for db segment: {0}", item.Segment));

                            if (ids.Contains(index))
                            {
                                var action = actions.Single(a => _propertyConverter.Compare(a.Index, index) == 0);
                                actions.Remove(action);
                            }

                            actions.Add(new TransactionIndexResult<IdType>(index, item.Action, item.Segment, segment.Property));

                            break;
                    }

                    ids.Add(index);
                }

                _indexFile.UpdateFromTransaction(actions);

                StartCachePage(actions);
            }
            catch (IOException ioEx) { Trace.TraceError("Stream in a bad state {0}.", ioEx); throw; }
            catch (AccessViolationException accEx) { Trace.TraceError("Invalid index dbSegment specified {0}.", accEx); throw; }
            catch (JsonSerializationException jsonEx) { Trace.TraceError("Formatter could not format the entity specified {0}.", jsonEx); throw; }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
            finally { lock (_syncOps) _operations.Pop(); }
        }

        public void Rebuild(int newRowSize, int newRows)
        {
            _indexFile.Rebuild(newRowSize, newRows, _indexFile.SeedPosition);
        }

        public long SaveSeed<SeedType>()
        {
            return _indexFile.SaveSeed<Int32>();
        }

        public void Flush()
        {
            Trace.TraceInformation("PrimaryIndex flushing");

            _indexFile.SaveSeed<IdType>();

            lock (_syncFile)
                _indexFile.Reorganize(_propertyConverter, index => index.Value<IdType>("Id"));

            Clear();
        }

        public virtual void Dispose()
        {
            Trace.TraceInformation("Canceling all cache operations.");

            lock (_syncOps)
                _disposeRequested = true;

            Trace.TraceInformation("Wating for all index file operations to complete");

            while (_indexFile.FileFlushQueueActive)
                Thread.Sleep(100);

            Trace.TraceInformation("All index file operations completed");

            Trace.TraceInformation("Wating for all other index operations to complete");

            while (_operations.Count > 0)
                Thread.Sleep(100);

            Trace.TraceInformation("All other index operations completed");

            lock (_syncStaging)
            {
                _indexStaging.Dispose();
            }

            lock (_syncFile)
            {
                if (_indexFile != null)
                    _indexFile.Dispose();

                if (_databaseFile != null)
                    _databaseFile.TransactionCommitted -= new Committed<EntityType>(OnTransactionCommit);
            }
        }
    }
}
