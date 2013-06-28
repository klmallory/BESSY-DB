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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace BESSy.Files
{
    public delegate void Reorganized<EntityType>();
    public delegate void Committed<EntityType>(IList<TransactionResult<EntityType>> results, IDisposable transaction);
    public delegate void SaveFailed<EntityType>(SaveFailureInfo<EntityType> saveFailInfo);
    public delegate void CommitFailed<EntityType>(CommitFailureInfo<EntityType> commitFailInfo);

    public interface IAtomicFileManager<EntityType> : IQueryableFile, IFileManager, IDisposable, ILoad
    {
        string FileNamePath { get; }
        int MaxLength { get; }
        int SeedPosition { get; }
        int Length { get; }
        int Stride { get; }
        bool FileFlushQueueActive { get; }
        EntityType LoadSegmentFrom(int segment);
        void SaveSegment(EntityType obj, int segment);
        int SaveSegment(EntityType obj);
        void DeleteSegment(int segment);
        IDictionary<IdType, int> CommitTransaction<IdType>(ITransaction<IdType, EntityType> trans, IDictionary<IdType, int> segments);
        void Reorganize<IdType>(IBinConverter<IdType> converter, Func<JObject, IdType> idSelector);
        void Rebuild(Guid transactionId, int newStride, int newLength, int newSeedStride);
        void Rebuild(int newStride, int newLength, int newSeedStride);

        event SaveFailed<EntityType> SaveFailed;
        //event CommitFailed<EntityType> CommitFailed;
        event Committed<EntityType> TransactionCommitted;
        event Reorganized<EntityType> Reorganized;
    }

    public class AtomicFileManager<EntityType> : IAtomicFileManager<EntityType>
    {
        //TODO: this needs to come from the Formatter, not the file manager. That way the delimeter can be specific to the encoding.
        internal readonly static ArraySegment<byte> SeedStart = new ArraySegment<byte>
            (new byte[] { 4, 4, 4, 4, 4, 4, 4, 4, 4 });

        internal readonly static ArraySegment<byte> SegmentDelimeter = new ArraySegment<byte>
            (new byte[] { 3, 3, 3, 3, 3, 3, 3, 3, 3 });

        public AtomicFileManager(string fileNamePath)
            : this(fileNamePath, Environment.SystemPageSize.Clamp(2048, 8192), new BSONFormatter())
        { }

        public AtomicFileManager(string fileNamePath, IQueryableFormatter formatter)
            : this(fileNamePath, Environment.SystemPageSize.Clamp(2048, 8192), formatter)
        { }

        public AtomicFileManager(string fileNamePath, int bufferSize, IQueryableFormatter formatter)
            : this(fileNamePath, bufferSize, formatter, new RowSynchronizer<int>(new BinConverter32()))
        { }

        public AtomicFileManager(string fileNamePath, int bufferSize, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer)
            : this(fileNamePath, bufferSize, 0, 0, formatter, rowSynchronizer)
        { }

        public AtomicFileManager(string fileNamePath, int bufferSize, int startingSize, int maximumBlockSize, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer)
        {
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

            return MemoryMappedFile.CreateFromFile
                (fileStream
                , fileNamePath + ".mapping"
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
        int _maximumBlockSize;
        
        MemoryMappedFile _fileMap;

        protected object _syncRoot = new object();
        protected string _ioError = "File name {0}, could not be found or accessed: {1}.";
        protected string _serializerError = "File could not be serialized : \r\n {0} \r\n {1}";
        protected Stack<int> _operations = new Stack<int>();

        protected FileStream _fileStream;
        protected IQueryableFormatter _formatter;
        protected virtual ISeed<Int32> _seed { get; set; }
        protected IList<IndexingCPUGroup<int>> _lookupGroups { get; set; }
        protected IRowSynchronizer<int> _rowSynchronizer { get; set; }

        protected virtual int GetMinimumDatabaseSize()
        {
            if (_startingSize > 0)
                return _startingSize;

            var cap = (TaskGrouping.MemoryLimit / (double)Stride);

            return ((int)Math.Floor(cap * 10)).Clamp(1, int.MaxValue);
        }

        protected virtual int GetSizeWithGrowthFactor(int length)
        {
            return length + ((int)Math.Ceiling(length * .10)).Clamp(GetMinimumDatabaseSize(), 10000);
        }

        protected virtual void CloseFile()
        {
            SaveSeed();

            if (_fileMap != null)
                _fileMap.Dispose();

            if (_seed != null)
                _fileStream.SetLength(SeedPosition + ((Length + 1) * Stride));

            _fileStream.Flush();
            _fileStream.Close();
            _fileStream.Dispose();
        }

        protected virtual void InitializeFileMap()
        {
            if (_fileMap != null)
                _fileMap.Dispose();

            _fileMap = OpenMemoryMap(FileNamePath, _fileStream);

            var groups = TaskGrouping.GetSegmentedTaskGroups(MaxLength, Stride);

            _lookupGroups = TaskGrouping.GetCPUGroupsFor(groups.ToDictionary(g => g, g => g));
        }

        protected virtual  void InitializeFileStream(FileStream fileStream, int length)
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

        protected virtual void InitializeSeedFrom(FileStream fileStream)
        {
            _seed = LoadSeedFrom<int>(fileStream);
        }

        protected virtual void InitializeSeed()
        {
            _seed = new Seed32(0);
        }

        protected virtual void ReinitializeSeed(int recordsWritten)
        {
            _seed = new Seed32(recordsWritten);
        }

        protected virtual ISeed<SeedType> GetDefaultSeed<SeedType>()
        {
            return (ISeed<SeedType>)(object)new Seed32();
        }

        protected virtual long SaveSeed()
        {
            var seedStream = _formatter.FormatObjStream(_seed);

            try
            {
                if (GetPositionFor(seedStream.Length + SegmentDelimeter.Array.Length) > SeedPosition)
                {
                    Rebuild(Stride, Length, GetPositionFor(seedStream.Length + SegmentDelimeter.Array.Length));
                    seedStream = _formatter.FormatObjStream(_seed);
                }

                return SaveSeed(_fileStream, seedStream, SeedPosition);
            }
            finally { if (seedStream != null) seedStream.Dispose(); }
        }

        protected virtual long SaveSeed(FileStream fileStream, Stream seedStream, int seedStride)
        {

#if DEBUG
            if (seedStream == null)
                throw new ArgumentNullException("seed");
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

                        seedStream.WriteAllTo(_fileStream);

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

        protected ISeed<IdType> LoadSeedFrom<IdType>(FileStream fileStream)
        {
            lock (_syncRoot)
            {
                try
                {
                    if (fileStream.Length < SegmentDelimeter.Array.Length)
                        return (ISeed<IdType>)(object)_seed;

                    var pos = fileStream.Position = 0;

                    var match = SegmentDelimeter.Array[0];
                    var delLength = SegmentDelimeter.Array.Length;

                    var buffer = new byte[_bufferSize];

                    int read = fileStream.Read(buffer, 0, buffer.Length);

                    ArraySegment<byte> s = new ArraySegment<byte>(buffer, 0, SeedStart.Array.Length);

                    if (!s.Equals<byte>(SeedStart))
                        return GetDefaultSeed<IdType>();

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
                                    //else
                                    //    seed.MinimumSeedStride = (int)fileStream.Position;

                                    return seed;
                                }

                                index = Array.FindIndex(buffer, index + 1, n => n == match);
                            }

                            bufferedStream.Write(buffer, 0, buffer.Length - delLength);

                            pos = fileStream.Position -= delLength;
                            read = fileStream.Read(buffer, 0, buffer.Length);
                        }
                    }

                    return GetDefaultSeed<IdType>();
                }
                catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format(_serializerError, jsEx.InnerException, jsEx)); throw; }
                catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
                catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
            }
        }

        public string FileNamePath { get; protected set; }
        public int MaxLength { get; protected set; }
        public string WorkingPath { get; set; }
        public int Pages { get { return _lookupGroups.Count; } }
        public virtual int SeedPosition { get { return _seed.MinimumSeedStride; } protected set { _seed.MinimumSeedStride = value; } }
        public virtual int Length { get { return _seed.LastSeed; } }
        public virtual int Stride { get { return _seed.Stride; } protected set { _seed.Stride = value; } }
        public bool FileFlushQueueActive { get { return _rowSynchronizer.HasLocks(); } }

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

        public virtual int Load()
        {
            lock (_syncRoot)
            {
                using (_rowSynchronizer.LockAll())
                {
                    var fi = new FileInfo(FileNamePath);

                    if (!fi.Exists)
                    {
                        InitializeSeed();

                        _fileStream = fi.Create();

                        InitializeFileStream(_fileStream, 0);
                        InitializeFileMap();
                    }
                    else
                    {
                        using (var fs = fi.Open(FileMode.Open))
                        {
                            fs.Position = 0;

                            InitializeSeedFrom(fs);

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
        /// Saves the entity at the specified dbSegment position.
        /// </summary>
        /// <param name="obj">The entity to be saved.</param>
        /// <param name="segment"></param>
        /// <returns></returns>
        public virtual void SaveSegment(EntityType obj, int segment)
        {
            try
            {
                using (var inStream = _formatter.FormatObjStream(obj))
                {
                    if (inStream.Length > Stride || segment > MaxLength)
                        InvokeSaveFailed(obj, segment, GetStrideFor((int)inStream.Length), GetSizeWithGrowthFactor(segment));
                    
                        while (segment > _seed.LastSeed)
                            lock (_syncRoot)
                                //open this dbSegment # as available for use.
                                _seed.Open(_seed.Increment());

                    using (var readLock = _rowSynchronizer.Lock(segment, FileAccess.Write, FileShare.Read))
                    {
                        using (var stream = GetWritableFileStream(segment, 1))
                        {
                            inStream.WriteAllTo(stream);

                            stream.Flush();
                        }
                    }
                }
            }
            catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format(_serializerError, jsEx.InnerException, jsEx)); throw; }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
        }

        public virtual int SaveSegment(EntityType obj)
        {
            try
            {
                using (_rowSynchronizer.Lock(int.MaxValue, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var inStream = _formatter.FormatObjStream(obj))
                    {
                        var newRowSize = (int)inStream.Length;

                        lock (_syncRoot)
                        {
                            if (newRowSize > Stride)
                            {
                                InvokeSaveFailed(obj, 0
                                    , newRowSize > Stride ? GetStrideFor(newRowSize) : Stride
                                    , GetSizeWithGrowthFactor(Length));

                                return 0;
                            }
                            else if (_seed.Peek() > MaxLength)
                            {
                                InvokeSaveFailed(obj, 0
                                    , Stride
                                    , GetSizeWithGrowthFactor(Length));
                            }
                        }

                        var nextSeed = _seed.Increment();

                        if (nextSeed > MaxLength)
                            throw new InvalidOperationException("Database length is to short for this transaction, rebuild the database first. " + nextSeed + " " + MaxLength);

                        using (var readLock = _rowSynchronizer.Lock(nextSeed, FileAccess.Write, FileShare.Read))
                        {
                            using (var stream = GetWritableFileStream(nextSeed, 1))
                            {
                                inStream.WriteAllTo(stream);

                                stream.Flush();
                            }
                        }

                        return nextSeed;
                    }
                }
            }
            catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format(_serializerError, jsEx.InnerException, jsEx)); throw; }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
        }

        public virtual void DeleteSegment(int segment)
        {
            try
            {
                if (segment > Length)
                    return;

                using (var readLock = _rowSynchronizer.Lock(segment, FileAccess.Write, FileShare.Read))
                {
                    using (var stream = GetWritableFileStream(segment, 1))
                    {
                        (new MemoryStream(new byte[Stride])).WriteAllTo(stream);

                        stream.Flush();

                        _seed.Open(segment);
                    }
                }
            }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
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

                Parallel.Invoke(
                    new System.Action(delegate
                        {
                            updates = actions.Where
                                (a => (a.Value.Action == Action.Create
                                || (a.Value.Action == Action.Update && segments[a.Key] > -1))).ToArray();

                            if (updates.Count() <= 0)
                                return;

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
                        })
                    , new System.Action(delegate
                        {
                            deletes = actions.Where
                                (a => a.Value.Action == Action.Delete
                                && segments.ContainsKey(a.Key) && segments[a.Key] > -1).ToArray();

                            var buffer = new Dictionary<IdType, Stream>();

                            foreach (var d in deletes)
                                buffer.Add(d.Key, new MemoryStream());

                            lock (syncBuffer)
                                buffers.Merge(buffer);
                        })
                    );

                if (buffers.Count == 0)
                    return returnSegments;

                var newRows = actions.Values.Count(a => a.Action == Action.Create);

                bool fail = false;

                lock (_syncRoot)
                    if (maxRowSize > Stride || newRows >= MaxLength - Length)
                        fail = true;

                if (fail)
                    Rebuild(trans.Id
                        , Math.Max(GetStrideFor(maxRowSize), Stride)
                        , Math.Max(MaxLength, GetSizeWithGrowthFactor(Length + newRows))
                        , SeedPosition);

                using (var lockAll = _rowSynchronizer.LockAll())
                {
                    foreach (var buffer in buffers)
                    {
                        var action = actions[buffer.Key].Action;

                        if (action != Action.Delete && (!segments.ContainsKey(buffer.Key) || segments[buffer.Key] == 0))
                            segment = _seed.Increment();
                        else
                            segment = segments[buffer.Key];

                        if (segment > MaxLength)
                            throw new InvalidOperationException("Database length is to short for this transaction, rebuild the database first. " + segment + " " + MaxLength);

                        using (var stream = GetWritableFileStream(segment, 1))
                        {
                            buffer.Value.WriteAllTo(stream);

                            stream.Flush();
                        }

                        var a = actions[buffer.Key];
                        if (action != Action.Delete)
                        {
                            returnSegments.Add(buffer.Key, segment);
                            results.Add(new TransactionResult<EntityType>(segment, a.Action, a.Entity));
                        }
                        else
                        {
                            _seed.Open(segment);
                            results.Add(new TransactionResult<EntityType>(segment, Action.Delete, a.Entity));
                        }
                    }
                }

                InvokeTransactionCommitted<IdType>(results, trans);

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
                            _formatter.TryUnformatObj(stream, out entity);

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

                using (var rowLock = _rowSynchronizer.Lock(new Range<int>(group.StartSegment, group.EndSegment)))
                {
                    var bufferSize = Stride > Environment.SystemPageSize ? Environment.SystemPageSize : Stride;

                    using (var stream = GetReadableFileStream(group.StartSegment, group.EndSegment - group.StartSegment + 1))
                    {
                        for (var i = group.StartSegment; i <= group.EndSegment; i++)
                        {
                            using (var outStream = new MemoryStream())
                            {
                                if (stream.WriteSegmentTo(outStream, bufferSize, Stride, Stride))
                                    queryPage.Add(_formatter.Parse(outStream).Add<int>("___segment", i));
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
                    if (_fileMap != null)
                        _fileMap.Dispose();

                    _fileStream.Flush();

                    if (_seed != null)
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

        protected void CopySegmentTo(int newStride, int newSeedPosition, string newFileName, int startSegment, int endsegment)
        {
            using (var readLock = _rowSynchronizer.Lock(new Range<int>(startSegment, endsegment), FileAccess.Read, FileShare.None))
            {
                using (var inStream = GetReadableFileStream(startSegment, (endsegment - startSegment) + 1))
                {
                    //inStream.Position = SeedPosition + (startSegment * Stride);

                    using (var outStream = GetWritableFileStream(newFileName))
                    {
                        outStream.Position = newSeedPosition + (startSegment * newStride);

                        var bufferSize = Stride > Environment.SystemPageSize ? Environment.SystemPageSize : Stride;

                        for (var i = startSegment; i <= endsegment; i++)
                        {
                            if (!inStream.WriteSegmentTo(outStream, bufferSize, newStride, Stride))
                                _seed.Open(i);
                        }

                        outStream.Flush();
                        outStream.Close();
                    }

                    inStream.Close();
                }
            }
        }

        //public virtual void Rebuild(int length)
        //{
        //    Trace.TraceInformation("Filemanager rebuilding datafile length {0}", length);

        //    lock (_syncRoot)
        //    {
        //        using (var lockAll = _rowSynchronizer.LockAll())
        //        {
        //            if (_fileStream != null)
        //            {
        //                SaveSeed();

        //                if (_fileMap != null)
        //                    _fileMap.Dispose();

        //                _fileStream.Flush();

        //                if (_seed != null)
        //                    _fileStream.SetLength(SeedPosition + ((length + 1) * Stride));

        //                _fileStream.Close();
        //                _fileStream.Dispose();
        //                _fileStream = null;
        //            }

        //            _fileStream = OpenFile(FileNamePath, _bufferSize);

        //            InitializeFileStream(_fileStream, length);
        //            InitializeFileMap();
        //        }
        //    }

        //    InvokeReorganized();
        //}

        public virtual void Rebuild(int newStride, int newLength, int newSeedStride)
        {
            Rebuild(Guid.NewGuid(), newStride, newLength, newSeedStride);
        }

        public virtual void Rebuild(Guid transactionId, int newStride, int newLength, int newSeedStride)
        {
            Trace.TraceInformation("Filemanager rebuilding seedposition {0}, rowsize {1}, and length {2}", newSeedStride, newStride, newLength);

            using (_rowSynchronizer.Lock(int.MaxValue, FileAccess.Read, FileShare.ReadWrite))
            {
                if (newSeedStride <= 0)
                    newSeedStride = Math.Max(GetPositionFor(_formatter.FormatObj(_seed).Length + SegmentDelimeter.Array.Length), SeedPosition);
                if (newLength <= 0)
                    newLength = MaxLength;
                if (newStride <= 0)
                    newStride = Stride;

                var newFileName = transactionId.ToString() + ".rebuild";
                var fi = new FileInfo(newFileName);
                if (!fi.Exists)
                    using (var nfs = fi.Create())
                    {
                        nfs.SetLength(GetFileSizeFor(newSeedStride, newLength, newStride));
                        nfs.Flush();
                    }

                lock (_syncRoot)
                {
                    if (newStride > Stride || newSeedStride > SeedPosition)
                    {
                        var tasks = TaskGrouping.GetSegmentedTaskGroups(Length, Stride);
                        var taskgroups = new Dictionary<int, int>();

                        tasks.ForEach(t => taskgroups.Add(t, t));

                        var grouping = TaskGrouping.GetCPUGroupsFor<int>(taskgroups);

                        Parallel.ForEach(grouping, delegate(IndexingCPUGroup<int> group)
                        { CopySegmentTo(newStride, newSeedStride, newFileName, group.StartSegment, group.EndSegment); });
                    }

                    ReplaceDataFile(newFileName, newStride, newLength, newSeedStride);
                }
            }
        }

        public virtual void Reorganize<IdType>(IBinConverter<IdType> converter, Func<JObject, IdType> idSelector)
        {
            Trace.TraceInformation("Filemanager reorganizing");

            if (Length == 0)
                return;

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
                    int recordsWritten = 0;
                    IdType lastIdWritten = default(IdType);
                    var block = new SortedDictionary<IdType, JObject>(converter);

                    while (recordsWritten < Length)
                    {
                        for (var page = 0; page < Pages; page++)
                        {
                            foreach (var obj in GetPage(page))
                            {
                                obj.Remove("___segment");

                                var id = idSelector.Invoke(obj);

                                if (converter.Compare(id, lastIdWritten) <= 0)
                                    continue;

                                if (block.ContainsKey(id))
                                    block[id] = obj;
                                else
                                    block.Add(id, obj);
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
                                    using (var s = _formatter.FormatObjStream(item.Value.ToObject<EntityType>()))
                                        s.WriteAllTo(fs, Stride);

                                fs.Flush();
                            }

                            recordsWritten += block.Count;
                            lastIdWritten = block.Last().Key;
                        }

                        block.Clear();
                    }

                    ReinitializeSeed(recordsWritten);
                    ReplaceDataFile(newFileName, Stride, Length, SeedPosition);
                }
            }

            InvokeReorganized();
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

        #region Committed Event

        protected void InvokeTransactionCommitted<IdType>(IList<TransactionResult<EntityType>> results, ITransaction<IdType, EntityType> trans)
        {
            if (TransactionCommitted != null)
                TransactionCommitted(results, trans);
        }

        public event Committed<EntityType> TransactionCommitted;

        #endregion

        #region Reorganized Event

        protected void InvokeReorganized()
        {
            if (Reorganized  != null)
                Reorganized();
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
