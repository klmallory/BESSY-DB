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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading.Tasks;
using BESSy.Cache;
using BESSy.Extensions;
using BESSy.Parallelization;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using BESSy.Transactions;
using BESSy.Json;
using BESSy.Json.Linq;
using System.Threading;

namespace BESSy.Files
{
    public delegate void Reorganized<EntityType>(int recordsWritten);
    public delegate void Committed<EntityType>(IList<TransactionResult<EntityType>> results, IDisposable transaction);
    public delegate void SaveFailed<EntityType>(SaveFailureInfo<EntityType> saveFailInfo);
    //public delegate void CommitFailed<EntityType>(object trans, object segs, int newRowSize, int newRows);
    public delegate void Rebuild<EntityType>(Guid transactionId, int newStride, int newLength, int newSeedStride);

    public interface IAtomicFileManager<EntityType> : IQueryableFile, IFileManager, IDisposable
    {
        string FileNamePath { get; }
        int MaxLength { get; }
        int SeedPosition { get; }
        int Length { get; }
        int Stride { get; }
        bool FileFlushQueueActive { get; }
        object Seed { get; }
        ISeed<Int32> SegmentSeed { get; }

        int Load<IdType>();
        void ReinitializeSeed<IdType>(int recordsWritten);
        long SaveSeed<IdType>();
        EntityType LoadSegmentFrom(int segment);
        //void SaveSegment(EntityType obj, int segment);
        //int SaveSegment(EntityType obj);
        //void DeleteSegment(int segment);
        IDictionary<IdType, int> CommitTransaction<IdType>(ITransaction<IdType, EntityType> trans, IDictionary<IdType, int> segments);
        void Reorganize<IdType>(IBinConverter<IdType> converter, Func<JObject, IdType> idSelector);
        void Rebuild(Guid transactionId, int newStride, int newLength, int newSeedStride);
        void Rebuild(int newStride, int newLength, int newSeedStride);

        event SaveFailed<EntityType> SaveFailed;
        //event CommitFailed<EntityType> CommitFailed;
        event Committed<EntityType> TransactionCommitted;
        event Rebuild<EntityType> Rebuilt;
        event Reorganized<EntityType> Reorganized;
    }

    public class AtomicFileManager<EntityType> : IAtomicFileManager<EntityType>
    {
        //TODO: this needs to come from the Formatter, not the file manager. That way the delimeter can be specific to the encoding.
        internal readonly static ArraySegment<byte> SeedStart = new ArraySegment<byte>
            (new byte[] { 4, 4, 4, 4, 5, 5, 5, 5, 6 });

        internal readonly static ArraySegment<byte> SegmentDelimeter = new ArraySegment<byte>
            (new byte[] { 2, 2, 2, 2, 3, 3, 3, 3, 4 });

        public AtomicFileManager(string fileNamePath, ISeed<Int32> segmentSeed)
            : this(fileNamePath, segmentSeed, new BSONFormatter())
        { }

        public AtomicFileManager(string fileNamePath,ISeed<Int32> segmentSeed, IQueryableFormatter formatter)
            : this(fileNamePath, Environment.SystemPageSize.Clamp(2048, 8192), segmentSeed, formatter)
        { }

        public AtomicFileManager(string fileNamePath, int bufferSize,ISeed<Int32> segmentSeed, IQueryableFormatter formatter)
            : this(fileNamePath, bufferSize, segmentSeed, formatter, new RowSynchronizer<int>(new BinConverter32()))
        { }

        public AtomicFileManager(string fileNamePath, int bufferSize, ISeed<Int32> segmentSeed, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer)
            : this(fileNamePath, bufferSize, 0, 0, segmentSeed, formatter, rowSynchronizer)
        { }

        public AtomicFileManager(string fileNamePath, int bufferSize, int startingSize, int maximumBlockSize, ISeed<Int32> segmentSeed, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer)
            : this(fileNamePath, bufferSize, startingSize, maximumBlockSize, null, segmentSeed, formatter, rowSynchronizer)
        { }

        //

        public AtomicFileManager(string fileNamePath, object entitySeed, ISeed<Int32> segmentSeed)
            : this(fileNamePath, entitySeed, segmentSeed, new BSONFormatter())
        { }

