using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime;
using System.Security.AccessControl;
using System.Text;
using BESSy.Extensions;
using BESSy.Files;
using BESSy.Parallelization;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Json;
using BESSy.Enumerators;
using System.Threading.Tasks;
using BESSy.Json.Linq;
using System.Threading;
using System.Collections;
using BESSy.Synchronization;
using BESSy.Transactions;

namespace BESSy.Cache
{
    public class PTree<IndexType, EntityType, SegmentType> : NTree<IndexType, EntityType, SegmentType>, IQueryableFile
    {
        [TargetedPatchingOptOut("Performance critical to inline this tBuilder of method across NGen image boundaries")]
        public PTree(string indexPropertyName, string fileName)
            : this(indexPropertyName, fileName, false)
        {

        }

        [TargetedPatchingOptOut("Performance critical to inline this tBuilder of method across NGen image boundaries")]
        public PTree(string indexPropertyName, string fileName, bool enforceUnique)
            : this(indexPropertyName, fileName, enforceUnique, 10240, TypeFactory.GetBinConverterFor<IndexType>(), TypeFactory.GetBinConverterFor<SegmentType>(), new RowSynchronizer<long>(new BinConverter64()), new RowSynchronizer<int>(new BinConverter32()))
        {

        }

        [TargetedPatchingOptOut("Performance critical to inline this tBuilder of method across NGen image boundaries")]
        public PTree(string indexToken, string fileName, bool enforceUnique, int startingSize, IBinConverter<IndexType> indexConverter, IBinConverter<SegmentType> segmentConverter, IRowSynchronizer<long> rowSynchronizer, IRowSynchronizer<int> pageSynchronizer)
            : base(indexToken, enforceUnique, indexConverter, segmentConverter, pageSynchronizer)
        {
            _fileName = fileName;

            _stride = indexConverter.Length + segmentConverter.Length;
            _rowSync = rowSynchronizer;
            _startingSize = startingSize;
        }

        protected string _fileName = null;

        protected MemoryMappedFile _fileMap;
        protected FileStream _fileStream;

        protected long _maxLength;
        protected int _stride;
        protected int _startingSize;

        protected IRowSynchronizer<long> _rowSync;

        protected virtual long GetSizeWithGrowthFactor(long length)
        {
            length.Clamp(_startingSize, long.MaxValue);
            return (long)Math.Ceiling(length * 1.25);
        }

        protected static FileStream OpenFile(string fileName, int bufferSize)
        {
            var fi = new FileInfo(fileName);

            if (!fi.Directory.Exists)
                fi.Directory.Create();

            return new FileStream
                 (fileName
                 , FileMode.OpenOrCreate
                 , FileAccess.ReadWrite
                 , FileShare.None
                 , bufferSize, true);
        }

        protected static MemoryMappedFile OpenMemoryMap(FileStream fileStream, int stride, long maxSize)
        {
            MemoryMappedFileSecurity security = new MemoryMappedFileSecurity();
            security.AddAccessRule
                (new System.Security.AccessControl.AccessRule<MemoryMappedFileRights>
                    ("everyone", MemoryMappedFileRights.FullControl
                    , System.Security.AccessControl.AccessControlType.Allow));

            GC.Collect();

            var handle = Guid.NewGuid().ToString();

            return MemoryMappedFile.CreateFromFile
                (fileStream
                , handle
                , fileStream.Length
                , MemoryMappedFileAccess.ReadWrite //Execute
                , security
                , HandleInheritability.Inheritable
                , true);
        }

        protected virtual void CloseFile()
        {
            if (_fileMap != null)
                _fileMap.Dispose();

            if (_fileStream != null && _fileStream.CanWrite)
            {
                if (_locationSeed != null)
                    _fileStream.SetLength((_locationSeed.LastSeed + 1) * _stride);

                _fileStream.Flush();
                _fileStream.Close();
                _fileStream.Dispose();
            }
        }

