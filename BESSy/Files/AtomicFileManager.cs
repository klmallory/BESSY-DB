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
using BESSy.Enumerators;

namespace BESSy.Files
{
    public delegate void Reorganized<EntityType>(int recordsWritten);
    public delegate void Committed<EntityType>(ITransaction<EntityType> transaction);
    public delegate void SaveFailed<EntityType>(SaveFailureInfo<EntityType> saveFailInfo);
    public delegate void Rebuild<EntityType>(Guid transactionId, int newStride, long newLength, int newSeedStride);

    public interface IAtomicFileManager<EntityType> : IQueryableFile, IFileManager, IDisposable
    {
        string FileNamePath { get; }
        long MaxLength { get; }
        int CorePosition { get; }
        long Length { get; }
        int Stride { get; }
        bool FileFlushQueueActive { get; }
        IFileCore<long> Core { get; }
        ISeed<Int64> SegmentSeed { get; }

        long Load<IdType>();
        void ReinitializeSeed<IdType>(long recordsWritten);
        long SaveCore<IdType>();
        EntityType LoadSegmentFrom(long segment);
        JObject LoadJObjectFrom(long segment);
        IDictionary<IdType, long> CommitTransaction<IdType>(ITransaction<IdType, EntityType> trans, IDictionary<IdType, long> segments);
        void Reorganize<IdType>(IBinConverter<IdType> converter, Func<JObject, IdType> idSelector);
        void Rebuild(Guid transactionId, int newStride, long newLength, int newSeedStride);
        void Rebuild(int newStride, long newLength, int newSeedStride);

        event SaveFailed<EntityType> SaveFailed;
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

        public AtomicFileManager(string fileNamePath)
            : this(fileNamePath, new BSONFormatter())
        { }

        public AtomicFileManager(string fileNamePath,  IQueryableFormatter formatter)
            : this(fileNamePath, Environment.SystemPageSize.Clamp(2048, 8192), formatter)
        { }

        public AtomicFileManager(string fileNamePath, int bufferSize, IQueryableFormatter formatter)
            : this(fileNamePath, bufferSize, formatter, new RowSynchronizer<long>(new BinConverter64()))
        { }

        public AtomicFileManager(string fileNamePath, int bufferSize, IQueryableFormatter formatter, IRowSynchronizer<long> rowSynchronizer)
            : this(fileNamePath, bufferSize, 0, 0, formatter, rowSynchronizer)
        { }

        public AtomicFileManager(string fileNamePath, int bufferSize, int startingSize, int maximumBlockSize, IQueryableFormatter formatter, IRowSynchronizer<long> rowSynchronizer)
            : this(fileNamePath, bufferSize, startingSize, maximumBlockSize, null, formatter, rowSynchronizer)
        { }

        public AtomicFileManager(string fileNamePath,IFileCore<long> core)
            : this(fileNamePath, core, new BSONFormatter())
        { }

        public AtomicFileManager(string fileNamePath, IFileCore<long> core, IQueryableFormatter formatter)
            : this(fileNamePath, Environment.SystemPageSize.Clamp(2048, 8192), core, formatter)
        { }

        public AtomicFileManager(string fileNamePath, int bufferSize, IFileCore<long> core, IQueryableFormatter formatter)
            : this(fileNamePath, bufferSize, core, formatter, new RowSynchronizer<long>(new BinConverter64()))
        { }

        public AtomicFileManager(string fileNamePath, int bufferSize, IFileCore<long> core, IQueryableFormatter formatter, IRowSynchronizer<long> rowSynchronizer)
            : this(fileNamePath, bufferSize, 0, 0, core, formatter, rowSynchronizer)
        { }

