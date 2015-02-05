using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BESSy.Enumerators;
using BESSy.Extensions;
using BESSy.Files;
using BESSy.Json.Linq;
using BESSy.Parallelization;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;

namespace BESSy.Cache
{
    public struct NTreeItem<IndexType, SegmentType>
    {
        [TargetedPatchingOptOut("Performance critical to inline this tBuilder of method across NGen image boundaries")]
        public NTreeItem(IndexType index, SegmentType segment)
            : this()
        {
            Index = index;
            Segment = segment;
        }

        public IndexType Index;
        public SegmentType Segment;
    }

    public class NTree<IndexType, EntityType, SegmentType> : IPagedFile<Tuple<long, NTreeItem<IndexType, SegmentType>>>, IDisposable
    {
        [TargetedPatchingOptOut("Performance critical to inline this tBuilder of method across NGen image boundaries")]
        public NTree(string indexPropertyName)
            : this(indexPropertyName, false)
        {

        }

        [TargetedPatchingOptOut("Performance critical to inline this tBuilder of method across NGen image boundaries")]
        public NTree(string indexPropertyName, bool enforceUnique)
            : this(indexPropertyName, enforceUnique, TypeFactory.GetBinConverterFor<IndexType>(), TypeFactory.GetBinConverterFor<SegmentType>(), new RowSynchronizer<int>(new BinConverter32()))
        {

        }

        [TargetedPatchingOptOut("Performance critical to inline this tBuilder of method across NGen image boundaries")]
        public NTree(string indexToken, bool enforceUnique, IBinConverter<IndexType> indexConverter, IBinConverter<SegmentType> segmentConverter, IRowSynchronizer<int> pageSynchronizer)
            : this(indexToken, enforceUnique, indexConverter, segmentConverter, pageSynchronizer, GetIndexer(indexToken))
        {
            _indexHints = new Dictionary<IndexType, long>();
            _segmentHints = new Dictionary<SegmentType, long>();

            _indexToken = indexToken;
            _enforceUnique = enforceUnique;
            _indexConverter = indexConverter;
            _segmentConverter = segmentConverter;

            _hintSkip = 1;

            _indexGet = GetIndexer(indexToken);

            _locationSeed = new Seed64();
            _locationConverter = new BinConverter64();

            _pageSize = TaskGrouping.ReadLimit / (indexConverter.Length + _segmentConverter.Length);

            _pageSync = pageSynchronizer;
        }

        [TargetedPatchingOptOut("Performance critical to inline this tBuilder of method across NGen image boundaries")]
        public NTree(string indexToken, bool enforceUnique, IBinConverter<IndexType> indexConverter, IBinConverter<SegmentType> segmentConverter, IRowSynchronizer<int> pageSynchronizer, Func<EntityType, IndexType> indexGet)
        {
            _indexHints = new Dictionary<IndexType, long>();
            _segmentHints = new Dictionary<SegmentType, long>();

            _indexToken = indexToken;
            _enforceUnique = enforceUnique;
            _indexConverter = indexConverter;
            _segmentConverter = segmentConverter;

            _hintSkip = 1;

            _indexGet = indexGet;

            _locationSeed = new Seed64();
            _locationConverter = new BinConverter64();

            _pageSize = TaskGrouping.ReadLimit / (indexConverter.Length + _segmentConverter.Length);

            _pageSync = pageSynchronizer;
        }

        protected static Func<EntityType, IndexType> GetIndexer(string indexToken)
        {
            if (indexToken != null)
                return (Func<EntityType, IndexType>)Delegate.CreateDelegate(typeof(Func<EntityType, IndexType>), typeof(EntityType).GetProperty(indexToken).GetGetMethod());
            else
                return new Func<EntityType, IndexType>(e => default(IndexType));
        }

        protected object _syncHints = new object();

        protected int _hintSkip = 0;
        protected bool _enforceUnique = false;
        protected long _pageSize = Environment.SystemPageSize;
        protected string _indexToken;