        protected virtual void InitializeFile(int stride, long growth = 1)
        {
            _fileStream = OpenFile(_fileName, Environment.SystemPageSize);

            _locationSeed = new Seed64(((_fileStream.Length / stride) -1).Clamp(0, long.MaxValue));

            _maxLength = GetSizeWithGrowthFactor(Math.Max(Length + growth, _startingSize));

            _fileStream.SetLength((_maxLength) * stride);

            _fileMap = OpenMemoryMap(_fileStream, stride, _maxLength);

            lock (_syncHints)
            {
                _hintSkip = (int)((long)Math.Ceiling(_maxLength / (double)TaskGrouping.ArrayLimit) / _pageSize).Clamp(1, int.MaxValue);

                _indexHints.Clear();
                _segmentHints.Clear();
            }
        }

        protected virtual void ResizeFile(long growth)
        {
            using (_rowSync.LockAll())
            {
                CloseFile();

                InitializeFile(_stride, growth);
            }
        }

        protected void PushToFile(long location, NTreeItem<IndexType, SegmentType> item)
        {
            if (Length >= _maxLength)
            {
                using (_rowSync.LockAll())
                {
                    ResizeFile(1);

                    _locationSeed = new Seed64(Math.Max(Length, location));
                }
            }

            using (_rowSync.Lock(location))
            {
                using (var view = GetWritableFileStream(location, 1))
                {
                    view.Write(_indexConverter.ToBytes(item.Index), 0, _indexConverter.Length);
                    view.Write(_segmentConverter.ToBytes(item.Segment), 0, _segmentConverter.Length);
                    view.Flush();
                    view.Close();
                }
            }
        }

        protected void PushToFile(IList<Tuple<long, NTreeItem<IndexType, SegmentType>>> items)
        {
            if (items.Count + Length > _maxLength)
            {
                using (_rowSync.LockAll())
                {
                    ResizeFile(items.Count);

                    _locationSeed = new Seed64(Math.Max(Length, items.Max(i => i.Item1)));
                }
            }

            if (items.Count() < 1)
                return;

            using (_rowSync.Lock(new Range<long>(items.Min(i => i.Item1), items.Max(i => i.Item1))))
            {
                foreach (var item in items)
                {
                    using (var view = GetWritableFileStream(item.Item1, 1))
                    {
                        view.Write(_indexConverter.ToBytes(item.Item2.Index), 0, _indexConverter.Length);
                        view.Write(_segmentConverter.ToBytes(item.Item2.Segment), 0, _segmentConverter.Length);
                        view.Flush();
                        view.Close();
                    }
                }
            }
        }

        protected void PopFromFile(long[] ts)
        {
            if (ts.Length < 1)
                return;

            using (_rowSync.Lock(new Range<long>(ts.Min(i => i), ts.Max(i => i))))
            {
                foreach (var loc in ts)
                {
                    using (var view = GetWritableFileStream(loc, 1))
                    {
                        view.Write(_indexConverter.ToBytes(default(IndexType)), 0, _indexConverter.Length);
                        view.Write(_segmentConverter.ToBytes(default(SegmentType)), 0, _segmentConverter.Length);
                        view.Flush();
                        view.Close();
                    }
                }
            }
        }

        protected override long[] Push(IEnumerable<NTreeItem<IndexType, SegmentType>> items)
        {
            var locations = base.Push(items);

            if (locations.Length < 1)
                return locations;

            var toFile = new List<Tuple<long, NTreeItem<IndexType, SegmentType>>>();
            var array = items.ToArray();

            for (var i = 0; i < array.Length; i++)
                toFile.Add(new Tuple<long, NTreeItem<IndexType, SegmentType>>(locations[i], array[i]));

            PushToFile(toFile);

            return locations;
        }

        protected override void Push(List<Tuple<long, NTreeItem<IndexType, SegmentType>>> items)
        {
            base.Push(items);

            PushToFile(items);
        }

        protected override long Push(NTreeItem<IndexType, SegmentType> item)
        {
            var location = base.Push(item);

            PushToFile(location, item);

            return location;
        }

        protected override void Push(NTreeItem<IndexType, SegmentType> nt, long ts)
        {
            base.Push(nt, ts);

            PushToFile(ts, nt);
        }

        protected override long[] Pop(IEnumerable<IndexType> indexes)
        {
            var ts = base.Pop(indexes);

            PopFromFile(ts);

            return ts;
        }

