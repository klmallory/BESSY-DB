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
using Newtonsoft.Json.Linq;
using BESSy.Transactions;
using System.IO;
using Newtonsoft.Json;

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

        public int DbSegment { get; set; }
        public int IndexSegment { get; set; }
    }

    public interface IIndex<PropertyType, EntityType, SegType> : IFlush, ISweep, ILoad, IDisposable
    {
        SegType Add(PropertyType property, SegType segment);
        void Update(PropertyType property, SegType segment);
        void Delete(PropertyType property);
        SegType Fetch(PropertyType property);
        PropertyType RidLookup(int dbSegment);
        int Length { get; }
        void Clear();
        void Register(IAtomicFileManager<EntityType> databaseFile);
    }

    public class Index<IndexType, EntityType> : IIndex<IndexType, EntityType, int>
    {
        public Index
            (string fileName
            ,string indexToken
            ,IBinConverter<IndexType> propertyConverter
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

            if (typeof(IndexType) == typeof(Guid))
                _guidWorkaround = true;
        }

        protected object _syncStaging = new object();
        protected object _syncOps = new object();
        protected object _syncCache = new object();
        protected object _syncFile = new object();
        protected object _syncHints = new object();

        protected bool _guidWorkaround;
        protected bool _busy;
        protected string _fileName;
        protected string _indexToken;
        protected IBinConverter<IndexType> _propertyConverter;
        protected IBinConverter<int> _segmentConverter;
        protected IIndexFileManager<IndexType, EntityType, int> _indexFile;
        protected IRepositoryCache<IndexType, SegmentMap<int, int>> _indexCache;
        protected IRepositoryCache<Guid, IDictionary<IndexType, SegmentMap<int, int>>> _indexStaging;

        protected IRepositoryCacheFactory _cacheFactory;
        protected IQueryableFormatter _fileFormatter;
        protected IIndexFileFactory _indexFileFactory;
        protected IRowSynchronizer<int> _rowSynchronizer;
        protected IAtomicFileManager<EntityType> _databaseFile;

        protected IDictionary<IndexType, int> _hints = new Dictionary<IndexType, int>();
        protected Stack<int> _operations = new Stack<int>();

        protected void CachePage(JObject[] page)
        {
            ThreadPool.QueueUserWorkItem
                (new WaitCallback(StartCachePage), page);
        }

        protected void StartCachePage(object state)
        {
            lock (_syncOps)
                _operations.Push(1);

            try
            {
                Trace.TraceInformation("Page caching started");

                var page = (JObject[])state;

                if (page == null) return;

                var block = new Dictionary<IndexType, SegmentMap<int, int>>();

                foreach (var obj in page)
                {
                    var key = obj.Value<IndexType>("Id");

                    if (!block.ContainsKey(key))
                        block.Add(key, new SegmentMap<int, int>(obj.Value<int>("Property"), obj.Value<int>("___segment")));
                }

                lock (_syncStaging)
                    _indexStaging.UpdateCache(Guid.NewGuid(), block, true, false);

                SyncCache(block);
            }
            finally { lock (_syncOps) { _operations.Pop(); } }

            Trace.TraceInformation("Page caching completed");
        }

        protected virtual void SyncCache(IDictionary<IndexType, SegmentMap<int, int>> segments)
        {
            lock (_syncCache)
                foreach (var s in segments)
                    _indexCache.Detach(s.Key);
        }

        protected int GetSegmentFromCache(IndexType prop, out int indexSegment)
        {
            indexSegment = 0;

            if (_indexCache.Contains(prop))
            {
                var c = _indexCache.GetFromCache(prop);
                indexSegment = c.IndexSegment;
                return _indexCache.GetFromCache(prop).DbSegment;
            }

            if (_indexStaging.Count > 0)
            {
                var s = _indexStaging.GetCache().Values.FirstOrDefault(v => v.ContainsKey(prop));

                if (s != null)
                {
                    var c = s[prop];
                    indexSegment = c.IndexSegment;
                    return c.DbSegment;
                }
            }

            return 0;
        }

        protected int GetSegmentFromFile(IndexType prop, out int indexSegment)
        {
            indexSegment = 0;

            var cache = GetSegmentFromCache(prop, out indexSegment);

            if (cache > 0)
                return cache;

            var start = GetPageHint(prop);

            if (start > 0)
            {
                var page = _indexFile.GetPage(start);

                var seg = GetSegmentFromPage(page, prop);

                if (seg > 0)
                    return seg;
            }
            var count = 0;
            foreach (var page in _indexFile.AsEnumerable())
            {
                var seg = GetSegmentFromPage(page, prop);

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

        protected IndexType GetSegmentFromFile(int dbSegemnt, out int indexSegment)
        {
            indexSegment = 0;

            var count = 0;
            foreach (var page in _indexFile.AsEnumerable())
            {
                var prop = GetSegmentFromPage(page, dbSegemnt);

                if (_propertyConverter.Compare(prop, default(IndexType)) != 0)
                {
                    var ps = page.First().Value<IndexType>("Id");

                    lock (_syncHints)
                        if (_hints.Count < TaskGrouping.ArrayLimit)
                            if (!_hints.ContainsKey(ps))
                                _hints.Add(ps, count);

                    return prop;
                }

                count++;
            }

            return default(IndexType);
        }

        protected virtual int GetSegmentFromPage(JObject[] page, IndexType prop)
        {
            JObject index = null;

            index = page.FirstOrDefault(query => _propertyConverter.Compare(query.Value<IndexType>("Id"), prop) == 0);

            var indexSegment = 0;

            if (!object.Equals(index, default(JObject)))
            {
                CachePage(page);
                indexSegment = index.Value<int>("___segment");
                return index.Value<int>("Property");
            }

            return indexSegment;
        }

        protected virtual IndexType GetSegmentFromPage(JObject[] page, int dbSegment)
        {
            JObject index = null;

            index = page.FirstOrDefault(query => _segmentConverter.Compare(query.Value<int>("Property"), dbSegment) == 0);

            var indexSegment = 0;

            if (!object.Equals(index, default(JObject)))
            {
                CachePage(page);
                indexSegment = index.Value<int>("___segment");
                return index.Value<IndexType>("Id");
            }

            return default(IndexType);
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

                _indexFile.Load();

                lock (_syncCache)
                    _indexCache = _cacheFactory.Create<IndexType, SegmentMap<int, int>>(true, Caching.DetermineOptimumCacheSize(_indexFile.Stride), _propertyConverter);

                lock (_syncCache)
                    _indexStaging = _cacheFactory.Create<Guid, IDictionary<IndexType, SegmentMap<int, int>>>(true, TaskGrouping.ArrayLimit, new BinConverterGuid());

                _indexFile.Reorganized += new Reorganized<IndexPropertyPair<IndexType, int>>(OnReorganized);
            }

            InitializeCache();

            return _indexFile.Length;
        }

        protected virtual void InitializeCache()
        {
            Trace.TraceInformation("Index cache initializing");

            lock (_syncHints)
                _hints.Clear();

            lock (_syncCache)
                _indexCache.ClearCache();

            var pages = _indexFile.AsEnumerable().Count().Clamp(1, int.MaxValue);
            var pageCount = (_indexFile.Length / pages).Clamp(1, int.MaxValue);
            var pageSkip = (_indexCache.CacheSize / pageCount).Clamp(1, TaskGrouping.ArrayLimit);
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

        public int Add(IndexType property, int segment)
        {
            var seg = _indexFile.SaveSegment(new IndexPropertyPair<IndexType, int>(property, segment));

            _indexCache.UpdateCache(property, new SegmentMap<int, int>(segment, seg), true, false);

            return seg;
        }

        public void Update(IndexType property, int segment)
        {
            int seg = 0;
            int iSeg = 0;

            seg = GetSegmentFromCache(property, out iSeg);

            if (seg == 0)
                seg = GetSegmentFromFile(property, out iSeg);

            if (seg == 0)
                return;

            _indexFile.SaveSegment(new IndexPropertyPair<IndexType, int>(property, segment), iSeg);
            _indexCache.UpdateCache(property, new SegmentMap<int, int>(segment, iSeg), true, false);
        }

        public void Delete(IndexType property)
        {
            int seg = 0;
            int iSeg = 0;

            seg = GetSegmentFromCache(property, out iSeg);

            if (seg == 0)
                seg = GetSegmentFromFile(property, out iSeg);

            if (seg == 0)
                return;

            _indexFile.DeleteSegment(iSeg);
            _indexCache.UpdateCache(property, new SegmentMap<int, int>(0, 0), true, false);
        }

        public int Fetch(IndexType prop)
        {
            if (_indexCache.Contains(prop))
                return _indexCache.GetFromCache(prop).DbSegment;

            int iSeg = 0;
            var seg = GetSegmentFromFile(prop, out iSeg);

            if (seg > 0)
                return seg;

            return 0;
        }

        public IndexType RidLookup(int dbSegment)
        {
            var cached = _indexCache.GetCache().FirstOrDefault(c => c.Value.DbSegment == dbSegment);

            if (!object.Equals(cached.Key, default(IndexType)))
                return cached.Key;

            int iSeg;
            var prop = GetSegmentFromFile(dbSegment, out iSeg);

            if (!(_propertyConverter.Compare(prop, default(IndexType)) == 0))
                return prop;

            return default(IndexType);
        }

        public int Length
        {
            get { return _indexFile.Length; }
        }

        public void Clear()
        {
            lock (_syncCache)
                _indexCache.ClearCache();

            lock (_syncStaging)
                _indexStaging.ClearCache();

            lock (_syncHints)
                _hints.Clear();
        }

        public void Sweep()
        {
            _indexCache.Sweep();
        }

        public void Register(IAtomicFileManager<EntityType> databaseFile)
        {
            _databaseFile = databaseFile;

            _databaseFile.TransactionCommitted += new Committed<EntityType>(OnTransactionCommit);
            _databaseFile.Reorganized += new Reorganized<EntityType>(OnReorganized);
        }

        protected void OnReorganized()
        {
            //This operation needs to be synchronous.
            Clear();

            var results = new List<TransactionIndexResult<IndexType>>();

            lock (_syncFile)
            {
                lock (_syncOps)
                    _operations.Push(1);

                try
                {
                    var pages = _indexFile.AsEnumerable().Count().Clamp(1, int.MaxValue);
                    var pageCount = (_indexFile.Length / pages).Clamp(1, int.MaxValue);
                    var pageSkip = (_indexCache.CacheSize / pageCount).Clamp(1, TaskGrouping.ArrayLimit);
                    var hintSkip = (pages / TaskGrouping.ArrayLimit).Clamp(1, TaskGrouping.ArrayLimit);

                    var count = 0;
                    foreach (var page in _databaseFile.AsEnumerable())
                    {
                        if (page.Count() <= 0)
                            continue;

                        foreach (var o in page)
                        {
                            var index = o.Value<IndexType>(_indexToken);
                            results.Add(new TransactionIndexResult<IndexType>(index, Action.Update, o.Value<int>("___segment"), Fetch(index)));
                        }

                        _indexFile.UpdateFromTransaction(results, null);

                        if (count % hintSkip == 0)
                            lock (_syncHints)
                            {
                                var id = page[0].Value<IndexType>(_indexToken);

                                if (!_hints.ContainsKey(id))
                                    _hints.Add(id, count);
                            }

                        if (count % pageSkip == 0)
                            CachePage(page);
                    }
                }
                finally { lock (_syncOps) { _operations.Pop(); } }
            }
        }

        protected virtual void OnTransactionCommit(IList<TransactionResult<EntityType>> results, IDisposable transaction)
        {
            Trace.TraceInformation("Index detected transaction committed ");

            var state = new Tuple<IList<TransactionResult<EntityType>>, IDisposable>(results, transaction);
            
            ThreadPool.QueueUserWorkItem(new WaitCallback(UpdateFromTransaction), state);

            int ops = 0;

            lock (_syncOps)
                ops = _operations.Count;

            //give the operation a chance to start for concurrency reasons.
            if (ops <= 0)
                Thread.Sleep(100);
        }

        protected virtual void UpdateFromTransaction(object state)
        {
            lock (_syncOps)
                _operations.Push(1);

            try
            {
                var param = ((Tuple<IList<TransactionResult<EntityType>>, IDisposable>)state);

                var results = param.Item1;
                var transaction = param.Item2 as ITransaction<IndexType, EntityType>;
                var enlisted = transaction.GetEnlistedActions();

                lock (_syncFile)
                    _busy = true;

                var actions = new List<TransactionIndexResult<IndexType>>();

                var index = default(IndexType);

                foreach (var item in results)
                {
                    switch (item.Action)
                    {
                        case Action.Update:
                            index = _indexFile.IndexGet(item.Entity);
                            actions.Add(new TransactionIndexResult<IndexType>(index, item.Action, item.Segment, Fetch(index)));
                            break;
                        case Action.Create:
                            index = _indexFile.IndexGet(item.Entity);
                            actions.Add(new TransactionIndexResult<IndexType>(index, item.Action, item.Segment, 0));
                            break;
                        case Action.Delete:
                            index = RidLookup(item.Segment);
                            actions.Add(new TransactionIndexResult<IndexType>(index, item.Action, item.Segment, Fetch(index)));
                            break;
                    }
                }

                _indexFile.UpdateFromTransaction(actions, transaction);

                var staging = new Dictionary<IndexType, SegmentMap<int, int>>();

                foreach (var a in actions)
                    staging.Add(a.Index, new SegmentMap<int, int>(a.Segment, a.IndexSegment));

                lock (_syncStaging)
                    _indexStaging.UpdateCache(Guid.NewGuid(), staging, true, false);
            }
            catch (IOException ioEx) { Trace.TraceError("Stream in a bad state {0}.", ioEx); throw; }
            catch (AccessViolationException accEx) { Trace.TraceError("Invalid index dbSegment specified {0}.", accEx); throw; }
            catch (JsonSerializationException jsonEx) { Trace.TraceError("Formatter could not format the entity specified {0}.", jsonEx); throw; }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
            finally { lock (_syncOps) _operations.Pop(); }
        }

        //void IndexFileTransactionUpdateComplete(IList<TransactionResult<IndexPropertyPair<IndexType, int>>> results, IDisposable transaction)
        //{
        //    Trace.TraceInformation("Index update complete, begining post update");



        //    Trace.TraceInformation("Index post update complete");
        //}


        public void Flush()
        {
            Trace.TraceInformation("Index flushing");

            lock (_syncFile)
                _indexFile.Reorganize(_propertyConverter, index => index.Value<IndexType>("Id"));

            Clear();
        }

        public virtual void Dispose()
        {
            Trace.TraceInformation("Wating for all index file operations to complete");

            while (_indexFile.FileFlushQueueActive)
                Thread.Sleep(100);

            Trace.TraceInformation("All index file operations completed");

            Trace.TraceInformation("Wating for all other index operations to complete");

            while (_operations.Count > 0)
                Thread.Sleep(100);

            Trace.TraceInformation("All other index operations completed");

            lock (_syncCache)
                if (_indexCache != null)
                    _indexCache.Dispose();

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
