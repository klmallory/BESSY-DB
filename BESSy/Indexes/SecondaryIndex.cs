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
    public struct SecondaryIndexCache<IndexType>
    {
        public IndexType Key;
        public JObject Entity;
    }

    public struct SegmentPair
    {
        public int DbSegment;
        public int IndexSegment;
    }

    public interface ISecondaryIndex<IndexType, EntityType, SegType> : IFlush, ISweep, ILoadAndRegister<EntityType>, IDisposable
    {
        int Length { get; }
        SegType[] Fetch(IndexType property);
        SegType[] FetchBetween(IndexType start, IndexType end);
        int RidLookup(int dbSegment);
        long SaveSeed<IdType>();
        void Clear();
        void Reorganize();
        void Rebuild(int newRowSize, int newRows);
    }

    public class SecondaryIndex<IndexType, EntityType> : ISecondaryIndex<IndexType, EntityType, int>
    {
        public SecondaryIndex
            (string fileName
            , string indexToken
            , IBinConverter<IndexType> propertyConverter
            , IRepositoryCacheFactory cacheFactory
            , IQueryableFormatter fileFormatter
            , IIndexFileFactory indexFileFactory
            , IRowSynchronizer<int> rowSynchonizer)
        {
            _segmentConverter = new BinConverter32();

            _fileName = fileName;
            _indexToken = indexToken;
            _propertyConverter = propertyConverter;
            _cacheFactory = cacheFactory;
            _fileFormatter = fileFormatter;
            _indexFileFactory = indexFileFactory;
            _rowSynchronizer = rowSynchonizer;
        }

        protected object _syncStaging = new object();
        protected object _syncOps = new object();
        protected object _syncCache = new object();
        protected object _syncFile = new object();
        protected object _syncHints = new object();

        protected bool _disposeRequested;
        protected bool _busy;
        protected string _fileName;
        protected string _indexToken;
        protected IBinConverter<IndexType> _propertyConverter;
        protected IBinConverter<int> _segmentConverter;
        protected IIndexFileManager<IndexType, EntityType, int> _indexFile;
        protected IRepositoryCache<Guid, IList<SecondaryIndexCache<IndexType>>> _indexStaging;
        //protected IRepositoryCache<Guid, IDictionary<int, int>> _reverseStaging;

        protected IRepositoryCacheFactory _cacheFactory;
        protected IQueryableFormatter _fileFormatter;
        protected IIndexFileFactory _indexFileFactory;
        protected IRowSynchronizer<int> _rowSynchronizer;
        protected IAtomicFileManager<EntityType> _databaseFile;

        protected IDictionary<IndexType, int> _hints = new Dictionary<IndexType, int>();
        protected Stack<int> _operations = new Stack<int>();

        protected void CachePage(JObject[] page)
        {
            if (_disposeRequested)
                return;

            ThreadPool.QueueUserWorkItem
                (new WaitCallback(StartCachePage), page);
        }

        protected void CachePage(List<TransactionIndexResult<IndexType>> actions)
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
                StartCachePage((List<TransactionIndexResult<IndexType>>)state);

            Trace.TraceInformation("Page caching completed");
        }

        protected void StartCachePage(List<TransactionIndexResult<IndexType>> actions)
        {
            if (actions == null) return;

            //var page = new List<SecondaryIndexCache<IndexType>>();
            //var reverse = new Dictionary<int, int>();

            //var idsToRemove = actions.Where(a => a.Action == Action.Delete).Select(s => s.Index);

            //var reverseCache = _reverseStaging.GetCache();
            //var reverseKeysToRemove = new List<Guid>();

            foreach (var a in actions)
            {
                if (a.Action != Action.Delete)
                {
                    //if (a.Segment <= 0)
                    //    continue;

                    //if (reverse.ContainsKey(a.Segment))
                    //    reverse[a.Segment] = a.IndexSegment;
                    //else
                    //    reverse.Add(a.Segment, a.IndexSegment);
                }
                else
                {
                    //var reverseKeys = reverseCache.Where(c => c.Value.ContainsKey(a.Segment)).Select(c => c.Key);

                    //if (reverseKeys != null && reverseKeys.Count() > 0)
                    //    reverseKeysToRemove.AddRange(reverseKeys);

                    lock (_syncHints)
                        _hints.ToList().RemoveAll(r => _propertyConverter.Compare(r.Key, a.Index) == 0);
                }
            }

            //lock (_syncStaging)
            //{
            //    foreach (var r in reverseKeysToRemove)
            //        _reverseStaging.Detach(r);

            //    var g = Guid.NewGuid();

            //    _reverseStaging.UpdateCache(g, reverse, true, false);
            //}
        }

        protected void StartCachePage(JObject[] page)
        {
            if (page == null) return;

            //var reverse = page.Where(j => j != null && j.HasValues && j.Value<int>("Property") > 0)
            //    .ToDictionary(o => o.Value<int>("Property"), o => o.Value<int>("$segment"));

            //lock (_syncStaging)
            //{
            //    var g = Guid.NewGuid();
            //    _reverseStaging.UpdateCache(g, reverse, true, false);
            //}
        }

        protected IList<SegmentPair> GetSegmentsFromFile(IndexType prop)
        {
            var segments = new List<SegmentPair>();

            var start = GetPageHint(prop);

            var count = 0;

            foreach (var page in _indexFile.AsEnumerable().Skip((start -1).Clamp(0, int.MaxValue)))
            {
                var segs = GetSegmentsFromPage(page, prop);

                if (segs.Count > 0)
                {
                    lock (_syncHints)
                        if (_hints.Count < TaskGrouping.ArrayLimit)
                            if (!_hints.ContainsKey(prop))
                                _hints.Add(prop, count);
                            else if (_hints[prop] > count)
                                _hints[prop] = count;

                    StartCachePage(page);

                    segments.AddRange(segs);
                }

                count++;
            }

            return segments;
        }

        protected int GetSegmentFromFile(int dbSegemnt)
        {
            IndexPropertyPair<IndexType, int> prop;

            var count = 0;

            foreach (var page in _indexFile.AsEnumerable())
            {
                prop = GetSegmentFromPage(page, dbSegemnt);

                if (prop.Property == dbSegemnt)
                {
                    var ps = page.First().Value<IndexType>("Id");

                    lock (_syncHints)
                        if (_hints.Count < TaskGrouping.ArrayLimit)
                            if (!_hints.ContainsKey(ps))
                                _hints.Add(ps, count);
                            else if (_hints[ps] > count)
                                _hints[ps] = count;

                    CachePage(page);

                    return prop.Property;
                }

                count++;
            }

            return 0;
        }

        protected virtual IList<SegmentPair> GetSegmentsFromPage(JObject[] page, IndexType prop)
        {
            var items = page.Where(query => _propertyConverter.Compare(query.Value<IndexType>("Id"), prop) == 0);

            var segments = new List<SegmentPair>();

            if (items.Count() <= 0)
                return segments;

            segments.AddRange
                (items.Where(s => s != null)
                .Select(i => new SegmentPair() 
                { DbSegment = i.Value<int>("Property"), IndexSegment = i.Value<int>("$segment") }));

            return segments;
        }

        protected virtual IndexPropertyPair<IndexType, int> GetSegmentFromPage(JObject[] page, int dbSegment)
        {
            JObject index = null;

            index = page.FirstOrDefault(query => _segmentConverter.Compare(query.Value<int>("Property"), dbSegment) == 0);

            var pair = new IndexPropertyPair<IndexType, int>();

            if (!object.Equals(index, default(JObject)))
            {
                CachePage(page);
                pair.Property = index.Value<int>("$segment");
                pair.Id = index.Value<IndexType>("Id");
            }

            return pair;
        }

        protected int GetPageHint(IndexType prop)
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
                _indexFile = _indexFileFactory.Create<IndexType, EntityType>(_fileName, _indexToken, Environment.SystemPageSize, 0, 0, _propertyConverter, _fileFormatter, _rowSynchronizer);

                _indexFile.Load<IndexType>();

                lock (_syncCache)
                {
                    _indexStaging = _cacheFactory.Create<Guid, IList<SecondaryIndexCache<IndexType>>>(true, TaskGrouping.ArrayLimit, new BinConverterGuid());
                    //_reverseStaging = _cacheFactory.Create<Guid, IDictionary<int, int>>(true, TaskGrouping.ArrayLimit, new BinConverterGuid());
                }

                _indexFile.Reorganized += new Reorganized<IndexPropertyPair<IndexType, int>>(OnReorganized);
            }

            InitializeCache();

            return _indexFile.Length;
        }

        protected virtual void InitializeCache()
        {
            Trace.TraceInformation("PrimaryIndex cache initializing");

            lock (_syncHints)
                _hints.Clear();

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
                        var index = p[0].Value<IndexType>(_indexToken);
                        if (_propertyConverter.Compare(index, default(IndexType)) != 0)
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

        public int[] Fetch(IndexType prop)
        {
            var indexes = new List<int>();

            var segs = GetSegmentsFromFile(prop);

            if (segs.Count <= 0)
                return indexes.ToArray();

            indexes.AddRange(segs.Select(s => s.DbSegment));

            return indexes.ToArray();
        }

        public int[] FetchBetween(IndexType start, IndexType end)
        {
            var segments = new List<int>();

            foreach (var p in _indexFile.AsEnumerable())
            {

                var segs = p.Where(j => _propertyConverter.Compare(j.Value<IndexType>("Id"), start) >= 0
                    && _propertyConverter.Compare(j.Value<IndexType>("Id"), end) <= 0)
                .Select(i => i.Value<int>("Property"));

                if (segs.Count() > 0)
                    StartCachePage(p);

                segments.AddRange(segs);               
            }

            return segments.ToArray();
        }

        public int RidLookup(int dbSegment)
        {
            //lock (_syncStaging)
            //{
            //    var item = _reverseStaging.GetCache().Select(s => s.Value.LastOrDefault(d => d.Key == dbSegment)).LastOrDefault();

            //    if (item.Key == dbSegment)
            //        return item.Value;
            //}

            var prop = GetSegmentFromFile(dbSegment);

            return prop;
        }

        public IDictionary<int, IndexPropertyPair<IndexType, int>> RidLookup(IEnumerable<int> dbSegments)
        {
            var indexes = new Dictionary<int, IndexPropertyPair<IndexType, int>>();

            foreach (var p in _indexFile.AsEnumerable())
            {
                var matches = p.Where(o => dbSegments.Contains(o.Value<int>("Property")));

                foreach (var m in matches)
                    indexes.Add(m.Value<int>("Property"), new IndexPropertyPair<IndexType, int>(m.Value<IndexType>("Id"), m.Value<int>("$segment")));
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

            var results = new List<TransactionIndexResult<IndexType>>();

            lock (_syncFile)
            {
                lock (_syncOps)
                    _operations.Push(2);

                try
                {
                    using (var l = _rowSynchronizer.LockAll())
                    {
                        _indexFile.ReinitializeSeed<IndexType>(recordsWritten);

                        var pages = _indexFile.AsEnumerable().Count().Clamp(1, int.MaxValue);
                        var pageCount = (_indexFile.Length / pages).Clamp(1, int.MaxValue);
                        var pageSkip = (_indexStaging.CacheSize / pageCount).Clamp(1, TaskGrouping.ArrayLimit);
                        var hintSkip = (pages / TaskGrouping.ArrayLimit).Clamp(1, TaskGrouping.ArrayLimit);

                        var count = 0;
                        var pageNumber = -1;

                        foreach (var page in _databaseFile.AsEnumerable())
                        {
                            pageNumber++;

                            if (page.Count() <= 0)
                                continue;

                            results.Clear();

                            foreach (var o in page)
                            {
                                count++;
                                var index = o.Value<IndexType>(_indexToken);
                                var prop = o.Value<int>("Property");
                                var seg = o.Value<int>("$segment");

                                results.Add(new TransactionIndexResult<IndexType>(index, Action.Update, seg, count));
                            }

                            if (results.Count > recordsWritten)
                                throw new InvalidOperationException("Reorganization mismatch in index.");

                            _indexFile.UpdateFromTransaction(results);

                            if (pageNumber % hintSkip == 0)
                            {
                                lock (_syncHints)
                                {
                                    var id = page[0].Value<IndexType>(_indexToken);

                                    if (!_hints.ContainsKey(id))
                                        _hints.Add(id, pageNumber);
                                    else if (_hints[id] > pageNumber)
                                        _hints[id] = pageNumber;
                                }
                            }
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

                var actions = new List<TransactionIndexResult<IndexType>>();

                var index = default(IndexType);
                int indexSegment = 0;

                var deleteIndexes = RidLookup(results.Where(a => a.Action == Action.Delete).Select(s => s.Segment));

                foreach (var item in results)
                {
                    switch (item.Action)
                    {
                        case Action.Update:
                            index = _indexFile.IndexGet(item.Entity);
                            indexSegment = RidLookup(item.Segment);
                            actions.Add(new TransactionIndexResult<IndexType>(index, item.Action, item.Segment, indexSegment));
                            break;
                        case Action.Create:
                            index = _indexFile.IndexGet(item.Entity);
                            actions.Add(new TransactionIndexResult<IndexType>(index, item.Action, item.Segment, 0));
                            break;
                        case Action.Delete:
                            if (!deleteIndexes.ContainsKey(item.Segment))
                                continue;

                            var segment = deleteIndexes[item.Segment];

                            index = segment.Id;

                            if (_propertyConverter.Compare(index, default(IndexType)) == 0)
                                throw new InvalidDataException(string.Format("index not found for db segment: {0}", item.Segment));

                            actions.Add(new TransactionIndexResult<IndexType>(index, item.Action, item.Segment, segment.Property));

                            break;
                    }
                }

                _indexFile.UpdateFromTransaction(actions);

                CachePage(actions);
            }
            catch (IOException ioEx) { Trace.TraceError("Stream in a bad state {0}.", ioEx); throw; }
            catch (AccessViolationException accEx) { Trace.TraceError("Invalid index dbSegment specified {0}.", accEx); throw; }
            catch (JsonSerializationException jsonEx) { Trace.TraceError("Formatter could not format the entity specified {0}.", jsonEx); throw; }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
            finally { lock (_syncOps) _operations.Pop(); }
        }

        public void Reorganize()
        {
            _indexFile.Reorganize<IndexType>(_propertyConverter, j => j.Value<IndexType>(_indexToken));
        }

        public void Rebuild(int newRowSize, int newRows)
        {
            _indexFile.Rebuild(newRowSize, newRows, _indexFile.SeedPosition);
        }

        public long SaveSeed<IdType>()
        {
            return _indexFile.SaveSeed<Int32>();
        }

        public void Flush()
        {
            Trace.TraceInformation("PrimaryIndex flushing");

            _indexFile.SaveSeed<IndexType>();

            lock (_syncFile)
                _indexFile.Reorganize(_propertyConverter, index => index.Value<IndexType>("Id"));

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

            //lock (_syncStaging)
            //    _reverseStaging.Dispose();

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