        protected override long[] Pop(IEnumerable<SegmentType> segments)
        {
            var ts = base.Pop(segments);

            PopFromFile(ts);

            return ts;
        }

        protected override long[] Pop(IndexType index)
        {
            var ts = base.Pop(index);

            PopFromFile(ts);

            return ts;
        }

        protected override long[] PopFirst(IndexType index)
        {
            var ts = base.PopFirst(index);

            PopFromFile(ts);

            return ts;
        }

        protected override long[] PopFirst(SegmentType segment)
        {
            var ts = base.PopFirst(segment);

            PopFromFile(ts);

            return ts;
        }

        protected override void PopLocation(long location)
        {
            base.PopLocation(location);

            PopFromFile(new long[] { location });
        }

        protected Stream GetReadableWritableFileStream(long location, long count)
        {
            return _fileMap.CreateViewStream(location * _stride, count * _stride, MemoryMappedFileAccess.ReadWrite);
        }

        protected Stream GetWritableFileStream(long location, long count)
        {
            return _fileMap.CreateViewStream(location * _stride, count * _stride, MemoryMappedFileAccess.Write);
        }

        protected Stream GetReadableFileStream(long location, long count)
        {
            return _fileMap.CreateViewStream(location * _stride, count * _stride, MemoryMappedFileAccess.Read);
        }

        protected void BuildCacheFromFile()
        {
            using (_rowSync.LockAll())
            {
                if (Length <= 0)
                    return;

                long rem;
                var pages = Math.DivRem(Length, _pageSize, out rem);

                if (rem > 0)
                    pages += 1;

                Parallel.For(0, pages, new Action<long>(delegate(long pageId)
                {
                    long location = 0;
                    var page = new Dictionary<long, NTreeItem<IndexType, SegmentType>>();
                    var count = pageId < pages -1? _pageSize : rem;

                    using (var view = GetReadableFileStream(pageId, count + 1))
                    {
                        while (view.Position < view.Length)
                        {
                            page.Add((pageId * _pageSize) + location,
                                new NTreeItem<IndexType, SegmentType>
                                (_indexConverter.FromStream(view),
                                _segmentConverter.FromStream(view)));

                            location++;
                        }
                    }

                    using (_pageSync.LockAll())
                        _cache.Add((int)pageId, page);
                }));
            }
        }

        public bool FileFlushQueueActive { get { return _rowSync.HasLocks(); } }

        public void Clear()
        {
            using (_rowSync.LockAll())
            {
                _cache = new Dictionary<int, IDictionary<long, NTreeItem<IndexType, SegmentType>>>();

                CloseFile();
            }
        }

        public long Load()
        {
            Trace.TraceInformation("PTree file loading");

            InitializeFile(_stride);

            Trace.TraceInformation("PTree Building Cache");
            
            BuildCacheFromFile();

            return Length;
        }

        public void Rebuild(long newLength)
        {
            ResizeFile(newLength);
        }