        protected ISeed<long> _locationSeed;
        protected IBinConverter<IndexType> _indexConverter;
        protected IBinConverter<SegmentType> _segmentConverter;
        protected IBinConverter<long> _locationConverter;

        protected IDictionary<IndexType, long> _indexHints = null;
        protected IDictionary<SegmentType, long> _segmentHints = null;
        protected IDictionary<int, IDictionary<long, NTreeItem<IndexType, SegmentType>>> _cache = new Dictionary<int, IDictionary<long, NTreeItem<IndexType, SegmentType>>>();

        protected Func<EntityType, IndexType> _indexGet;

        protected IRowSynchronizer<int> _pageSync;

        protected virtual void UpdateHint(SegmentType segment, IndexType index, long pageNumber)
        {
            lock (_syncHints)
            {
                var containsKey = _segmentHints.ContainsKey(segment);

                if (containsKey && _segmentHints[segment] > pageNumber)
                    _segmentHints[segment] = pageNumber;
                else if(!containsKey)
                    _segmentHints.Add(segment, pageNumber);

                containsKey = _indexHints.ContainsKey(index);

                if (containsKey && _indexHints[index] > pageNumber)
                    _indexHints[index] = pageNumber;
                else if (!containsKey)
                    _indexHints.Add(index, pageNumber);
            }
        }

        protected virtual void SafeIterate(int pageStart, int pageLast, Action<int> action)
        {
            Parallel.For(pageStart, pageLast, new Action<int>(delegate(int pageId)
                {
                    using (_pageSync.Lock(pageId))
                    {
                        action.Invoke(pageId);
                    }
                }));
        }

        protected virtual NTreeItem<IndexType, SegmentType> Get(long location)
        {
            int pageId = 0;

            using(var lck = _pageSync.LockAll())
                pageId = _cache.FirstOrDefault(c => c.Value.ContainsKey(location)).Key;

            if (_cache.Count < pageId)
                throw new KeyNotFoundException(string.Format("ts not found: {0}, page: {1}, actual page count: {2}", location, pageId, _cache.Count));

            if (!_cache.ContainsKey(pageId))
                return default(NTreeItem<IndexType, SegmentType>);

            using (var lck = _pageSync.Lock(pageId))
            {
                var page = _cache[pageId];

                lock (page)
                {
                    if (page.ContainsKey(location))
                        return page[location];
                }
            }

            return default(NTreeItem<IndexType, SegmentType>);
        }

        protected virtual SegmentType GetFirst(IndexType index, out long treeSegment)
        {
            object syncCancel = new object();
            int pageHint = 0;
            int pages = 0;
            bool cancel = false;

            using (var lck = _pageSync.LockAll())
                lock (_syncHints)
                {
                    pageHint = (int)_indexHints.LastOrDefault(hint => _indexConverter.Compare(hint.Key, index) < 0).Value;
                    pages = _cache.Count;
                }

            long loc = 0;
            var seg = default(SegmentType);

            SafeIterate(pageHint, pages, new Action<int>(delegate(int pageId)
                {
                    if (cancel)
                        return;

                    var page = _cache[pageId];

                    if (page.Any(p => _indexConverter.Compare(p.Value.Index, index) == 0))
                    {
                        foreach (var item in page)
                        {
                            if (_indexConverter.Compare(item.Value.Index, index) == 0)
                            {
                                loc = item.Key;
                                seg = item.Value.Segment;
                                lock (syncCancel)
                                    cancel = true;
                                return;
                            }
                        }
                    }
                }));

            treeSegment = loc;
            return seg;
        }