        public AtomicFileManager(string fileNamePath, int bufferSize, int startingSize, int maximumBlockSize, IFileCore<long> core, IQueryableFormatter formatter, IRowSynchronizer<long> rowSynchronizer)
        {
            _core = core;
            _startingSize = startingSize;
            _maximumBlockSize = maximumBlockSize;
            _bufferSize = bufferSize;
            _formatter = formatter;
            FileNamePath = fileNamePath;
            _rowSynchronizer = rowSynchronizer;
            _lookupGroups = new List<IndexingCPUGroup>();
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

        protected static long GetFileSizeFor(int seedPosition, long length, int stride)
        {
            return (((seedPosition + (length * stride)) / Environment.SystemPageSize) + 1) * Environment.SystemPageSize;
        }

        int _bufferSize;
        int _startingSize;
        

        MemoryMappedFile _fileMap;
        /// <summary>
        /// Synchronization root
        /// </summary>
        protected object _syncRoot = new object();
        protected string _ioError = "File property {0}, could not be found or accessed: {1}.";
        protected string _serializerError = "File could not be serialized : \r\n {0} \r\n {1}";
        protected Stack<int> _operations = new Stack<int>();

        protected int _maximumBlockSize;
        protected FileStream _fileStream;
        protected IQueryableFormatter _formatter;
        
        protected virtual IFileCore<long> _core { get; set; }
        protected IList<IndexingCPUGroup> _lookupGroups { get; set; }
        protected IRowSynchronizer<long> _rowSynchronizer { get; set; }

        protected virtual int GetMinimumDatabaseSize()
        {
            return Math.Max(1000000 / Math.Max(512, Stride), 12);
        }

        protected virtual long GetSizeWithGrowthFactor(long length)
        {
            return length + (long)(Math.Ceiling(length * .10)).Clamp(12, 10240);
        }

        protected virtual void CloseFile()
        {
            if (_fileStream != null || _fileStream.CanWrite)
                SaveCore();

            if (_fileMap != null)
                _fileMap.Dispose();

            if (_fileStream != null && _fileStream.CanWrite)
            {
                if (_core != null)
                    _fileStream.SetLength(CorePosition + ((long)(Length + 1) * Stride));

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

            _lookupGroups = TaskGrouping.GetCPUGroupsFor(groups);
        }

        protected virtual void InitializeFileStream(FileStream fileStream, long length)
        {
            MaxLength = GetSizeWithGrowthFactor(length);

            FileLength = length;

            var size = GetFileSizeFor(CorePosition, MaxLength + 1, Stride);

            fileStream.SetLength(size);
        }

        protected void InitializeFileStream()
        {
            if (_fileStream != null)
                CloseFile();

            _fileStream = OpenFile(FileNamePath, _bufferSize);

            InitializeFileStream(_fileStream, Length);
        }

        protected Stream GetReadableWritableFileStream(long segment, int count)
        {
            return _fileMap.CreateViewStream(CorePosition + (segment * Stride), count * Stride, MemoryMappedFileAccess.ReadWrite);
        }

        protected Stream GetWritableFileStream(long segment, int count)
        {
            return _fileMap.CreateViewStream(CorePosition + (segment * Stride), count * Stride, MemoryMappedFileAccess.Write);
        }

        protected Stream GetReadableFileStream(long segment, int count)
        {
            return _fileMap.CreateViewStream(CorePosition + (segment * Stride), count * Stride, MemoryMappedFileAccess.Read);
        }

        public virtual long SaveCore()
        {
            var coreStream = _formatter.FormatObjStream(Core);
            
            try
            {
                if (GetPositionFor(coreStream.Length + SegmentDelimeter.Array.Length) > CorePosition)
                {
                    Rebuild(Stride, Length, GetPositionFor(coreStream.Length + SegmentDelimeter.Array.Length));
                    coreStream = _formatter.FormatObjStream(Core);
                }

                return SaveCore(_fileStream, coreStream, CorePosition);
            }
            finally { if (coreStream != null) coreStream.Dispose(); }
        }

        protected virtual long SaveSeed(FileStream fileStream, int seedStride)
        {
            var seedStream = _formatter.FormatObjStream(Core);

            return SaveCore(fileStream, seedStream, seedStride);
        }

        protected virtual long SaveCore(FileStream fileStream, Stream seedStream, int seedStride)
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
                catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
                catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
            }
        }


        protected virtual void InitializeCoreFrom<IdType>(FileStream fileStream)
        {
            var core  = LoadCoreFrom<IdType>(fileStream);

            //var len = fileStream.Length == 0 ? 0 : (int)((fileStream.Length - CorePosition) / Stride);

            Stride = core.Stride;
            CorePosition = core.MinimumCoreStride;

            Core = core;
        }