        public void Reorganize(IEnumerable<JObject[]> database)
        {
            Trace.TraceInformation("PTree Reorganizing");

            object syncAll = new object();
            bool rebuilding = false;
            var ops = new Stack<long>();

            using (_rowSync.LockAll())
            {
                _locationSeed = new Seed64();

                using (_pageSync.LockAll())
                    _cache = new Dictionary<int, IDictionary<long, NTreeItem<IndexType, SegmentType>>>();

                var cachePage = new Dictionary<long, NTreeItem<IndexType, SegmentType>>();

                foreach (var page in database)
                {
                    if (_maxLength < Length + page.Length)
                    {
                        lock (syncAll)
                            rebuilding = true;

                        while (ops.Count > 0)
                            Thread.Sleep(250);

                        lock (syncAll)
                        {
                            Rebuild(Length + page.Length);
                            rebuilding = false;
                        }
                    }

                    while (rebuilding)
                        Thread.Sleep(250);

                    lock (syncAll)
                        ops.Push(_locationSeed.LastSeed);

                    foreach (var item in page)
                    {
                        var nTree = new NTreeItem<IndexType, SegmentType>();

                        nTree.Index = item.Value<IndexType>(_indexToken);
                        nTree.Segment = item.Value<SegmentType>("$location");

                        if (_indexConverter.Compare(nTree.Index, default(IndexType)) == 0)
                            continue;

                        var loc = _locationSeed.Increment();

                        cachePage.Add(loc, nTree);

                        using (var view = GetWritableFileStream(loc, 1))
                        {
                            view.Write(_indexConverter.ToBytes(nTree.Index), 0, _indexConverter.Length);
                            view.Write(_segmentConverter.ToBytes(nTree.Segment), 0, _segmentConverter.Length);
                        }

                        if (cachePage.Count >= _pageSize)
                        {
                            using (_pageSync.LockAll())
                            {
                                var next = _cache.Count > 0 ? _cache.Max(c => c.Key) + 1 : 0;
                                _cache.Add(next, cachePage);

                                cachePage = new Dictionary<long, NTreeItem<IndexType, SegmentType>>();
                            }
                        }
                    }

                    lock (syncAll)
                        ops.Pop();
                }

                if (cachePage.Count > 0)
                {
                    using (_pageSync.LockAll())
                    {
                        var next = _cache.Count > 0 ? _cache.Max(c => c.Key) + 1 : 0;
                        _cache.Add(next, cachePage);
                    }
                }
            }

            Trace.TraceInformation("PTree Reorganization Complete");
        }

        public void UpdateFromTransaction(ITransaction<EntityType> transaction)
        {
            try
            {
                Trace.TraceInformation("PTree Updating From Transaction");

                IEnumerable<NTreeItem<IndexType, SegmentType>> inserts;
                IEnumerable<SegmentType> deletes;

                var actions = transaction.GetActions();
                inserts = actions.Where(i => i.Action != Action.Delete).Select(s => new NTreeItem<IndexType, SegmentType>(_indexGet(s.Entity), (SegmentType)s.DbSegment));
                deletes = actions.Where(i => i.Action == Action.Delete && i.DbSegment != null).Select(s => s.DbSegment).Cast<SegmentType>();

                Pop(deletes);
                Push(inserts);
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }

            Trace.TraceInformation("PTree Completed Updating From Transaction");
        }

        public virtual long[] PushIndexes(IEnumerable<NTreeItem<IndexType, SegmentType>> items)
        {
            return Push(items);
        }

        public virtual long[] PopIndexes(IEnumerable<IndexType> indexes)
        {
            return Pop(indexes);
        }

        public virtual long[] PopSegments(IEnumerable<SegmentType> segments)
        {
            return Pop(segments);
        }

        #region IPagedFile<JObject> Explicit Members

        JObject[] IPagedFile<JObject>.GetPage(int page)
        {
            var pageResults = new List<JObject>();

            try
            {
                if (page > Pages)
                    return pageResults.ToArray();

                var start = page * (long)_pageSize;
                var end = start + (long)_pageSize - 1;

                var bufferSize = _stride > Environment.SystemPageSize ? Environment.SystemPageSize : _stride;

                using (_rowSync.Lock(new Range<long>(start, end)))
                {
                    using (var view = GetReadableFileStream(start, end))
                    {
                        while (view.Position < view.Length)
                        {
                            var item = new JObject();
                            item.Add<IndexType>("Index", _indexConverter.FromStream(view));
                            item.Add<SegmentType>("Segment", _segmentConverter.FromStream(view));
                            item.Add<long>("$location", start++);

                            pageResults.Add(item);
                        }
                    }
                }

                return pageResults.ToArray();
            }
            catch (Exception ex)
            { Trace.TraceError("Error getting page from PTree: {0}", ex); }

            return pageResults.ToArray();
        }

        IEnumerable<JObject[]> IPagedFile<JObject>.AsEnumerable()
        {
            return new PagedEnumerator<JObject>(this);
        }

        IEnumerable<JObject[]> IPagedFile<JObject>.AsReverseEnumerable()
        {
            return new PagedReverseEnumerator<JObject>(this);
        }

        #endregion

        public override void Dispose()
        {
            using (_rowSync.LockAll())
            {
                CloseFile();
            }
        }
    }
}