        protected virtual SegmentType[] Get(IndexType index, out long[] locations)
        {
            object syncAdd = new Object();

            var segments = new List<SegmentType>();
            var locs = new List<long>();

            int pageHint = 0;
            int pages = 0;

            using (var lck = _pageSync.LockAll())
                lock (_syncHints)
                {
                    pageHint = (int)_indexHints.LastOrDefault(hint => _indexConverter.Compare(hint.Key, index) < 0).Value;
                    pages = Pages;
                }

            SafeIterate(pageHint, pages, new Action<int>(delegate(int pageId)
            {
                var page = _cache[pageId];
                if (page.Any(p => _indexConverter.Compare(p.Value.Index, index) == 0))
                {
                    foreach (var item in page)
                    {
                        if (_indexConverter.Compare(item.Value.Index, index) == 0)
                        {
                            lock (syncAdd)
                            {
                                locs.Add(item.Key);
                                segments.Add(item.Value.Segment);
                            }
                        }
                    }
                }
            }));

            locations = locs.ToArray();
            return segments.ToArray();
        }

        protected virtual SegmentType[] GetRangeInclusive(IndexType startIndex, IndexType endIndex, out long[] locations)
        {
            object syncAdd = new Object();

            var segments = new List<SegmentType>();
            var locs = new List<long>();

            int pages = Pages;

            SafeIterate(0, pages, new Action<int>(delegate(int pageId)
            {
                var page = _cache[pageId];
                if (page.Any(p => _indexConverter.Compare(p.Value.Index, startIndex) >= 0 && _indexConverter.Compare(p.Value.Index, endIndex) <= 0))
                {
                    foreach (var item in page)
                    {
                        if (_indexConverter.Compare(item.Value.Index, startIndex) >= 0 
                            && _indexConverter.Compare(item.Value.Index, endIndex) <= 0)
                        {
                            lock (syncAdd)
                            {
                                locs.Add(item.Key);
                                segments.Add(item.Value.Segment);
                            }
                        }
                    }
                }
            }));

            locations = locs.ToArray();
            return segments.ToArray();
        }

        protected virtual IndexType GetFirst(SegmentType segment, out long location)
        {
            object syncCancel = new object();
            int pageHint = 0;
            int pages = 0;
            bool cancel = false;

            using (var lck = _pageSync.LockAll())
                lock (_syncHints)
                {
                    pageHint = (int)_segmentHints.LastOrDefault(hint => _segmentConverter.Compare(hint.Key, segment) < 0).Value;
                    pages = _cache.Count;
                }

            long loc = 0;
            var index = default(IndexType);

            SafeIterate(pageHint, pages, new Action<int>(delegate(int pageId)
            {
                if (cancel)
                    return;

                var page = _cache[pageId];
                if (page.Any(p => _segmentConverter.Compare(p.Value.Segment, segment) == 0))
                {
                    foreach (var item in page)
                    {
                        if (_segmentConverter.Compare(item.Value.Segment, segment) == 0)
                        {
                            loc = item.Key;
                            index = item.Value.Index;
                            lock (syncCancel)
                                cancel = true;
                            return;
                        }
                    }
                }
            }));

            location = loc;
            return index;
        }

        protected virtual IndexType[] Get(SegmentType segment, out long[] locations)
        {
            object syncAdd = new Object();

            var indexes = new List<IndexType>();
            var locs = new List<long>();

            int pageHint = 0;
            int pages = 0;

            using (var lck = _pageSync.LockAll())
                lock (_syncHints)
                {
                    pageHint = (int)_segmentHints.LastOrDefault(hint => _segmentConverter.Compare(hint.Key, segment) < 0).Value;
                    pages = _cache.Count;
                }

            SafeIterate(pageHint, pages, new Action<int>(delegate(int pageId)
            {
                var page = _cache[pageId];
                if (page.Any(p => _segmentConverter.Compare(p.Value.Segment, segment) == 0))
                {
                    foreach (var item in page)
                    {
                        if (_segmentConverter.Compare(item.Value.Segment, segment) == 0)
                        {
                            lock (syncAdd)
                            {
                                locs.Add(item.Key);
                                indexes.Add(item.Value.Index);
                            }
                        }
                    }
                }
            }));

            locations = locs.ToArray();
            return indexes.ToArray();
        }