        protected virtual void InitializeCore<IdType>()
        {
            if (Core == null)
                throw new InvalidOperationException("File core must be used when creating a new database.");

            var core = (IFileCore<IdType, long>)Core;

            Stride = core.Stride;
            CorePosition = core.MinimumCoreStride;
        }
        
        public int Pages { get { return _lookupGroups.Count; } }
        public long FileLength { get; protected set; }
        public virtual long Length { get { return SegmentSeed.LastSeed; } }
        public bool FileFlushQueueActive { get { return _rowSynchronizer.HasLocks(); } }

        public string FileNamePath { get; protected set; }
        public virtual int CorePosition { get; protected set;}
        public virtual int Stride { get; protected set; }
        public long MaxLength { get; protected set; }
        public IFileCore<long> Core { get { return _core;}  protected set { _core = value;} }
        public ISeed<Int64> SegmentSeed { get { return _core.SegmentSeed; } }
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

        public virtual long Load<IdType>()
        {
            lock (_syncRoot)
            {
                using (_rowSynchronizer.LockAll())
                {
                    var fi = new FileInfo(FileNamePath);

                    if (!fi.Exists)
                    {
                        _fileStream = fi.Create();

                        InitializeCore<IdType>();

                        InitializeFileStream(_fileStream, Core.InitialDbSize);
                        InitializeFileMap();

                        SaveCore<IdType>();
                    }
                    else
                    {
                        using (var fs = fi.Open(FileMode.Open))
                        {
                            fs.Position = 0;

                            InitializeCoreFrom<IdType>(fs);

                            fs.Close();
                        }

                        InitializeFileStream();
                        InitializeFileMap();
                    }

                    if (_maximumBlockSize <= 0)
                        _maximumBlockSize = (Caching.DetermineOptimumCacheSize(Stride) / 2).Clamp(0, int.MaxValue);

                    return Length;
                }
            }
        }

        /// <summary>
        /// Reinitializes the core with a new last id.
        /// </summary>
        /// <param property="recordsWritten"></param>
        public virtual void ReinitializeSeed<IdType>(long recordsWritten)
        {
            throw new Exception("WTF");
        }

        /// <summary>
        /// Saves the database'aqn primary Core
        /// </summary>
        /// <typeparam property="SeedType"></typeparam>
        /// <param property="segmentSeed"></param>
        /// <returns></returns>
        public virtual long SaveCore<IdType>()
        {
            var core = (IFileCore<IdType, long>)Core;

            core.Stride = this.Stride;
            core.MinimumCoreStride = this.CorePosition;

            var coreStream = _formatter.FormatObjStream(core);

            try
            {
                if (GetPositionFor(coreStream.Length + SegmentDelimeter.Array.Length) > CorePosition)
                {
                    Rebuild(Stride, Length, GetPositionFor(coreStream.Length + SegmentDelimeter.Array.Length));

                    core.Stride = this.Stride;
                    core.MinimumCoreStride = this.CorePosition;

                    coreStream = _formatter.FormatObjStream(core);
                }

                return SaveCore(_fileStream, coreStream, CorePosition);
            }
            finally { if (coreStream != null) coreStream.Dispose(); }
        }

        /// <summary>
        /// Loads the database'aqn primary segmentSeed.
        /// </summary>
        /// <typeparam property="SeedType"></typeparam>
        /// <param property="fileStream"></param>
        /// <returns></returns>
        public IFileCore<IdType, long> LoadCoreFrom<IdType>(FileStream fileStream)
        {
            lock (_syncRoot)
            {
                try
                {
                    if (fileStream.Length < SegmentDelimeter.Array.Length)
                        return (IFileCore<IdType, long>)(object)_core;

                    var pos = fileStream.Position = 0;

                    var match = SegmentDelimeter.Array[0];
                    var delLength = SegmentDelimeter.Array.Length;

                    var buffer = new byte[_bufferSize];

                    int read = fileStream.Read(buffer, 0, buffer.Length);

                    ArraySegment<byte> s = new ArraySegment<byte>(buffer, 0, SeedStart.Array.Length);

                    if (!s.Equals<byte>(SeedStart))
                        return default(FileCore<IdType, long>);

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

                                    var core = _formatter.UnformatObj<IFileCore<IdType, long>>(bufferedStream);

                                    fileStream.Position += SegmentDelimeter.Array.Length;

                                    if (core.MinimumCoreStride >= fileStream.Position)
                                        fileStream.Position = core.MinimumCoreStride;
                                    else
                                        core.MinimumCoreStride = (int)fileStream.Position;

                                    return core;
                                }

                                index = Array.FindIndex(buffer, index + 1, n => n == match);
                            }

                            bufferedStream.Write(buffer, 0, buffer.Length - delLength);

                            pos = fileStream.Position -= delLength;
                            read = fileStream.Read(buffer, 0, buffer.Length);
                        }
                    }