        public AtomicFileManager(string fileNamePath, object entitySeed, ISeed<Int32> segmentSeed, IQueryableFormatter formatter)
            : this(fileNamePath, Environment.SystemPageSize.Clamp(2048, 8192), entitySeed, segmentSeed, formatter)
        { }

        public AtomicFileManager(string fileNamePath, int bufferSize, object entitySeed, ISeed<Int32> segmentSeed, IQueryableFormatter formatter)
            : this(fileNamePath, bufferSize, entitySeed, segmentSeed, formatter, new RowSynchronizer<int>(new BinConverter32()))
        { }

        public AtomicFileManager(string fileNamePath, int bufferSize, object entitySeed, ISeed<Int32> segmentSeed, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer)
            : this(fileNamePath, bufferSize, 0, 0, entitySeed, segmentSeed, formatter, rowSynchronizer)
        { }

        public AtomicFileManager(string fileNamePath, int bufferSize, int startingSize, int maximumBlockSize, object entitySeed, ISeed<Int32> segmentSeed, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer)
        {
            _segmentSeed = segmentSeed;
            Seed = entitySeed;
            _startingSize = startingSize;
            _maximumBlockSize = maximumBlockSize;
            _bufferSize = bufferSize;
            _formatter = formatter;
            FileNamePath = fileNamePath;
            _rowSynchronizer = rowSynchronizer;
            _lookupGroups = new List<IndexingCPUGroup<int>>();
        }

        protected static int GetStrideFor(int length)
        {
            return (int)(((length / 64) + 1) * 64);
        }

        protected static int GetPositionFor(long length)
        {
            return (int)(((length / 512) + 1) * 512);
        }

        protected static MemoryMappedFile OpenMemoryMap(string fileNamePath, FileStream fileStream)
        {
            var fi = new FileInfo(fileNamePath);
            fi.Directory.Create();

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

        protected static FileStream OpenFile(string fileName, int bufferSize)
        {
            return new FileStream
                 (fileName
                 , FileMode.Open
                 , FileAccess.ReadWrite
                 , FileShare.None
                 , bufferSize, true);
        }

        protected static long GetFileSizeFor(int seedPosition, int length, int stride)
        {
            return (((seedPosition + (length * (long)stride)) / Environment.SystemPageSize) + 1) * Environment.SystemPageSize;
        }

        int _bufferSize;
        int _startingSize;
        

        MemoryMappedFile _fileMap;

        protected object _syncRoot = new object();
        protected string _ioError = "File name {0}, could not be found or accessed: {1}.";
        protected string _serializerError = "File could not be serialized : \r\n {0} \r\n {1}";
        protected Stack<int> _operations = new Stack<int>();

        protected int _maximumBlockSize;
        protected FileStream _fileStream;
        protected IQueryableFormatter _formatter;
        
        protected virtual ISeed<Int32> _segmentSeed { get; set; }
        protected IList<IndexingCPUGroup<int>> _lookupGroups { get; set; }
        protected IRowSynchronizer<int> _rowSynchronizer { get; set; }

        protected virtual int GetMinimumDatabaseSize()
        {
            return 1000000 / Math.Max(512, Stride);
        }

        protected virtual int GetSizeWithGrowthFactor(int length)
        {
            return length + ((int)Math.Ceiling(length * .10)).Clamp(GetMinimumDatabaseSize(), 10240);

        }

        protected virtual void CloseFile()
        {
            if (_fileStream != null || _fileStream.CanWrite)
                SaveSeed();

            if (_fileMap != null)
                _fileMap.Dispose();

            if (_fileStream != null && _fileStream.CanWrite)
            {
                if (_segmentSeed != null)
                    _fileStream.SetLength(SeedPosition + ((Length + 1) * Stride));

                _fileStream.Flush();
                _fileStream.Close();
                _fileStream.Dispose();
            }
        }

        protected virtual void InitializeFileMap()
        {
            if (_fileMap != null)
                _fileMap.Dispose();

            _fileMap = OpenMemoryMap(FileNamePath, _fileStream);

            var groups = TaskGrouping.GetSegmentedTaskGroups(MaxLength, Stride);

            _lookupGroups = TaskGrouping.GetCPUGroupsFor(groups.ToDictionary(g => g, g => g));
        }

        protected virtual void InitializeFileStream(FileStream fileStream, int length)
        {
            MaxLength = GetSizeWithGrowthFactor(length);

            var size = GetFileSizeFor(SeedPosition, MaxLength, Stride);

            fileStream.SetLength(size);
        }

        protected void InitializeFileStream()
        {
            if (_fileStream != null)
                CloseFile();

            _fileStream = OpenFile(FileNamePath, _bufferSize);

            InitializeFileStream(_fileStream, Length);
        }

        protected Stream GetWritableFileStream(int segment, int count)
        {
            return _fileMap.CreateViewStream(SeedPosition + (segment * Stride), count * Stride, MemoryMappedFileAccess.Write);
        }

        protected Stream GetReadableFileStream(int segment, int count)
        {
            return _fileMap.CreateViewStream(SeedPosition + (segment * Stride), count * Stride, MemoryMappedFileAccess.Read);
        }

        public virtual long SaveSeed()
        {
            var seedStream = _formatter.FormatObjStream(Seed);
            
            try
            {
                if (GetPositionFor(seedStream.Length + SegmentDelimeter.Array.Length) > SeedPosition)
                {
                    Rebuild(Stride, Length, GetPositionFor(seedStream.Length + SegmentDelimeter.Array.Length));
                    seedStream = _formatter.FormatObjStream(Seed);
                }

                return SaveSeed(_fileStream, seedStream, SeedPosition);
            }
            finally { if (seedStream != null) seedStream.Dispose(); }
        }

        protected virtual long SaveSeed(FileStream fileStream, int seedStride)
        {
            var seedStream = _formatter.FormatObjStream(Seed);

            return SaveSeed(fileStream, seedStream, seedStride);
        }

        protected virtual long SaveSeed(FileStream fileStream, Stream seedStream, int seedStride)
        {

#if DEBUG
            if (seedStream == null)
                throw new ArgumentNullException("segmentSeed");
#endif
            lock (_syncRoot)
            {
                try
                {
                    seedStream.Position = 0;

                    using (var allLock = _rowSynchronizer.LockAll())
                    {
                        fileStream.Position = 0;

                        fileStream.Write(SeedStart.Array, 0, SeedStart.Array.Length);

                        seedStream.WriteAllTo(fileStream, (int)seedStream.Length);

                        fileStream.Write(SegmentDelimeter.Array, 0, SegmentDelimeter.Array.Length);

                        if (fileStream.Position < seedStride)
                            fileStream.Position = seedStride;

                        var position = fileStream.Position;

                        fileStream.Flush();

                        return position;
                    }

                }
                catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format(_serializerError, jsEx.InnerException, jsEx)); throw; }
                catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
                catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
            }
        }