        protected virtual long Push(NTreeItem<IndexType, SegmentType> item)
        {
            IDictionary<long, NTreeItem<IndexType, SegmentType>> page = null;
            var pageId = 0;

            if (Pages < 1)
            {
                using (var lck = _pageSync.LockAll())
                {
                    page = new Dictionary<long, NTreeItem<IndexType, SegmentType>>();
                    _cache.Add(pageId, page);
                }
            }
            else
            {
                using (_pageSync.Lock(Pages - 1))
                {
                    var p = _cache[Pages - 1];

                    if (p.Count >= _pageSize)
                    {
                        using (_pageSync.LockAll())
                        {
                            page = new Dictionary<long, NTreeItem<IndexType, SegmentType>>();
                            pageId = Pages;
                            _cache.Add(Pages, page);
                        }
                    }
                    else
                    {
                        page = p;
                        pageId = Pages - 1;
                    }
                }
            }

            if (page == null)
                throw new InvalidProgramException("Page not found for updating");

            var ts = _locationSeed.Increment();

            page.Add(ts, item);

            if (ts % _hintSkip == 0)
                UpdateHint(item.Segment, item.Index, pageId);

            return ts;
        }

        protected virtual long[] Push(IEnumerable<NTreeItem<IndexType, SegmentType>> items)
        {
            IDictionary<long, NTreeItem<IndexType, SegmentType>> page = null;
            var pageId = 0;

            var tss = new List<long>();
            var group = items.Take((int)_pageSize).ToArray();
            var count = 0;

            if (group.Length < 0)
                return tss.ToArray();

            while (group != null && group.Length > 0)
            {
                var pages = Pages;

                if (pages < 1)
                {
                    page = new Dictionary<long, NTreeItem<IndexType, SegmentType>>();
                    _cache.Add(pageId, page);
                }
                else
                {
                    using (_pageSync.Lock(pages - 1))
                    {
                        var p = _cache[pages - 1];

                        if (p.Count + group.Length > _pageSize)
                        {
                            using (_pageSync.LockAll())
                            {
                                page = new Dictionary<long, NTreeItem<IndexType, SegmentType>>();
                                pageId = pages;
                                _cache.Add(pages, page);
                            }
                        }
                        else
                        {
                            page = p;
                            pageId = pages - 1;
                        }
                    }
                }

                if (page == null)
                    throw new InvalidProgramException("Page not found for updating");

                foreach (var item in group)
                {
                    var ts = _locationSeed.Increment();

                    page.Add(ts, item);

                    if (ts % _hintSkip == 0)
                        UpdateHint(item.Segment, item.Index, pageId);

                    tss.Add(ts);
                }

                count++;

                if (items.Count() > count * _pageSize)
                    group = items.Skip(count * (int)_pageSize).Take((int)_pageSize).ToArray();
                else
                    break;
            }

            return tss.ToArray();
        }

        protected virtual void Push(NTreeItem<IndexType, SegmentType> nt, long ts)
        {
            int pageHint = 0;

            lock (_syncHints)
                pageHint = (int)_indexHints.LastOrDefault(i => _indexConverter.Compare(i.Key, nt.Index) < 0).Value;

            var pages = Pages;

            SafeIterate(pageHint, pages, new Action<int>(delegate(int pageId)
            {
                if (!_cache[pageId].ContainsKey(ts))
                    return;

                var page = _cache[pageId];
                page[ts] = nt;

                if (ts % _hintSkip == 0)
                    UpdateHint(nt.Segment, nt.Index, pageId);
            }));
        }

        protected virtual void Push(List<Tuple<long, NTreeItem<IndexType, SegmentType>>> items)
        {
            var tss = new List<long>();
            //var count = 0;
            var group = items.Take((int)_pageSize).ToArray();
            var pages = Pages;

            SafeIterate(0, pages, new Action<int>(delegate(int pageId)
            {
                var page = _cache[pageId];

                if (page.Select(s => s.Key).Union(items.Select(s => s.Item1))
                    .Distinct().Count() != page.Count + items.Count)
                {
                    foreach (var item in items)
                    {
                        page[item.Item1] = item.Item2;

                        if (item.Item1 % _hintSkip == 0)
                            UpdateHint(item.Item2.Segment, item.Item2.Index, pageId);
                    }
                }
            }));
        }

