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
using System.Text;
using BESSy.Cache;
using BESSy.Extensions;
using BESSy.Files;
using BESSy.Parallelization;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using BESSy.Json;
using BESSy.Json.Linq;
using BESSy.Transactions;

namespace BESSy.Indexes
{
    public delegate void UpdateFailed<IndexType>(IList<TransactionIndexResult<IndexType>> indexes, IDisposable transaction, int newStride, int newLength);

    public interface IIndexFileManager<IndexType, EntityType, SegmentType> : IAtomicFileManager<IndexPropertyPair<IndexType, SegmentType>>, IQueryableFile
    {
        Func<EntityType, IndexType> IndexGet { get; }
        void UpdateFromTransaction(IList<TransactionIndexResult<IndexType>> indexes);
        event UpdateFailed<IndexType> UpdateFailed;
    }

    public class IndexFileManager<IdType, EntityType> : AtomicFileManager<IndexPropertyPair<IdType, int>>, IIndexFileManager<IdType, EntityType, int>
    {
                public IndexFileManager(string fileNamePath)
            : this(fileNamePath, new BSONFormatter())
        { }

        public IndexFileManager(string fileNamePath, IQueryableFormatter formatter)
            : this(fileNamePath, Environment.SystemPageSize.Clamp(2048, 8192), formatter)
        { }

        public IndexFileManager(string fileNamePath, int bufferSize, IQueryableFormatter formatter)
            : this(fileNamePath, bufferSize, formatter, new RowSynchronizer<int>(new BinConverter32()))
        { }

        public IndexFileManager(string fileNamePath, int bufferSize, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer)
            : this(fileNamePath, bufferSize, 0, 0, formatter, rowSynchronizer)
        { }

        public IndexFileManager(string fileNamePath, int bufferSize, int startingSize, int maximumBlockSize, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer)
            : this(fileNamePath, null, bufferSize, startingSize, maximumBlockSize, null, formatter, rowSynchronizer)
        {

        }

        public IndexFileManager(string fileNamePath, string indexToken, IBinConverter<IdType> propertyConverter)
            : this(fileNamePath, indexToken, new BSONFormatter(), propertyConverter)
        { }

        public IndexFileManager(string fileNamePath, string indexToken, IQueryableFormatter formatter, IBinConverter<IdType> propertyConverter)
            : this(fileNamePath, indexToken, Environment.SystemPageSize.Clamp(2048, 8192), formatter, propertyConverter)
        { }

        public IndexFileManager(string fileNamePath, string indexToken, int bufferSize, IQueryableFormatter formatter, IBinConverter<IdType> propertyConverter)
            : this(fileNamePath, indexToken, bufferSize, formatter, new RowSynchronizer<int>(new BinConverter32()), propertyConverter)
        { }

        public IndexFileManager(string fileNamePath, string indexToken, int bufferSize, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer, IBinConverter<IdType> propertyConverter)
            : this(fileNamePath, indexToken, bufferSize, 0, 0, formatter, rowSynchronizer, propertyConverter)
        { }

        public IndexFileManager(string fileNamePath, string indexToken, int bufferSize, int startingSize, int maximumBlockSize, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer, IBinConverter<IdType> propertyConverter)
            : this(fileNamePath, indexToken, bufferSize, startingSize, maximumBlockSize, propertyConverter, formatter, rowSynchronizer)
        {
        }

        public IndexFileManager(string fileNamePath, string indexToken, int bufferSize, int startingSize, int maximumBlockSize, IBinConverter<IdType> propertyConverter, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer)
            : base(fileNamePath, bufferSize, startingSize, maximumBlockSize, new Seed32(0), formatter, rowSynchronizer)
        {
            Seed = null;
           
            _propertyConverter = propertyConverter;
            _indexToken = indexToken;
        }

        protected object _syncSeed = new object();
        protected string _indexToken;
        protected IBinConverter<IdType> _propertyConverter;
        protected ISeed<Int32> _indexSeed { get; set; }

        protected override void InitializeSeed<SeedType>()
        {
            _segmentSeed = new Seed32(0);
            _segmentSeed.IdConverter = _propertyConverter;
            _segmentSeed.IdProperty = _indexToken;
            _segmentSeed.Stride = _formatter.FormatObj(new IndexPropertyPair<IdType, int>(_propertyConverter.Max, int.MaxValue)).Length;

            Stride = _segmentSeed.Stride;
            SeedPosition = _segmentSeed.MinimumSeedStride;

            lock (_syncSeed)
                _indexSeed = new Seed32(0);
        }

