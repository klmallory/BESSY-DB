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
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading.Tasks;
using BESSy.Extensions;
using BESSy.Parallelization;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using BESSy.Transactions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BESSy.Files
{
    public delegate void SaveFailed<EntityType>(SaveFailureInfo<EntityType> saveFailInfo);
    public delegate void CommitFailed<IdType, EntityType>( CommitFailureInfo<IdType, EntityType> commitFailInfo);

    public interface IAtomicFileManager<IdType, EntityType> : IQueryableFile, IFileManager, IDisposable
    {
        EntityType LoadSegmentFrom(int segment);
        void SaveSegment(EntityType obj, int segment);
        int SaveSegment(EntityType obj);
        void DeleteSegment(int segment);
        IDictionary<IdType, int> CommitTransaction(ITransaction<IdType, EntityType> trans, IDictionary<IdType, int> segments);
    }

    public class AtomicFileManager<IdType, EntityType> : IAtomicFileManager<IdType, EntityType>
    {
        //TODO: this needs to come from the Formatter, not the file manager. That way the delimeter can be specific to the encoding.
        internal readonly static ArraySegment<byte> SeedStart = new ArraySegment<byte>
            (new byte[] { 4, 4, 4, 4, 4, 4, 4, 4, 4 });

        internal readonly static ArraySegment<byte> SegmentDelimeter = new ArraySegment<byte>
            (new byte[] { 3, 3, 3, 3, 3, 3, 3, 3, 3 });

        public AtomicFileManager(string fileNamePath, IQueryableFormatter formatter)
            : this(fileNamePath, Environment.SystemPageSize.Clamp(2048, 8192), formatter)
        { }

        public AtomicFileManager(string fileNamePath, int bufferSize, IQueryableFormatter formatter)
            : this(fileNamePath, bufferSize, formatter, new RowSynchronizer<int>(new BinConverter32()))
        { }

        public AtomicFileManager(string fileNamePath, int bufferSize, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer)
            : this(fileNamePath, bufferSize, 0, formatter, rowSynchronizer)
        { }

        public AtomicFileManager(string fileNamePath, int bufferSize, int startingSize, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer)
        {
            _startingSize = startingSize;
            _bufferSize = bufferSize;
            _formatter = formatter;
            _fileNamePath = fileNamePath;
            _rowSynchronizer = rowSynchronizer;
            _lookupGroups = new List<IndexingCPUGroup<int>>();
            _segmentSeed = new Seed32(0);
        }

        static int GetStrideFor(int length)
        {
            return (int)(((length / 64) + 1) * 64);
        }

        protected static MemoryMappedFile OpenMemoryMap(string fileNamePath, FileStream fileStream)
        {
            var fi = new FileInfo(fileNamePath);
            fi.Directory.Create();

            return MemoryMappedFile.CreateFromFile
                (fileStream
                , "Global\\" + fileNamePath + ".mapping"
                , fileStream.Length
                , MemoryMappedFileAccess.ReadWrite //Execute
                , new MemoryMappedFileSecurity()
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

        object _syncRoot = new object();
        IQueryableFormatter _formatter;
        ISeed<Int32> _segmentSeed;
        int _bufferSize;
        int _startingSize;
        string _fileNamePath;
        string _ioError = "File name {0}, could not be found or accessed: {1}.";
        string _serilizerError = "File could not be serialized : \r\n {0} \r\n {1}";
        MemoryMappedFile _fileMap;
        FileStream _fileStream;

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

        protected void CloseFile()
        {
            SaveSeed();

            if (_fileMap != null)
                _fileMap.Dispose();

            if (_segmentSeed != null)
                _fileStream.SetLength(SeedPosition + ((Length + 1) * Stride));

            _fileStream.Flush();
            _fileStream.Close();
            _fileStream.Dispose();
        }

        protected void InitializeFileMap()
        {
            if (_fileMap != null)
                _fileMap.Dispose();

            _fileMap = OpenMemoryMap(_fileNamePath, _fileStream);

            var groups = TaskGrouping.GetSegmentedTaskGroups(MaxLength, Stride);

            _lookupGroups = TaskGrouping.GetCPUGroupsFor(groups.ToDictionary(g => g, g => g));
        }

        protected void InitializeFileStream(FileStream fileStream)
        {
            MaxLength = GetSizeWithGrowthFactor(Length);

            var size = GetFileSizeFor(SeedPosition, MaxLength, Stride);

            fileStream.SetLength(size);
        }

        protected void InitializeFileStream()
        {
            if (_fileStream != null)
                CloseFile();

            _fileStream = OpenFile(_fileNamePath, _bufferSize);

            InitializeFileStream(_fileStream);
        }

        protected Stream GetWritableFileStream(int segment, int count)
        {
            return _fileMap.CreateViewStream(segment * Stride, count * Stride, MemoryMappedFileAccess.Write);
        }

        protected Stream GetReadableFileStream(int segment, int count)
        {
            return _fileMap.CreateViewStream(segment * Stride, count * Stride, MemoryMappedFileAccess.Read);
        }

        protected long SaveSeed()
        {
            using (var seedStream = _formatter.FormatObjStream(_segmentSeed))
            {
                if (seedStream.Length + SegmentDelimeter.Array.Length > SeedPosition)
                    Rebuild(Stride, Length, (int)seedStream.Length + SegmentDelimeter.Array.Length);

                return SaveSeed(_fileStream, seedStream, SeedPosition);
            }
        }

        protected long SaveSeed(FileStream fileStream, Stream seedStream, int seedStride)
        {

#if DEBUG
            if (seedStream == null)
                throw new ArgumentNullException("seed");
#endif

            try
            {
                seedStream.Position = 0;

                lock (_syncRoot)
                {
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
            }
            catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format(_serilizerError, jsEx.InnerException, jsEx)); throw; }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
        }

        protected ISeed<int> LoadSeedFrom(FileStream fileStream)
        {
            try
            {
                if (fileStream.Length < SegmentDelimeter.Array.Length)
                    return _segmentSeed;

                lock (_syncRoot)
                {
                    var pos = fileStream.Position = 0;

                    var match = SegmentDelimeter.Array[0];
                    var delLength = SegmentDelimeter.Array.Length;

                    var buffer = new byte[_bufferSize];

                    int read = fileStream.Read(buffer, 0, buffer.Length);

                    ArraySegment<byte> s = new ArraySegment<byte>(buffer, 0, SeedStart.Array.Length);

                    if (!s.Equals<byte>(SeedStart))
                        return new Seed32();

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

                                    var seed = _formatter.UnformatObj<ISeed<int>>(bufferedStream);

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

                    return new Seed32();
                }
            }
            catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format(_serilizerError, jsEx.InnerException, jsEx)); throw; }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
        }
        
        public int MaxLength { get; protected set; }
        public string WorkingPath { get; set; }
        public int Pages { get { return _lookupGroups.Count; } }
        public int SeedPosition { get { return _segmentSeed.MinimumSeedStride; } }
        public int Length { get { return _segmentSeed.LastSeed; } }
        public int Stride { get { return _segmentSeed.Stride; } }

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

        public void Load()
        {
            lock (_syncRoot)
            {
                using (_rowSynchronizer.LockAll())
                {
                    ISeed<int> seed = new Seed32();

                    var fi = new FileInfo(_fileNamePath);

                    if (!fi.Exists)
                    {
                        _segmentSeed = seed;

                        _fileStream = fi.Create();

                        InitializeFileStream(_fileStream);
                        InitializeFileMap();
                    }
                    else
                    {
                        using (var fs = fi.Open(FileMode.Open))
                        {
                            fs.Position = 0;

                            _segmentSeed = LoadSeedFrom(fs);

                            fs.Close();
                        }

                        InitializeFileStream();
                        InitializeFileMap();
                    }
                }
            }
        }

        /// <summary>
        /// Saves the entity at the specified segment position.
        /// </summary>
        /// <param name="obj">The entity to be saved.</param>
        /// <param name="segment"></param>
        /// <returns></returns>
        public void SaveSegment(EntityType obj, int segment)
        {
            try
            {
                using (var inStream = _formatter.FormatObjStream(obj))
                {
                    if (inStream.Length > Stride)
                    { } //InvokeSaveFailed( Failed = true, NewRowSize = (int)inStream.Length };

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
            catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format(_serilizerError, jsEx.InnerException, jsEx)); throw; }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
        }

        public int SaveSegment(EntityType obj)
        {
            try
            {
                using (var inStream = _formatter.FormatObjStream(obj))
                {
                    var newRowSize = (int)inStream.Length;

                    if (newRowSize > Stride && _segmentSeed.Peek() > MaxLength)
                        InvokeSaveFailed(obj, newRowSize, GetSizeWithGrowthFactor(Length));
                    else if (newRowSize > Stride)
                        InvokeSaveFailed(obj, newRowSize, GetSizeWithGrowthFactor(Length));

                    var nextSeed = _segmentSeed.Increment();

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
            catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format(_serilizerError, jsEx.InnerException, jsEx)); throw; }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
        }

        public void DeleteSegment(int segment)
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

                        _segmentSeed.Open(segment);
                    }
                }
            }
            catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format(_serilizerError, jsEx.InnerException, jsEx)); throw; }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
        }

        public IDictionary<IdType, int> CommitTransaction(ITransaction<IdType, EntityType> trans, IDictionary<IdType, int> segments)
        {
            try
            {
                var actions = trans.GetEnlistedActions();
                Dictionary<IdType, Stream> buffers = new Dictionary<IdType, Stream>();
                Dictionary<IdType, int> returnSegments = new Dictionary<IdType, int>();

                foreach (var action in actions.Where(a => a.Value.Action != Action.Delete))
                    buffers.Add(action.Key, _formatter.FormatObjStream(action.Value.Entity));

                foreach (var action in actions.Where(a => a.Value.Action == Action.Delete))
                    buffers.Add(action.Key, new MemoryStream());

                var maxRowSize = (int)buffers.Values.Max(s => s.Length);
                var newRows = actions.Values.Count(a => a.Action == Action.Create);

                using (var lockAll = _rowSynchronizer.LockAll())
                {
                    if (maxRowSize > Stride || newRows > MaxLength - Length)
                    { InvokeCommitFailed(trans, maxRowSize, Length + newRows); }

                    foreach (var buffer in buffers)
                    {
                        var action = actions[buffer.Key].Action;

                        int segment = 0;
                        if (action == Action.Create)
                            segment = _segmentSeed.Increment();
                        else
                            segment = segments[buffer.Key];

                        using (var stream = GetWritableFileStream(segment, 1))
                        {
                            buffer.Value.WriteAllTo(stream);

                            stream.Flush();
                        }

                        if (action != Action.Delete)
                            returnSegments.Add(buffer.Key, segment);
                        else
                            _segmentSeed.Open(segment);
                    }
                }

                return returnSegments;
            }
            catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format(_serilizerError, jsEx.InnerException, jsEx)); throw; }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
            finally { GC.Collect(); }
        }

        public EntityType LoadSegmentFrom(int segment)
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

        public JObject[] GetPage(int page)
        {
            List<JObject> queryPage = new List<JObject>();

            lock (_syncRoot)
            {
                if (page > _lookupGroups.Count)
                    return queryPage.ToArray();

                var group = _lookupGroups[page];

                using (var rowLock = _rowSynchronizer.Lock(group.StartSegment, group.EndSegment))
                {
                    var bufferSize = Stride > Environment.SystemPageSize ? Environment.SystemPageSize : Stride;

                    using (var stream = GetReadableFileStream(group.StartSegment, group.EndSegment - group.StartSegment + 1))
                    {
                        for (var i = group.StartSegment; i <= group.EndSegment; i++)
                        {
                            using (var outStream = new MemoryStream())
                            {
                                if (stream.WriteSegmentTo(outStream, bufferSize, Stride, Stride))
                                    queryPage.Add(_formatter.Parse(outStream));
                            }
                        }
                    }
                }

                return queryPage.ToArray();
            }
        }

        public IEnumerator<JObject[]> GetEnumerator()
        {
            return new QueryEnumerator(this);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #region Rebuild

        private void ReplaceDataFile(string newFileName, int newStride, int newSeedStride)
        {
            using (var lockAll = _rowSynchronizer.LockAll())
            {
                if (_fileStream != null)
                {
                    if (_fileMap != null)
                        _fileMap.Dispose();

                    _fileStream.Flush();
                    _fileStream.Close();
                    _fileStream.Dispose();
                    _fileStream = null;
                }

                File.Replace(newFileName, _fileNamePath, _fileNamePath + ".old", true);

                _segmentSeed.Stride = newStride;
                _segmentSeed.MinimumSeedStride = newSeedStride;

                _fileStream = OpenFile(_fileNamePath, _bufferSize);

                InitializeFileStream(_fileStream);
                InitializeFileMap();

                SaveSeed();
            }
        }

        private void CopySegmentTo(int newStride, int newSeedPosition, string newFileName, int startSegment, int endsegment)
        {
            var buffer = new byte[newStride];

            using (var readLock = _rowSynchronizer.Lock(new Range<int>(startSegment, endsegment), FileAccess.Read, FileShare.None))
            {
                using (var inStream = GetReadableFileStream(startSegment, (endsegment - startSegment) + 1))
                {
                    inStream.Position = SeedPosition + (startSegment * Stride);

                    using (var outStream = GetWritableFileStream(newFileName))
                    {
                        outStream.Position = newSeedPosition + (startSegment * newStride);

                        var bufferSize = Stride > Environment.SystemPageSize ? Environment.SystemPageSize : Stride;

                        for (var i = startSegment; i <= endsegment; i++)
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
        }

        public void Rebuild(int newStride, int newLength, int newSeedStride)
        {
            Rebuild(Guid.NewGuid(), newStride, newLength, newSeedStride);
        }

        public void Rebuild(Guid transactionId, int newStride, int newLength, int newSeedStride)
        {
            if (newSeedStride <= 0)
                newSeedStride = SeedPosition;
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

            var tasks = TaskGrouping.GetSegmentedTaskGroups(Length, Stride);
            var taskgroups = new Dictionary<int, int>();

            tasks.ForEach(t => taskgroups.Add(t, t));

            var grouping = TaskGrouping.GetCPUGroupsFor<int>(taskgroups);

            lock (_syncRoot)
            {
                if (newStride > Stride || newSeedStride > SeedPosition)
                {
                    Parallel.ForEach(grouping, delegate(IndexingCPUGroup<int> group)
                    { CopySegmentTo(newStride, newSeedStride, newFileName, group.StartSegment, group.EndSegment); });
                }

                ReplaceDataFile(newFileName, newStride, newSeedStride);
            }
        }

        #endregion

        #region SaveFailed Event

        protected void InvokeSaveFailed(EntityType entity, int newRowSize, int newDatabaseSize)
        {
            if (SaveFailed != null)
                SaveFailed(new SaveFailureInfo<EntityType>() { Entity = entity, NewRowSize = GetStrideFor(newRowSize), NewDatabaseSize = newDatabaseSize });
        }

        public event SaveFailed<EntityType> SaveFailed;

        #endregion

        #region CommitFailed Event

        protected void InvokeCommitFailed(ITransaction<IdType, EntityType> trans, int newRowSize, int newDatabaseSize)
        {
            if (CommitFailed != null)
                CommitFailed(new CommitFailureInfo<IdType, EntityType>() { Transaction = trans, NewRowSize = GetStrideFor(newRowSize), NewDatabaseSize = newDatabaseSize });
        }

        public event CommitFailed<IdType, EntityType> CommitFailed;

        #endregion

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_fileStream != null)
                    CloseFile();
            }
        }
    }
}