        protected virtual long[] Pop(IndexType index)
        {
            int pageHint = 0;

            lock (_segmentHints)
                pageHint = (int)_indexHints.LastOrDefault(i => _indexConverter.Compare(i.Key, index) < 0).Value;

            var pages = Pages;

            var locations = new List<long>();

            if (pages <= pageHint)
                throw new KeyNotFoundException(string.Format("page not found: {0}, actual page count: {1}", pageHint, _cache.Count));

            SafeIterate(pageHint, pages, new Action<int>(delegate(int pageId)
            {

                var page = _cache[pageId];
                var match = page.FirstOrDefault(p => _indexConverter.Compare(p.Value.Index, index) == 0);

                if (page.ContainsKey(match.Key))
                {
                    var old = page[match.Key];
                    page[match.Key] = default(NTreeItem<IndexType, SegmentType>);
                    _locationSeed.Open(match.Key);

                    lock (_syncHints)
                        if (_segmentHints.ContainsKey(old.Segment))
                            _segmentHints.Remove(old.Segment);

                    locations.Add(match.Key);
                }
            }));

            lock (_syncHints)
                if (_indexHints.ContainsKey(index))
                    _indexHints.Remove(index);


            return locations.ToArray();
        }

        protected virtual long[] PopFirst(IndexType index)
        {
            int pageHint = 0;

            object syncCancel = new object();
            bool cancel = false;

            lock (_segmentHints)
                pageHint = (int)_indexHints.LastOrDefault(i => _indexConverter.Compare(i.Key, index) < 0).Value;

            var pages = Pages;

            if (pages <= pageHint)
                throw new KeyNotFoundException(string.Format("page not found: {0}, actual page count: {1}", pageHint, _cache.Count));

            var locations = new long[] { 0 };
            SafeIterate(pageHint, pages, new Action<int>(delegate(int pageId)
            {
                if (cancel)
                    return;

                var page = _cache[pageId];
                var match = page.FirstOrDefault(p => _indexConverter.Compare(p.Value.Index, index) == 0);

                if (page.ContainsKey(match.Key))
                {
                    var old = page[match.Key];
                    page[match.Key] = default(NTreeItem<IndexType, SegmentType>);
                    _locationSeed.Open(match.Key);

                    lock (_syncHints)
                        if (_segmentHints.ContainsKey(old.Segment))
                            _segmentHints.Remove(old.Segment);

                    locations = new long[] { match.Key };

                    lock (syncCancel)
                        cancel = true;
                }
            }));

            lock (_syncHints)
                if (_indexHints.ContainsKey(index))
                    _indexHints.Remove(index);

            return locations;
        }

        protected virtual long[] Pop(IEnumerable<IndexType> indexes)
        {
            var locations = new List<long>();

            if (indexes == null || indexes.Count() < 1)
                return locations.ToArray();

            var pages = Pages;

            SafeIterate(0, pages, new Action<int>(delegate(int pageId)
                {
                    var page = _cache[pageId];

                    if (page.Select(s => s.Value.Index).Union(indexes).Distinct().Count() == page.Count + indexes.Count())
                        return;

                    var items = new List<KeyValuePair<long, NTreeItem<IndexType, SegmentType>>>();

                    items.AddRange(page.Where(v => indexes.Contains(v.Value.Index)));

                    foreach (var l in items)
                    {
                        page.Remove(l.Key);
                        _locationSeed.Open(l.Key);

                        lock (_syncHints)
                        {
                            if (_segmentHints.ContainsKey(l.Value.Segment))
                                _segmentHints.Remove(l.Value.Segment);

                            if (_indexHints.ContainsKey(l.Value.Index))
                                _indexHints.Remove(l.Value.Index);
                        }
                    }

                    locations.AddRange(items.Select(i => i.Key));
                }));

            return locations.ToArray();
        }