        protected override void InitializeSeedFrom<SeedType>(FileStream fileStream)
        {
            var seed = LoadSeedFrom<Int32>(fileStream);

            //var len = fileStream.Length == 0 ? 0 : (int)((fileStream.Length - SeedPosition) / Stride);

            Stride = seed.Stride;
            SeedPosition = seed.MinimumSeedStride;
            _indexToken = seed.IdProperty;
            _propertyConverter = (IBinConverter<IdType>)seed.IdConverter;
            _segmentSeed = seed;

            lock (_syncSeed)
                _indexSeed = new Seed32(seed.LastSeed);
        }

        public Func<EntityType, IdType> IndexGet { get; protected set; }
        public override int Length { get { return _indexSeed.LastSeed; } }
        public override int SeedPosition { get { return SegmentSeed.MinimumSeedStride; } protected set { SegmentSeed.MinimumSeedStride = value; } }
        public override int Stride { get { return SegmentSeed.Stride; } protected set { SegmentSeed.Stride = value; } }

        public override int Load<SeedType>()
        {
            Trace.TraceInformation("Index filemanager loading");

            var len = base.Load<Int32>();

            IndexGet = (Func<EntityType, IdType>)Delegate.CreateDelegate(typeof(Func<EntityType, IdType>), typeof(EntityType).GetProperty(_indexToken).GetGetMethod());

            return len;
        }

        public override long SaveSeed()
        {
            _segmentSeed.MinimumSeedStride = SeedPosition;
            _segmentSeed.Stride = Stride;

            var seedStream = _formatter.FormatObjStream(_segmentSeed);

            try
            {
                if (GetPositionFor(seedStream.Length + SegmentDelimeter.Array.Length) > SeedPosition)
                {
                    Rebuild(Stride, Length, GetPositionFor(seedStream.Length + SegmentDelimeter.Array.Length));

                    _segmentSeed.MinimumSeedStride = SeedPosition;
                    _segmentSeed.Stride = Stride;

                    seedStream = _formatter.FormatObjStream(_segmentSeed);
                }

                return SaveSeed(_fileStream, seedStream, SeedPosition);
            }
            finally { if (seedStream != null) seedStream.Dispose(); }
        }

        protected override long SaveSeed(FileStream fileStream, int seedStride)
        {
            var seedStream = _formatter.FormatObjStream(SegmentSeed);

            return SaveSeed(fileStream, seedStream, seedStride);
        }

        public override long SaveSeed<SeedType>()
        {
            _segmentSeed.MinimumSeedStride = SeedPosition;
            _segmentSeed.Stride = Stride;

            var seedStream = _formatter.FormatObjStream(_segmentSeed);

            try
            {
                if (GetPositionFor(seedStream.Length + SegmentDelimeter.Array.Length) > SeedPosition)
                {
                    Rebuild(Stride, Length, GetPositionFor(seedStream.Length + SegmentDelimeter.Array.Length));

                    _segmentSeed.MinimumSeedStride = SeedPosition;
                    _segmentSeed.Stride = Stride;

                    seedStream = _formatter.FormatObjStream(_segmentSeed);
                }

                return SaveSeed(_fileStream, seedStream, SeedPosition);
            }
            finally { if (seedStream != null) seedStream.Dispose(); GC.Collect(); }
        }

        //public override int SaveSegment(IndexPropertyPair<IdType, int> obj)
        //{
        //    try
        //    {
        //        using (var inStream = _formatter.FormatObjStream(obj))
        //        {
        //            if (inStream.Length > Stride || _indexSeed.Peek() > MaxLength)
        //                InvokeSaveFailed(obj, _indexSeed.Peek(), GetStrideFor((int)inStream.Length), GetSizeWithGrowthFactor(_indexSeed.Peek()));

        //            lock (_syncSeed)
        //            {
        //                var iseg = _indexSeed.Increment();

        //                using (var readLock = _rowSynchronizer.Lock(iseg, FileAccess.Write, FileShare.Read))
        //                {
        //                    using (var stream = GetWritableFileStream(iseg, 1))
        //                    {
        //                        inStream.WriteAllTo(stream);

        //                        stream.Flush();
        //                    }
        //                }

        //                return iseg;
        //            }
        //        }
        //    }
        //    catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format(_serializerError, jsEx.InnerException, jsEx)); throw; }
        //    catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
        //    catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
        //}

        //public override void SaveSegment(IndexPropertyPair<IdType, int> obj, int segment)
        //{
        //    try
        //    {
        //        using (var inStream = _formatter.FormatObjStream(obj))
        //        {
        //            if (inStream.Length > Stride || segment > MaxLength)
        //                InvokeSaveFailed(obj, segment, GetStrideFor((int)inStream.Length), GetSizeWithGrowthFactor(segment));

        //            using (var readLock = _rowSynchronizer.Lock(segment, FileAccess.Write, FileShare.Read))
        //            {
        //                using (var stream = GetWritableFileStream(segment, 1))
        //                {
        //                    inStream.WriteAllTo(stream);