        protected virtual void InitializeSeedFrom<IdType>(FileStream fileStream)
        {
            var seed  = LoadSeedFrom<IdType>(fileStream);

            //var len = fileStream.Length == 0 ? 0 : (int)((fileStream.Length - SeedPosition) / Stride);

            Stride = seed.Stride;
            SeedPosition = seed.MinimumSeedStride;

            Seed = seed;
        }

        protected virtual void InitializeSeed<IdType>()
        {
            if (Seed == null)
                throw new InvalidOperationException("Entity seed must be used when creating a new database.");

            var seed = (ISeed<IdType>)Seed;

            Stride = seed.Stride;
            SeedPosition = seed.MinimumSeedStride;
        }
        
        public int Pages { get { return _lookupGroups.Count; } }
        public virtual int Length { get { return _segmentSeed.LastSeed; } }
        public bool FileFlushQueueActive { get { return _rowSynchronizer.HasLocks(); } }

        public string FileNamePath { get; protected set; }
        public virtual int SeedPosition { get; protected set;}
        public virtual int Stride { get; protected set; }
        public int MaxLength { get; protected set; }
        public object Seed { get; protected set; }
        public ISeed<Int32> SegmentSeed { get { return _segmentSeed; } }
        public string WorkingPath { get; set; }

        public Stream GetWritableFileStream(string fileNamePath)
        {
            return new FileStream(fileNamePath, FileMode.OpenOrCreate
                , FileAccess.ReadWrite, FileShare.ReadWrite
                , _bufferSize, true);
        }

        public Stream GetReadableFileStream(string fileNamePath)
        {
            return new FileStream(fileNamePath, FileMode.Open
                , FileAccess.Read, FileShare.ReadWrite
                , _bufferSize, true);
        }