        protected virtual long[] PopFirst(SegmentType segment)
        {
            int pageHint = 0;

            object syncCancel = new object();
            bool cancel = false;

            lock (_segmentHints)
                pageHint = (int)_segmentHints.LastOrDefault(i => _segmentConverter.Compare(i.Key, segment) < 0).Value;

            var pages = Pages;

            if (pages <= pageHint)
                throw new KeyNotFoundException(string.Format("page not found: {0}, actual page count: {1}", pageHint, _cache.Count));

            var locations = new long[] { 0 };
            SafeIterate(pageHint, pages, new Action<int>(delegate(int pageId)
            {
                if (cancel)
                    return;

                var page = _cache[pageId];
                var match = page.FirstOrDefault(p => _segmentConverter.Compare(p.Value.Segment, segment) == 0);

                if (page.ContainsKey(match.Key))
                {
                    var old = page[match.Key];
                    page[match.Key] = default(NTreeItem<IndexType, SegmentType>);
                    _locationSeed.Open(match.Key);

                    lock (_syncHints)
                        if (_indexHints.ContainsKey(old.Index))
                            _indexHints.Remove(old.Index);

                    locations = new long[] { match.Key };

                    lock (syncCancel)
                        cancel = true;
                }
            }));

            lock (_syncHints)
                if (_segmentHints.ContainsKey(segment))
                    _segmentHints.Remove(segment);

            return locations;
        }

        protected virtual long[] Pop(IEnumerable<SegmentType> segments)
        {
            var locations = new List<long>();

            if (segments == null || segments.Count() < 1)
                return locations.ToArray();

            var pages = Pages;

            SafeIterate(0, pages, new Action<int>(delegate(int pageId)
            {
                var page = _cache[pageId];

                if (page.Select(s => s.Value.Segment).Union(segments).Distinct().Count() == page.Count + segments.Count())
                    return;

                var items = new List<KeyValuePair<long, NTreeItem<IndexType, SegmentType>>>();

                items.AddRange(page.Where(v => segments.Contains(v.Value.Segment)));

                foreach (var l in items)
                {
                    page.Remove(l.Key);
                    _locationSeed.Open(l.Key);

                    lock (_syncHints)
                    {
                        if (_segmentHints.ContainsKey(l.Value.Segment))
                            _segmentHints.Remove(l.Value.Segment);

                        if (_indexHints.ContainsKey(l.Value.Index))
                            _indexHints.Remove(l.Value.Index);
                    }
                }

                locations.AddRange(items.Select(i => i.Key));
            }));


            return locations.ToArray();
        }

        protected virtual void PopLocation(long location)
        {
            KeyValuePair<int, IDictionary<long, NTreeItem<IndexType, SegmentType>>> page;

            using (_pageSync.LockAll())
                page = _cache.FirstOrDefault(c => c.Value.ContainsKey(location));

            using (_pageSync.Lock(page.Key))
            {
                if (page.Value == null || page.Value.Count == 0)
                    return;

                page.Value.Remove(location);

                lock (_syncHints)
                {
                    if (_segmentHints.Values.Contains(location))
                        _segmentHints.Remove(_segmentHints.First(f => f.Value == location).Key);
                    if (_indexHints.Values.Contains(location))
                        _indexHints.Remove(_indexHints.First(f => f.Value == location).Key);
                }
            }
        }

        public virtual Guid Handle { get; protected set; }

        public virtual long[] Delete(IndexType index)
        {
            if (_enforceUnique)
                return PopFirst(index);

            return Pop(index);
        }

        public virtual long[] DeleteBySegment(SegmentType segment)
        {
            if (_enforceUnique)
                return PopFirst(segment);

            return Pop(new SegmentType[] { segment });
        }