                    return default(FileCore<IdType, long>);
                }
                catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format(_serializerError, jsEx.InnerException, jsEx)); throw; }
                catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
                catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
            }
        }

        public virtual IDictionary<IdType, long> CommitTransaction<IdType>(ITransaction<IdType, EntityType> trans, IDictionary<IdType, long> segments)
        {
            Trace.TraceInformation("Filemanager committing trans");
            long segment = 0;

            object syncBuffer = new object();
            object syncSegment = new object();
            object syncLock = new object();

            var stride = Stride > 0 ? Stride : 512;

            try
            {
                if (trans.EnlistCount <= 0)
                    return new Dictionary<IdType, long>();

                var returnSegments = new Dictionary<IdType, long>();
                var updateSegments = new Dictionary<IdType, object>();

                using (var locks = new RowLockContainer<long>())
                {
                    var groupSize = (TaskGrouping.TransactionLimit / stride).Clamp(1, trans.EnlistCount);

                    var tGroups = GetGroups(trans.EnlistCount, groupSize);

                    for (var index = 0; index < tGroups; index++)
                    {
                        var actions = trans.GetEnlistedActions(index * groupSize, groupSize);

                        IDictionary<IdType, Stream> buffers = new Dictionary<IdType, Stream>();

                        KeyValuePair<IdType, EnlistedAction<EntityType>>[] creates;
                        KeyValuePair<IdType, EnlistedAction<EntityType>>[] updates;
                        KeyValuePair<IdType, EnlistedAction<EntityType>>[] deletes;

                        var maxRowSize = 0;

                        Parallel.Invoke(new System.Action[]
                        {
                            new System.Action(delegate() {
                                creates = FillCreateBuffers<IdType>(syncBuffer, actions, buffers, ref maxRowSize);
                            }),

                            new System.Action(delegate() {

                                updates = FillUpdateBuffers<IdType>(segments, segment, syncBuffer, locks, actions, buffers, ref maxRowSize);
                            }),

                            new System.Action( delegate() {

                                deletes = FillDeleteBuffers<IdType>(segments, segment, locks, actions, buffers);
                            })
                        });

                        if (buffers.Count > 0)
                        {

                            var newRows = actions.Values.Count(a => a.Action == Action.Create);

                            if (maxRowSize > Stride || newRows + Length >= MaxLength)
                            {
                                Rebuild(Math.Max(maxRowSize, Stride), newRows + Length, CorePosition);
                            }

                            foreach (var buffer in buffers)
                            {
                                var action = actions[buffer.Key].Action;

                                if (action == Action.Create)
                                {
                                    segment = SegmentSeed.Increment();
                                    locks.Add(_rowSynchronizer.Lock(segment));
                                }
                                else
                                    segment = segments[buffer.Key];

                                if (segment > MaxLength)
                                {
                                    lock (_syncRoot)
                                    {
                                        ResizeDataFile(GetSizeWithGrowthFactor(segment), Stride);

                                        foreach (var rl in locks)
                                            _rowSynchronizer.Lock(rl.Rows);
                                    }
                                }

                                using (var stream = GetWritableFileStream(segment, 1))
                                {
                                    try
                                    {
                                        buffer.Value.WriteAllTo(stream, Stride);

                                        stream.Flush();
                                    }
                                    catch (Exception ex) { Trace.TraceError("Error writing location {0} to database: {1}", segment, ex); throw; }
                                }

                                var a = actions[buffer.Key];
                                if (action != Action.Delete)
                                {
                                    returnSegments.Add(buffer.Key, segment);
                                    updateSegments.Add(buffer.Key, segment);
                                }
                                else
                                {
                                    SegmentSeed.Open(segment);

                                    updateSegments.Add(buffer.Key, segment);
                                }
                            }

                            trans.UpdateSegments(updateSegments);
                        }
                    }

                    InvokeTransactionCommitted<IdType>(trans);

                    return returnSegments;
                }
            }
            catch (UnauthorizedAccessException unAcEx) { Trace.TraceError(String.Format("Invalid dbSegment specified {0} for file length {1}, {2}", segment, MaxLength, unAcEx)); throw; }
            catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format(_serializerError, jsEx.InnerException, jsEx)); throw; }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
            finally { GC.Collect(); }
        }

        private KeyValuePair<IdType, EnlistedAction<EntityType>>[] FillDeleteBuffers<IdType>(IDictionary<IdType, long> segments, long segment, RowLockContainer<long> locks, IDictionary<IdType, EnlistedAction<EntityType>> actions, IDictionary<IdType, Stream> buffers)
        {
            KeyValuePair<IdType, EnlistedAction<EntityType>>[] deletes;
            deletes = actions.Where
                (a => a.Value.Action == Action.Delete
                && segments.ContainsKey(a.Key) && segments[a.Key] > -1).ToArray();

            if (deletes.Length > 0)
            {
                var buffer = new Dictionary<IdType, Stream>();

                foreach (var d in deletes)
                {
                    buffer.Add(d.Key, new MemoryStream(new byte[Stride]));

                    RowLock<long> rowLock;

                    if (!_rowSynchronizer.TryLock(segments[d.Key], 5000, out rowLock))
                        throw new RowLockTimeoutException(string.Format("Row deadlock for id {0}, row {1}", d.Key, segment));

                    locks.Add(rowLock);
                }

                buffers.Merge(buffer);
            }
            return deletes;
        }

        private KeyValuePair<IdType, EnlistedAction<EntityType>>[] FillUpdateBuffers<IdType>(IDictionary<IdType, long> segments, long segment, object syncBuffer, RowLockContainer<long> locks, IDictionary<IdType, EnlistedAction<EntityType>> actions, IDictionary<IdType, Stream> buffers, ref int maxRowSize)
        {
            var size = maxRowSize;

            KeyValuePair<IdType, EnlistedAction<EntityType>>[] updates;
            updates = actions.Where
                (a => a.Value.Action == Action.Update).ToArray();

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
                        {
                            buffer.Add(updates[i].Key, _formatter.FormatObjStream(updates[i].Value.Entity));

                            RowLock<long> rowLock;

                            if (!_rowSynchronizer.TryLock(segments[updates[i].Key], 5000, out rowLock))
                                throw new RowLockTimeoutException(string.Format("Row deadlock for id {0}, row {1}", updates[i].Key, segment));

                            locks.Add(rowLock);
                        }
                    else
                        for (var i = (g * count); i < (g * count) + count; i++)
                        {
                            buffer.Add(updates[i].Key, _formatter.FormatObjStream(updates[i].Value.Entity));

                            RowLock<long> rowLock;

                            if (!_rowSynchronizer.TryLock(segments[updates[i].Key], 5000, out rowLock))
                                throw new RowLockTimeoutException(string.Format("Row deadlock for id {0}, row {1}", updates[i].Key, segment));

                            locks.Add(rowLock);
                        }

                    lock (syncBuffer)
                    {
                        buffers.Merge(buffer);

                        size = Math.Max(size, (int)buffer.Max(b => b.Value.Length));
                    }


                });
            }

            maxRowSize = size;

            return updates;
        }

        private KeyValuePair<IdType, EnlistedAction<EntityType>>[] FillCreateBuffers<IdType>(object syncBuffer, IDictionary<IdType, EnlistedAction<EntityType>> actions, IDictionary<IdType, Stream> buffers, ref int maxRowSize)
        {
            var size = maxRowSize;

            KeyValuePair<IdType, EnlistedAction<EntityType>>[] creates;
            creates = actions.Where
            (a => (a.Value.Action == Action.Create)).ToArray();

            if (creates.Count() > 0)
            {
                int rem = 0;
                var count = Math.DivRem(creates.Length, Environment.ProcessorCount, out rem);
                var groups = Environment.ProcessorCount;

                if (creates.Length <= Environment.ProcessorCount * 12)
                {
                    count = creates.Length;
                    groups = 1;
                    rem = 0;
                }

                Parallel.For(0, rem > 0 ? groups + 1 : groups, delegate(int g)
                {
                    var buffer = new Dictionary<IdType, Stream>();

                    if (rem > 0 && g == groups)
                        for (var i = (g * count); i < (g * count) + rem; i++)
                            buffer.Add(creates[i].Key, _formatter.FormatObjStream(creates[i].Value.Entity));
                    else
                        for (var i = (g * count); i < (g * count) + count; i++)
                            buffer.Add(creates[i].Key, _formatter.FormatObjStream(creates[i].Value.Entity));

                    lock (syncBuffer)
                    {
                        buffers.Merge(buffer);

                        size = Math.Max(size, (int)buffer.Max(b => b.Value.Length));
                    }
                });
            }

            maxRowSize = size;

            return creates;
        }

        private static int GetGroups(int count, int groupSize)
        {
            int tRem = 0;
            var tGroups = groupSize > 0 ? Math.DivRem(count, groupSize, out tRem) : 1;

            if (tRem > 0)
                tGroups++;
            return tGroups;
        }

        public virtual EntityType LoadSegmentFrom(long segment)
        {
            try
            {
                var entity = default(EntityType);

                if (segment <= MaxLength)
                    using (_rowSynchronizer.Lock(segment, FileAccess.Read, FileShare.Read))
                    {
                        using (var stream = GetReadableFileStream(segment, 1))
                        {
                            if (_formatter.Trim)
                            {
                                var tmpStream = new MemoryStream();
                                int trim;

                                stream.WriteSegmentToWithTrim(tmpStream, Environment.SystemPageSize, Stride, Stride, out trim);
                                if (trim >= 0)
                                    if (_formatter.TrimTerms > 0)
                                    tmpStream.SetLength(trim + (trim % _formatter.TrimTerms));
                                    else
                                        tmpStream.SetLength(trim + 1);

                                _formatter.TryUnformatObj(tmpStream, out entity);
                            }
                            else
                            {
                                _formatter.TryUnformatObj(stream, out entity);
                            }
                        }
                    }
                return entity;
            }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
        }

        public virtual JObject LoadJObjectFrom(long segment)
        {
            try
            {
                JObject entity = new JObject();

                if (segment <= MaxLength)
                    using (_rowSynchronizer.Lock(segment, FileAccess.Read, FileShare.Read))
                    {
                        using (var stream = GetReadableFileStream(segment, 1))
                        {
                            if (_formatter.Trim)
                            {
                                var tmpStream = new MemoryStream();
                                int trim;

                                stream.WriteSegmentToWithTrim(tmpStream, Environment.SystemPageSize, Stride, Stride, out trim);
                                if (trim >= 0)
                                    tmpStream.SetLength(trim + 1);

                                _formatter.TryParse(tmpStream, out entity);
                            }
                            else
                            {
                                _formatter.TryParse(stream, out entity);
                            }
                        }
                    }
                return entity;
            }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
        }
        public virtual long GetSegmentForPage(int page)
        {
            return _lookupGroups[page].StartSegment;
        }

        public virtual JObject[] GetPage(int page)
        {
            List<JObject> queryPage = new List<JObject>();

            //Trace.TraceInformation("Filemanager retreiving page {0}", page);
            IndexingCPUGroup group;

            lock (_syncRoot)
            {
                if (page >= _lookupGroups.Count)
                    return queryPage.ToArray();

                group = _lookupGroups[page];

                if (group.StartSegment > Length)
                    return queryPage.ToArray();
            }

            int trimIndex;

            using (var rowLock = _rowSynchronizer.Lock(new Range<long>(group.StartSegment, group.EndSegment)))
            {
                var bufferSize = Stride > Environment.SystemPageSize ? Environment.SystemPageSize : Stride;

                using (var stream = GetReadableFileStream(group.StartSegment + 1, (int)Math.Max(group.EndSegment - (group.StartSegment), 1)))
                {
                    for (var i = (group.StartSegment + 1); i <= group.EndSegment; i++)
                    {
                        using (var outStream = new MemoryStream())
                        {
                            if (stream.WriteSegmentToWithTrim(outStream, bufferSize, Stride, Stride, out trimIndex))
                            {
                                if (_formatter.Trim && trimIndex >= 0)
                                    outStream.SetLength(trimIndex + 1);

                                outStream.Position = 0;

                                try
                                {
                                    queryPage.Add(_formatter.Parse(outStream).Add<long>("$location", i));
                                }
                                catch (JsonReaderException jsEx)
                                {
                                    Trace.TraceError("Error parsing database page: {0},\r\n {1}", page, jsEx);
                                }
                            }
                        }
                    }
                }
            }

            return queryPage.ToArray();
        }

        public virtual Stream GetPageStream(int page)
        {
            var outStream = new MemoryStream();

            using (_rowSynchronizer.Lock(int.MaxValue, FileAccess.Read, FileShare.ReadWrite))
            {
                List<JObject> queryPage = new List<JObject>();

                //Trace.TraceInformation("Filemanager retreiving page {0}", page);
                IndexingCPUGroup group;

                lock (_syncRoot)
                {
                    if (page >= _lookupGroups.Count)
                        return outStream;

                    group = _lookupGroups[page];

                    if (group.StartSegment > Length)
                        return outStream;
                }

                int trimIndex = 0;
                var lastLength = 0L;

                using (var rowLock = _rowSynchronizer.Lock(new Range<long>(group.StartSegment, group.EndSegment)))
                {
                    var bufferSize = Stride > Environment.SystemPageSize ? Environment.SystemPageSize : Stride;

                    using (var stream = GetReadableFileStream(group.StartSegment + 1, (int)Math.Max(group.EndSegment - (group.StartSegment), 1)))
                    {
                        for (var i = (group.StartSegment + 1); i <= group.EndSegment; i++)
                        {
                            lastLength = outStream.Length;
                            if (stream.WriteSegmentToWithTrim(outStream, bufferSize, Stride, Stride, out trimIndex))
                            {
                                try
                                {
                                    if (_formatter.Trim && trimIndex >= 0)
                                        outStream.SetLength(lastLength + trimIndex + 1);
                                }
                                catch (Exception ex)
                                {
                                    Trace.TraceError("Error streaming database page: {0}, \r\n{1}", page, ex);
                                }
                            }
                        }
                    }
                }

                return outStream;
            }
        }

        public IEnumerable<JObject[]> AsEnumerable()
        {
            return new PagedEnumerator<JObject>(this);
        }

        public IEnumerable<JObject[]> AsReverseEnumerable()
        {
            return new PagedReverseEnumerator<JObject>(this);
        }

        public IEnumerable<Stream> AsStreaming()
        {
            return new PagedStreamingEnumerator(this);
        }

        #region Rebuild

        protected virtual void ReplaceDataFile(string newFileName, int newStride, long newLength, int newSeedStride)
        {
            Trace.TraceInformation("Filemanager replacing datafile");

            using (var lockAll = _rowSynchronizer.LockAll())
            {
                if (_fileStream != null)
                {
                    if (newSeedStride <= CorePosition)
                        SaveCore();

                    if (_fileMap != null)
                        _fileMap.Dispose();

                    _fileStream.Flush();

                    if (_core != null)
                        _fileStream.SetLength(CorePosition + ((long)(MaxLength + 1) * Stride));

                    _fileStream.Close();
                    _fileStream.Dispose();
                    _fileStream = null;
                }

                File.Replace(newFileName, FileNamePath, FileNamePath + ".old", true);

                this.Stride = newStride;
                this.CorePosition = newSeedStride;

                _fileStream = OpenFile(FileNamePath, _bufferSize);

                InitializeFileStream(_fileStream, newLength);
                InitializeFileMap();
            }
        }

        protected virtual void ResizeDataFile(long newLength, int newStride)
        {
            Trace.TraceInformation("Filemanager resizing datafile");

            using (var lockAll = _rowSynchronizer.LockAll())
            {
                lock (_syncRoot)
                {
                    if (_fileStream != null)
                    {
                        if (_fileMap != null)
                            _fileMap.Dispose();

                        _fileStream.Flush();

                        if (_core != null)
                            _fileStream.SetLength(CorePosition + ((MaxLength + 1) * Stride));

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
        }

        //protected void CopySegmentTo(int newStride, int newSeedPosition, string newFileName, int startSegment, int endSegment)
        //{
        //    CopySegmentTo(newStride,newSeedPosition,newFileName,startSegment,endSegment);
        //}

        protected void CopySegmentTo(int newStride, int newSeedPosition, string newFileName, long startSegment, long endSegment)
        {
            using (var inStream = GetReadableFileStream(startSegment + 1, (int)Math.Max(endSegment - (startSegment), 1)))
            {
                using (var outStream = GetWritableFileStream(newFileName))
                {
                    outStream.Position = newSeedPosition + (((long)startSegment + 1) * newStride);

                    var bufferSize = Stride > Environment.SystemPageSize ? Environment.SystemPageSize : Stride;

                    
                    for (var i = (startSegment + 1); i <= endSegment; i++)
                    {
                        if (!inStream.WriteSegmentTo(outStream, bufferSize, newStride, Stride))
                            SegmentSeed.Open(i);
                    }

                    outStream.Flush();
                    outStream.Close();
                }

                inStream.Close();
            }
        }

        public virtual void Rebuild(int newStride, long newLength, int newSeedStride)
        {
            Rebuild(Guid.NewGuid(), newStride, newLength, newSeedStride);
        }

        public virtual void Rebuild(Guid transactionId, int newStride, long newLength, int newSeedStride)
        {
            Trace.TraceInformation("Filemanager rebuilding seedposition {0}, rowsize {1}, and length {2}", newSeedStride, newStride, newLength);

            //Lock new database records down.
            using (var rl = _rowSynchronizer.LockAll())
            {
                if (newSeedStride <= 0)
                    newSeedStride = CorePosition;
                if (newLength < MaxLength)
                    newLength = MaxLength;
                if (newStride <= 0)
                    newStride = Stride;

                lock (_syncRoot)
                {
                    if ((newStride > Stride)  || newSeedStride > CorePosition)
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

                        var tasks = TaskGrouping.GetSegmentedTaskGroups(MaxLength, Stride);

                        var grouping = TaskGrouping.GetCPUGroupsFor(tasks);
                        
                        Parallel.ForEach(grouping, delegate(IndexingCPUGroup group)
                        { CopySegmentTo(newStride, newSeedStride, newFileName, group.StartSegment, group.EndSegment); });

                        ReplaceDataFile(newFileName, newStride, newLength, newSeedStride);
                    }
                    else
                    {
                        ResizeDataFile(newLength + 1, newStride);
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
                var newFileName = Path.Combine(Path.GetDirectoryName(FileNamePath), Guid.NewGuid().ToString() + ".reorganize");
                var fi = new FileInfo(newFileName);
                if (!fi.Exists)
                {
                    using (var nfs = fi.Create())
                    {
                        nfs.SetLength(GetFileSizeFor(CorePosition, Length + 1, Stride));
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
                                    continue; // _core.Open(jObj.Value<int>("$location"));
                                }

                                if (block.ContainsKey(id))
                                    block[id] = obj;
                                else
                                    block.Add(id, obj);

                                obj.Remove("$location");
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
                                fs.Position = CorePosition + (recordsWritten * Stride) + Stride;

                                foreach (var item in block)
                                    using (var s = _formatter.Unparse(item.Value))
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

                    ReplaceDataFile(newFileName, Stride, Length, CorePosition);
                    InvokeRebuilt(Guid.Empty, Stride, Length, CorePosition);
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

        //private void InvokeCommitFailed(object stream, object segs, int maxRowSize, int newRows)
        //{
        //    if (CommitFailed != null)
        //        CommitFailed(stream, segs, maxRowSize, newRows);
        //}

        //public event CommitFailed<EntityType> CommitFailed;

        //#endregion

        #region Committed Event

        protected void InvokeTransactionCommitted<IdType>(ITransaction<EntityType> transaction)
        {
            if (TransactionCommitted != null)
                TransactionCommitted(transaction);
        }

        public event Committed<EntityType> TransactionCommitted;

        #endregion

        #region Rebuilt Event

        protected void InvokeRebuilt(Guid transactionId, int newStride, long newLength, int newSeedStride)
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