        public virtual int Load<IdType>()
        {
            lock (_syncRoot)
            {
                using (_rowSynchronizer.LockAll())
                {
                    var fi = new FileInfo(FileNamePath);

                    if (!fi.Exists)
                    {
                        _fileStream = fi.Create();

                        InitializeSeed<IdType>();

                        InitializeFileStream(_fileStream, 0);
                        InitializeFileMap();

                        SaveSeed<IdType>();
                    }
                    else
                    {
                        using (var fs = fi.Open(FileMode.Open))
                        {
                            fs.Position = 0;

                            InitializeSeedFrom<IdType>(fs);

                            fs.Close();
                        }

                        InitializeFileStream();
                        InitializeFileMap();
                    }

                    if (_maximumBlockSize <= 0)
                        _maximumBlockSize = (Caching.DetermineOptimumCacheSize(Stride) / 2).Clamp(0, MaxLength);

                    return Length;
                }
            }
        }

        /// <summary>
        /// Reinitializes the seed with a new last id.
        /// </summary>
        /// <param name="recordsWritten"></param>
        public virtual void ReinitializeSeed<IdType>(int recordsWritten)
        {
            throw new Exception("WTF");
        }

        /// <summary>
        /// Saves the database's primary Seed
        /// </summary>
        /// <typeparam name="SeedType"></typeparam>
        /// <param name="segmentSeed"></param>
        /// <returns></returns>
        public virtual long SaveSeed<IdType>()
        {
            var seed = (ISeed<IdType>)Seed;

            seed.Stride = this.Stride;
            seed.MinimumSeedStride = this.SeedPosition;

            var seedStream = _formatter.FormatObjStream(seed);

            try
            {
                if (GetPositionFor(seedStream.Length + SegmentDelimeter.Array.Length) > SeedPosition)
                {
                    Rebuild(Stride, Length, GetPositionFor(seedStream.Length + SegmentDelimeter.Array.Length));

                    seed.Stride = this.Stride;
                    seed.MinimumSeedStride = this.SeedPosition;

                    seedStream = _formatter.FormatObjStream(seed);
                }

                return SaveSeed(_fileStream, seedStream, SeedPosition);
            }
            finally { if (seedStream != null) seedStream.Dispose(); }
        }