        public virtual long[] Delete(EntityType entity)
        {
            try
            {
                var index = _indexGet(entity);

                if (_enforceUnique)
                    return PopFirst(index);

                return Pop(index);
            }
            catch (NullReferenceException) { Trace.TraceError("attempt to delete null entity from NTree"); }

            return new long[0];
        }

        public virtual void DeleteAt(long location)
        {
            PopLocation(location);
        }

        public virtual long[] DeleteMany(IEnumerable<IndexType> indexes)
        {
            return Pop(indexes);
        }

        public virtual long[] DeleteMany(IEnumerable<EntityType> entities)
        {
            try
            {
                return Pop(entities.Select(e => _indexGet(e)).ToArray());
            }
            catch (NullReferenceException) { Trace.TraceError("attempt to delete null entity from NTree"); }

            return new long[0];
        }

        public virtual long AddOrUpdate(IndexType index, SegmentType segment)
        {
            if (_enforceUnique)
            {
                long ts;
                var dupe = GetFirstByIndex(index, out ts);

                if (_segmentConverter.Compare(dupe, segment) == 0)
                {
                    Push(new NTreeItem<IndexType, SegmentType>(index, segment), ts);
                    return ts;
                }
            }
            return Push(new NTreeItem<IndexType, SegmentType>(index, segment));
        }

        public long AddOrUpdate(Tuple<EntityType, SegmentType> entity)
        {
            try
            {
                return AddOrUpdate(_indexGet(entity.Item1), entity.Item2);
            }
            catch (NullReferenceException) { Trace.TraceError("attempt to add null entity to NTree"); }

            return 0;
        }

        public long[] AddOrUpdateRange(IList<Tuple<EntityType, SegmentType>> entities)
        {
            var indexes = new List<NTreeItem<IndexType, SegmentType>>();

            var all = entities.Select(e => e.Item1 != null
                ? new NTreeItem<IndexType, SegmentType>(_indexGet(e.Item1), e.Item2)
                : new NTreeItem<IndexType, SegmentType>())
                .ToList();

            return AddOrUpdateRange(all);
        }

        public long[] AddOrUpdateRange(IList<NTreeItem<IndexType, SegmentType>> items)
        {
            if (items == null || items.Count < 1)
                return new long[0];

            var alreadyAddedItems = new List<Tuple<long, NTreeItem<IndexType, SegmentType>>>();

            if (_enforceUnique)
            {
                var indexes = items.Select(s => s.Index);

                foreach (var ps in (this).AsEnumerable())
                {
                    if (ps.Select(s => s.Item2.Index).Union(indexes).Distinct().Count() == ps.Count() + indexes.Count())
                        continue;

                    foreach (var item in items)
                    {
                        var ts = ps.First(p => _indexConverter.Compare(p.Item2.Index, item.Index) == 0).Item1;

                        alreadyAddedItems.Add(new Tuple<long, NTreeItem<IndexType, SegmentType>>(ts, item));
                    }
                }

                items = items.Except(alreadyAddedItems.Select(t => t.Item2)).ToList();
            }

            Push(alreadyAddedItems);
            var ids = Push(items.ToList()).ToList();

            ids.AddRange(alreadyAddedItems.Select(s => s.Item1));

            return ids.ToArray();
        }

         public IndexType[] GetBySegment(SegmentType segment, out long[] locations)
        {
            List<IndexType> indexes = new List<IndexType>();
            List<long> treeSegs = new List<long>();

            var temp = Get(segment, out locations);

            indexes.AddRange(temp);
            treeSegs.AddRange(locations);

            locations = treeSegs.ToArray();
            return indexes.ToArray();
        }

        public IndexType GetFirstBySegment(SegmentType segment, out long location)
        {
            List<IndexType> indexes = new List<IndexType>();

            var index = GetFirst(segment, out location);

            return index;
        }

        public virtual SegmentType[] GetByIndex(IndexType index, out long[] locations)
        {
            List<SegmentType> segments = new List<SegmentType>();
            List<long> locs = new List<long>();

            var temp = Get(index, out locations);

            segments.AddRange(temp);
            locs.AddRange(locations);

            locations = locs.ToArray();
            return segments.ToArray();
        }