        //                    stream.Flush();
        //                }
        //            }
        //        }
        //    }
        //    catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format(_serializerError, jsEx.InnerException, jsEx)); throw; }
        //    catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
        //    catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
        //}

        //public override void DeleteSegment(int segment)
        //{
        //    try
        //    {
        //        if (segment > Length)
        //            return;

        //        _indexSeed.Open(segment);

        //        using (var readLock = _rowSynchronizer.Lock(segment, FileAccess.Write, FileShare.Read))
        //        {
        //            using (var stream = GetWritableFileStream(segment, 1))
        //            {
        //                (new MemoryStream(new byte[Stride])).WriteAllTo(stream);

        //                stream.Flush();
        //            }
        //        }
        //    }
        //    catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
        //    catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
        //}

        public void UpdateFromTransaction(IList<TransactionIndexResult<IdType>> indexes)
        {
            Trace.TraceInformation("Index filemanager updating from transaction");

            try
            {
                var streams = new Dictionary<int, Stream>();

                for (var i = 0; i < indexes.Count; i++)
                {
                    var index = indexes[i];

                    if (index.Action != Action.Delete)
                        streams.Add(i, _formatter.FormatObjStream(new IndexPropertyPair<IdType, int>(index.Index, index.Segment)));
                    else
                        streams.Add(i, new MemoryStream(new byte[Stride]));
                }

                bool fail = false;
                int max = 0;
                int size = 0;

                //prevent new rows from being added until the segmentSeed has incremented all new rows.
                using (var lockAdd = _rowSynchronizer.Lock(new Range<int>(Length, int.MaxValue)))
                {
                    max = indexes.Where(r => r.Action == Action.Create).Count() + Length;
                    size = (int)streams.Max(s => s.Value.Length);

                    if (size > Stride || max > MaxLength)
                        fail = true;       
                }

                if (fail)
                {
                    Rebuild(Guid.NewGuid()
                        , Math.Max(Stride, GetStrideFor(size))
                        , Math.Max(MaxLength, GetSizeWithGrowthFactor(max))
                        , SeedPosition);
                }
                using (var lockSeg = _rowSynchronizer.LockAll())
                {
                    for (var x = 0; x < indexes.Count; x++)
                    {
                        var i = indexes[x];
                         
                        Stream indexStream = null;

                        if (i.Action == Action.Create)
                        {
                            indexStream = streams[x];
                            i.IndexSegment = _indexSeed.Increment();
                        }
                        else
                        {
                            indexStream = streams[x];
                        }

                        var trimLen = indexStream.Length;
                        i.Stream = indexStream;
                        indexes[x] = i;

                        if (i.IndexSegment <= 0)
                            continue;

                        if (i.IndexSegment > MaxLength)
                            throw new InvalidOperationException("Database length is to short for this transaction, rebuild the database first.");

                        using (var stream = GetWritableFileStream(i.IndexSegment, 1))
                        {
                            indexStream.WriteAllTo(stream, Stride);

                            stream.Flush();
                        }
                        
                        if (_formatter.Trim)
                            indexStream.SetLength(trimLen);
                    }
                }

                SaveSeed<Int32>();
            }

            catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
        }

        public override void ReinitializeSeed<IndexType>(int recordsWritten)
        {
            lock (_syncRoot)
            {
                using (var l = _rowSynchronizer.LockAll())
                {
                    _segmentSeed = new Seed32(recordsWritten)
                    {
                        Stride = _segmentSeed.Stride,
                        MinimumSeedStride = _segmentSeed.MinimumSeedStride,
                        IdProperty = _segmentSeed.IdProperty,
                        IdConverter = _segmentSeed.IdConverter,
                        PropertyConverter = _segmentSeed.PropertyConverter,
                        CategoryIdProperty = _segmentSeed.CategoryIdProperty,
                        LastReplicatedTimeStamp = _segmentSeed.LastReplicatedTimeStamp
                    };

                    _indexSeed = new Seed32(_segmentSeed.LastSeed);

                    SaveSeed<IndexType>();
                }
            }
        }

        public override void Reorganize<IndexType>(IBinConverter<IndexType> converter, Func<JObject, IndexType> idSelector)
        {
            if (!(typeof(IndexType) == typeof(IdType)))
                throw new InvalidOperationException("invalid segmentSeed SeedType specified.");

            base.Reorganize<IndexType>(converter, idSelector);
        }

        #region UpdateFailed Event

        protected void InvokeUpdateFailed(IList<TransactionIndexResult<IdType>> indexes, IDisposable transaction, int newStride, int newLength)
        {
            if (UpdateFailed != null)
                UpdateFailed(indexes, transaction, newStride, newLength);
        }

        public event UpdateFailed<IdType> UpdateFailed;

        #endregion
    }
}