        /// <summary>
        /// Loads the database's primary segmentSeed.
        /// </summary>
        /// <typeparam name="SeedType"></typeparam>
        /// <param name="fileStream"></param>
        /// <returns></returns>
        public ISeed<IdType> LoadSeedFrom<IdType>(FileStream fileStream)
        {
            lock (_syncRoot)
            {
                try
                {
                    if (fileStream.Length < SegmentDelimeter.Array.Length)
                        return (ISeed<IdType>)(object)_segmentSeed;

                    var pos = fileStream.Position = 0;

                    var match = SegmentDelimeter.Array[0];
                    var delLength = SegmentDelimeter.Array.Length;

                    var buffer = new byte[_bufferSize];

                    int read = fileStream.Read(buffer, 0, buffer.Length);

                    ArraySegment<byte> s = new ArraySegment<byte>(buffer, 0, SeedStart.Array.Length);

                    if (!s.Equals<byte>(SeedStart))
                        return default(Seed<IdType>);

                    pos += (SeedStart.Array.Length);
                    buffer = buffer.Skip((int)pos).ToArray();

                    using (Stream bufferedStream = new MemoryStream())
                    {
                        while (read > SegmentDelimeter.Array.Length)
                        {
                            var index = Array.FindIndex(buffer, b => b == match);

                            while (index >= 0 && index <= buffer.Length - delLength)
                            {
                                ArraySegment<byte> b = new ArraySegment<byte>(buffer, index, delLength);

                                if (b.Equals<byte>(SegmentDelimeter))
                                {
                                    bufferedStream.Write(buffer, 0, index);

                                    fileStream.Position = pos + index + delLength;

                                    var seed = _formatter.UnformatObj<ISeed<IdType>>(bufferedStream);

                                    fileStream.Position += SegmentDelimeter.Array.Length;

                                    if (seed.MinimumSeedStride >= fileStream.Position)
                                        fileStream.Position = seed.MinimumSeedStride;
                                    else
                                        seed.MinimumSeedStride = (int)fileStream.Position;

                                    return seed;
                                }

                                index = Array.FindIndex(buffer, index + 1, n => n == match);
                            }

                            bufferedStream.Write(buffer, 0, buffer.Length - delLength);

                            pos = fileStream.Position -= delLength;
                            read = fileStream.Read(buffer, 0, buffer.Length);
                        }
                    }

                    return default(Seed<IdType>);
                }
                catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format(_serializerError, jsEx.InnerException, jsEx)); throw; }
                catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
                catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
            }
        }

        public virtual IDictionary<IdType, int> CommitTransaction<IdType>(ITransaction<IdType, EntityType> trans, IDictionary<IdType, int> segments)
        {
            Trace.TraceInformation("Filemanager committing transaction");
            int segment = 0;
            object syncBuffer = new object();

            try
            {
                var actions = trans.GetEnlistedActions();
                IDictionary<IdType, Stream> buffers = new Dictionary<IdType, Stream>();
                Dictionary<IdType, int> returnSegments = new Dictionary<IdType, int>();
                IList<TransactionResult<EntityType>> results = new List<TransactionResult<EntityType>>();

                KeyValuePair<IdType, EnlistedAction<IdType, EntityType>>[] updates;
                KeyValuePair<IdType, EnlistedAction<IdType, EntityType>>[] deletes;

                var maxRowSize = 0;

                Parallel.Invoke(new System.Action[]
                {
                    new System.Action(delegate() {

                        updates = actions.Where
                            (a => (a.Value.Action == Action.Create
                            || (a.Value.Action == Action.Update && segments.ContainsKey(a.Key) && segments[a.Key] > -1))).ToArray();

                        if (updates.Count() > 0)
                        {

                            int rem = 0;
                            var count = Math.DivRem(updates.Length, Environment.ProcessorCount, out rem);
                            var groups = Environment.ProcessorCount;

                            if (updates.Length <= Environment.ProcessorCount * 12)
                            {
                                count = updates.Length;
                                groups = 1;
                                rem = 0;
                            }

                            Parallel.For(0, rem > 0 ? groups + 1 : groups, delegate(int g)
                            {
                                var buffer = new Dictionary<IdType, Stream>();

                                if (rem > 0 && g == groups)
                                    for (var i = (g * count); i < (g * count) + rem; i++)
                                        buffer.Add(updates[i].Key, _formatter.FormatObjStream(updates[i].Value.Entity));
                                else
                                    for (var i = (g * count); i < (g * count) + count; i++)
                                        buffer.Add(updates[i].Key, _formatter.FormatObjStream(updates[i].Value.Entity));

                                lock (syncBuffer)
                                {
                                    buffers.Merge(buffer);

                                    maxRowSize = Math.Max(maxRowSize, (int)buffer.Max(b => b.Value.Length));
                                }
                            });
                        }
                    }),
                        new System.Action( delegate() {

                            deletes = actions.Where
                                (a => a.Value.Action == Action.Delete
                                && segments.ContainsKey(a.Key) && segments[a.Key] > -1).ToArray();

                            if (deletes.Length > 0)
                            {
                                var buffer = new Dictionary<IdType, Stream>();

                                foreach (var d in deletes)
                                    buffer.Add(d.Key, new MemoryStream(new byte[Stride]));

                                buffers.Merge(buffer);
                            }
                            })
                    });

                if (buffers.Count == 0)
                    return returnSegments;

                var newRows = actions.Values.Count(a => a.Action == Action.Create);

                using (var lockAll = _rowSynchronizer.LockAll())
                {
                    if (maxRowSize > Stride || newRows + Length >= MaxLength)
                    {
                        Rebuild(Math.Max(maxRowSize, Stride), newRows + Length, SeedPosition);
                    }

                    foreach (var buffer in buffers)
                    {
                        var action = actions[buffer.Key].Action;

                        if (action != Action.Delete && (!segments.ContainsKey(buffer.Key) || segments[buffer.Key] == 0))
                            segment = _segmentSeed.Increment();
                        else
                            segment = segments[buffer.Key];

                        if (segment > MaxLength)
                            throw new InvalidOperationException("Database length is too short for this transaction, rebuild the database first. " + segment + " " + MaxLength);

                        using (var stream = GetWritableFileStream(segment, 1))
                        {
                            try
                            {
                                buffer.Value.WriteAllTo(stream, Stride);

                                stream.Flush();
                            }
                            catch (Exception ex) { Trace.TraceError(segment.ToString()); throw; }
                        }

                        var a = actions[buffer.Key];
                        if (action != Action.Delete)
                        {
                            returnSegments.Add(buffer.Key, segment);
                            results.Add(new TransactionResult<EntityType>(segment, a.Action, a.Entity));
                        }
                        else
                        {
                            _segmentSeed.Open(segment);
                            results.Add(new TransactionResult<EntityType>(segment, Action.Delete, a.Entity));
                        }
                    }

                    InvokeTransactionCommitted<IdType>(results, trans);
                }

                return returnSegments;
            }
            catch (UnauthorizedAccessException unAcEx) { Trace.TraceError(String.Format("Invalid dbSegment specified {0} for file length {1}, {2}", segment, MaxLength, unAcEx)); throw; }
            catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format(_serializerError, jsEx.InnerException, jsEx)); throw; }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
            finally { GC.Collect(); }
        }

        public virtual EntityType LoadSegmentFrom(int segment)
        {
            try
            {
                var entity = default(EntityType);

                if (segment <= MaxLength)
                    using (var readLock = _rowSynchronizer.Lock(segment, FileAccess.Read, FileShare.Read))
                    using (var stream = GetReadableFileStream(segment, 1))
                    {
                        if (_formatter.Trim)
                        {
                            var tmpStream = new MemoryStream();
                            int trim;

                            stream.WriteSegmentToWithTrim(tmpStream, Environment.SystemPageSize, Stride, Stride, out trim);
                            if (trim >= 0)
                                tmpStream.SetLength(trim + 1);

                            _formatter.TryUnformatObj(tmpStream, out entity);
                        }
                        else
                            _formatter.TryUnformatObj(stream, out entity);
                    }

                return entity;
            }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
        }

        public virtual int GetSegmentForPage(int page)
        {
            return _lookupGroups[page].StartSegment;
        }

        public virtual JObject[] GetPage(int page)
        {
            using (_rowSynchronizer.Lock(int.MaxValue, FileAccess.Read, FileShare.ReadWrite))
            {
                List<JObject> queryPage = new List<JObject>();

                //Trace.TraceInformation("Filemanager retreiving page {0}", page);
                IndexingCPUGroup<int> group;

                lock (_syncRoot)
                {
                    if (page >= _lookupGroups.Count)
                        return queryPage.ToArray();

                    group = _lookupGroups[page];

                    if (group.StartSegment > Length)
                        return queryPage.ToArray();
                }

                int trimIndex;

                using (var rowLock = _rowSynchronizer.Lock(new Range<int>(group.StartSegment, group.EndSegment)))
                {
                    var bufferSize = Stride > Environment.SystemPageSize ? Environment.SystemPageSize : Stride;

                    using (var stream = GetReadableFileStream(group.StartSegment + 1, Math.Max(group.EndSegment - (group.StartSegment), 1)))
                    {
                        for (var i = (group.StartSegment + 1); i <= group.EndSegment; i++)
                        {
                            using (var outStream = new MemoryStream())
                            {
                                if (stream.WriteSegmentToWithTrim(outStream, bufferSize, Stride, Stride, out trimIndex))
                                {
                                    if (_formatter.Trim && trimIndex >= 0)
                                        outStream.SetLength(trimIndex + 1);

                                    queryPage.Add(_formatter.Parse(outStream).Add<int>("$segment", i));
                                }
                            }
                        }
                    }
                }
                
                return queryPage.ToArray();
            }
        }
   

        public IEnumerable<JObject[]> AsEnumerable()
        {
            return new QueryEnumerator(this);
        }

        public IEnumerable<JObject[]> AsReverseEnumerable()
        {
            return new ReverseQueryEnumerator(this);
        }

        #region Rebuild

        protected virtual void ReplaceDataFile(string newFileName, int newStride, int newLength, int newSeedStride)
        {
            Trace.TraceInformation("Filemanager replacing datafile");

            using (var lockAll = _rowSynchronizer.LockAll())
            {
                if (_fileStream != null)
                {
                    if (newSeedStride <= SeedPosition)
                        SaveSeed();

                    if (_fileMap != null)
                        _fileMap.Dispose();

                    _fileStream.Flush();

                    if (_segmentSeed != null)
                        _fileStream.SetLength(SeedPosition + ((Length + 1) * Stride));

                    _fileStream.Close();
                    _fileStream.Dispose();
                    _fileStream = null;
                }

                File.Replace(newFileName, FileNamePath, FileNamePath + ".old", true);

                this.Stride = newStride;
                this.SeedPosition = newSeedStride;

                _fileStream = OpenFile(FileNamePath, _bufferSize);

                InitializeFileStream(_fileStream, newLength);
                InitializeFileMap();
            }
        }

        protected virtual void ResizeDataFile(int newLength, int newStride)
        {
            Trace.TraceInformation("Filemanager resizing datafile");

            using (var lockAll = _rowSynchronizer.LockAll())
            {
                if (_fileStream != null)
                {
                    if (_fileMap != null)
                        _fileMap.Dispose();

                    _fileStream.Flush();

                    if (_segmentSeed != null)
                        _fileStream.SetLength(SeedPosition + ((Length + 1) * Stride));

                    _fileStream.Close();
                    _fileStream.Dispose();
                    _fileStream = null;
                }

                this.Stride = newStride;

                _fileStream = OpenFile(FileNamePath, _bufferSize);

                InitializeFileStream(_fileStream, newLength);
                InitializeFileMap();
            }
        }

        protected void CopySegmentTo(int newStride, int newSeedPosition, string newFileName, int startSegment, int endSegment)
        {
            using (var inStream = GetReadableFileStream(startSegment + 1, Math.Max(endSegment - (startSegment), 1)))
            {
                using (var outStream = GetWritableFileStream(newFileName))
                {
                    outStream.Position = newSeedPosition + ((startSegment + 1) * newStride);

                    var bufferSize = Stride > Environment.SystemPageSize ? Environment.SystemPageSize : Stride;

                    for (var i = (startSegment + 1); i <= endSegment; i++)
                    {
                        if (!inStream.WriteSegmentTo(outStream, bufferSize, newStride, Stride))
                            _segmentSeed.Open(i);
                    }

                    outStream.Flush();
                    outStream.Close();
                }

                inStream.Close();
            }
        }

        public virtual void Rebuild(int newStride, int newLength, int newSeedStride)
        {
            Rebuild(Guid.NewGuid(), newStride, newLength, newSeedStride);
        }

        public virtual void Rebuild(Guid transactionId, int newStride, int newLength, int newSeedStride)
        {
            Trace.TraceInformation("Filemanager rebuilding seedposition {0}, rowsize {1}, and length {2}", newSeedStride, newStride, newLength);

            //Lock new database records down.
            using (var rl = _rowSynchronizer.LockAll())
            {
                if (newSeedStride <= 0)
                    newSeedStride = SeedPosition;
                if (newLength <= 0)
                    newLength = MaxLength;
                if (newStride <= 0)
                    newStride = Stride;

                lock (_syncRoot)
                {
                    if ((newStride > Stride && Length > 0 ) || newSeedStride > SeedPosition)
                    {
                        var newFileName = Path.Combine(Path.GetDirectoryName(FileNamePath), transactionId.ToString() + ".rebuild");
                        var fi = new FileInfo(newFileName);
                        if (!fi.Exists)
                            using (var nfs = fi.Create())
                            {
                                SaveSeed(nfs, newSeedStride);
                                nfs.SetLength(GetFileSizeFor(newSeedStride, newLength + 1, newStride));
                                nfs.Flush();
                            }

                        var tasks = TaskGrouping.GetSegmentedTaskGroups(Length + 1, Stride);
                        var taskgroups = new Dictionary<int, int>();

                        tasks.ForEach(t => taskgroups.Add(t, t));

                        var grouping = TaskGrouping.GetCPUGroupsFor<int>(taskgroups);
                        
                        Parallel.ForEach(grouping, delegate(IndexingCPUGroup<int> group)
                            { CopySegmentTo(newStride, newSeedStride, newFileName, group.StartSegment, group.EndSegment); });

                        ReplaceDataFile(newFileName, newStride, newLength, newSeedStride);
                    }
                    else
                    {
                        ResizeDataFile(newLength, newStride);
                    }

                    InvokeRebuilt(transactionId, newStride, newLength, newSeedStride);
                }
            }
        }

        public virtual void Reorganize<IdType>(IBinConverter<IdType> converter, Func<JObject, IdType> idSelector)
        {
            Trace.TraceInformation("Filemanager reorganizing");

            if (Length == 0)
                return;

            int recordsWritten = 0;

            lock (_syncRoot)
            {
                var newFileName = Guid.NewGuid().ToString() + ".reorganize";
                var fi = new FileInfo(newFileName);
                if (!fi.Exists)
                {
                    using (var nfs = fi.Create())
                    {
                        nfs.SetLength(GetFileSizeFor(SeedPosition, Length + 1, Stride));
                        nfs.Flush();
                    }
                }

                using (var lockAll = _rowSynchronizer.LockAll())
                {
                    IdType lastIdWritten = default(IdType);
                    var block = new SortedDictionary<IdType, JObject>(converter);

                    while (recordsWritten <= Length)
                    {
                        for (var page = 0; page < Pages; page++)
                        {
                            foreach (var obj in GetPage(page))
                            {
                                var id = idSelector.Invoke(obj);

                                if (converter.Compare(id, lastIdWritten) <= 0)
                                {
                                    continue; // _segmentSeed.Open(obj.Value<int>("$segment"));
                                }

                                if (block.ContainsKey(id))
                                    block[id] = obj;
                                else
                                    block.Add(id, obj);

                                obj.Remove("$segment");
                            }

                            if (block.Count > _maximumBlockSize)
                                block = new SortedDictionary<IdType, JObject>
                                    (block
                                    .Take(_maximumBlockSize)
                                    .ToDictionary(k => k.Key, k => k.Value), converter);
                        }

                        if (block.Count > 0)
                        {
                            using (var fs = GetWritableFileStream(newFileName))
                            {
                                fs.Position = SeedPosition + (recordsWritten * Stride) + Stride;

                                foreach (var item in block)
                                    using (var s = _formatter.FormatObjStream(item.Value.ToObject<EntityType>(_formatter.Serializer)))
                                        s.WriteAllTo(fs, Stride);

                                fs.Flush();
                            }

                            recordsWritten += block.Count;
                            lastIdWritten = block.Last().Key;
                            
                            block.Clear();
                        }
                        else
                            break;
                    }

                    ReplaceDataFile(newFileName, Stride, Length, SeedPosition);
                    InvokeRebuilt(Guid.Empty, Stride, Length, SeedPosition);
                }
            }

            InvokeReorganized(recordsWritten);
        }

        #endregion

        #region SaveFailed Event

        protected void InvokeSaveFailed(EntityType entity,int segment, int newRowSize, int newDatabaseSize)
        {
            if (SaveFailed != null)
                SaveFailed(new SaveFailureInfo<EntityType>() { Entity = entity, Segment = segment, NewRowSize = GetStrideFor(newRowSize), NewDatabaseSize = newDatabaseSize });
        }

        public event SaveFailed<EntityType> SaveFailed;

        #endregion

        //#region CommitFailed Event

        //private void InvokeCommitFailed(object trans, object segs, int maxRowSize, int newRows)
        //{
        //    if (CommitFailed != null)
        //        CommitFailed(trans, segs, maxRowSize, newRows);
        //}

        //public event CommitFailed<EntityType> CommitFailed;

        //#endregion

        #region Committed Event

        protected void InvokeTransactionCommitted<IdType>(IList<TransactionResult<EntityType>> results, ITransaction<IdType, EntityType> trans)
        {
            if (TransactionCommitted != null)
                TransactionCommitted(results, trans);
        }

        public event Committed<EntityType> TransactionCommitted;

        #endregion

        #region Rebuilt Event

        protected void InvokeRebuilt(Guid transactionId, int newStride, int newLength, int newSeedStride)
        {
            if (Rebuilt != null)
                Rebuilt(transactionId, newStride, newLength, newSeedStride);
        }

        public event Rebuild<EntityType> Rebuilt;

        #endregion

        #region Reorganized Event

        protected void InvokeReorganized(int recordsWritten)
        {
            if (Reorganized  != null)
                Reorganized(recordsWritten);
        }

        public event Reorganized<EntityType> Reorganized;

        #endregion


        public void Dispose()
        {
            lock (_syncRoot)
            {
                while (_rowSynchronizer.HasLocks())
                    Thread.Sleep(100);

                if (_fileStream != null)
                    CloseFile();
            }

            GC.Collect();
        }
    }
}