        public virtual SegmentType[] GetByIndexRangeInclusive(IndexType startIndex,IndexType endIndex, out long[] locations)
        {
            return this.GetRangeInclusive(startIndex, endIndex, out locations);
        }

        public virtual SegmentType GetFirstByIndex(IndexType index, out long location)
        {
            List<IndexType> indexes = new List<IndexType>();

            var segment = GetFirst(index, out location);

            return segment;
        }

        public virtual IndexType[] GetBySegment(SegmentType segment)
        {
            long[] ts;
            return GetBySegment(segment, out ts);
        }

        public virtual SegmentType[] GetByIndex(IndexType index)
        {
            long[] ts;
            return GetByIndex(index, out ts);
        }

        public virtual IndexType GetFirstBySegment(SegmentType segment)
        {
            long ts;
            return GetFirstBySegment(segment, out ts);
        }

        public virtual long GetFirstLocationBySegment(SegmentType segment)
        {
            long ts;
            GetFirstBySegment(segment, out ts);
            return ts;
        }

        public virtual SegmentType GetFirstByIndex(IndexType index)
        {
            long ts;
            return GetFirstByIndex(index, out ts);

        }

        public virtual NTreeItem<IndexType, SegmentType> this[long location]
        {
            get
            {
                return Get(location);
            }
            set
            {
                Push(value, location);
            }
        }

        public int Pages
        {
            get
            {
                using (var lck = _pageSync.LockAll())
                    return _cache.Count;
            }
        }

        public long Length
        {
            get
            {
                return _locationSeed.LastSeed;
            }
        }

        #region IPagedFile<NtreeItem<IndexType, SegmentType>> Members

        public Tuple<long, NTreeItem<IndexType, SegmentType>>[] GetPage(int pageId)
        {
            var pageResults = new List<Tuple<long, NTreeItem<IndexType, SegmentType>>>();

            try
            {
                if (pageId > Pages)
                    return pageResults.ToArray();

                using (_pageSync.Lock(pageId))
                {
                    var page = _cache[pageId];

                    pageResults.AddRange(page.Select(s => new Tuple<long, NTreeItem<IndexType, SegmentType>>(s.Key, s.Value)));
                }
                return pageResults.ToArray();
            }
            catch (Exception ex)
            { Trace.TraceError("Error getting page from NTree: {0}", ex); }

            return pageResults.ToArray();
        }

        public IEnumerable<Tuple<long, NTreeItem<IndexType, SegmentType>>[]> AsEnumerable()
        {
            return new PagedEnumerator<Tuple<long, NTreeItem<IndexType, SegmentType>>>(this);
        }

        public IEnumerable<Tuple<long, NTreeItem<IndexType, SegmentType>>[]> AsReverseEnumerable()
        {
            return new PagedReverseEnumerator<Tuple<long, NTreeItem<IndexType, SegmentType>>>(this);
        }

        public Stream GetPageStream(int pageId)
        {
            var pageResults = new MemoryStream();

            try
            {
                if (pageId > Pages)
                    return pageResults;

                using (_pageSync.Lock(pageId))
                {
                    var page = _cache[pageId];
                    foreach (var p in page)
                    {
                        pageResults.Write(_locationConverter.ToBytes(p.Key), 0, _locationConverter.Length);
                        pageResults.Write(_indexConverter.ToBytes(p.Value.Index), 0, _indexConverter.Length);
                        pageResults.Write(_segmentConverter.ToBytes(p.Value.Segment), 0, _segmentConverter.Length);
                    }
                }

                return pageResults;
            }
            catch (Exception ex)
            { Trace.TraceError("Error getting page from NTree: {0}", ex); }

            return pageResults;
        }

        public IEnumerable<Stream> AsStreaming()
        {
            return new PagedStreamingEnumerator(this);
        }

        #endregion

        public virtual void Dispose()
        {

        }
    }
}
