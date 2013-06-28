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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BESSy.Transactions;

namespace BESSy.Indexes
{
    public delegate void UpdateFailed<IndexType>(IList<TransactionIndexResult<IndexType>> indexes, IDisposable transaction, int newStride, int newLength);

    public interface IIndexFileManager<IndexType, EntityType, SegmentType> : IAtomicFileManager<IndexPropertyPair<IndexType, SegmentType>>, IQueryableFile
    {
        Func<EntityType, IndexType> IndexGet { get; }
        void UpdateFromTransaction(IList<TransactionIndexResult<IndexType>> indexes, IDisposable transaction);
        event UpdateFailed<IndexType> UpdateFailed;
    }

    public class IndexFileManager<IndexType, EntityType> : AtomicFileManager<IndexPropertyPair<IndexType, int>>, IIndexFileManager<IndexType, EntityType, int>
    {
        //public IndexFileManager(string fileNamePath, string indexToken)
        //    : this(fileNamePath, indexToken, TypeFactory.GetBinConverterFor<IndexType>())
        //{ }

        //public IndexFileManager(string fileNamePath, string indexToken, IBinConverter<IndexType> propertyConverter)
        //    : this(fileNamePath, indexToken, propertyConverter, new BSONFormatter())
        //{ }

        //public IndexFileManager(string fileNamePath, string indexToken, IBinConverter<IndexType> propertyConverter, IQueryableFormatter formatter)
        //    : this(fileNamePath, indexToken, Environment.SystemPageSize.Clamp(2048, 8192), propertyConverter, formatter)
        //{ }

        //public IndexFileManager(string fileNamePath, string indexToken, int bufferSize, IBinConverter<IndexType> propertyConverter, IQueryableFormatter formatter)
        //    : this(fileNamePath, indexToken, bufferSize, propertyConverter, formatter, new RowSynchronizer<int>(new BinConverter32()))
        //{ }

        //public IndexFileManager(string fileNamePath, string indexToken, int bufferSize, IBinConverter<IndexType> propertyConverter, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer)
        //    : this(fileNamePath, indexToken, bufferSize, 0, 0, propertyConverter, formatter, rowSynchronizer)
        //{ }

        public IndexFileManager(string fileNamePath, string indexToken, int bufferSize, int startingSize, int maximumBlockSize, IBinConverter<IndexType> propertyConverter, IQueryableFormatter formatter, IRowSynchronizer<int> rowSynchronizer)
            : base(fileNamePath, bufferSize, startingSize, maximumBlockSize, formatter, rowSynchronizer)
        {
            _seed = new Seed32(0);
            _propertyConverter = propertyConverter;
            _indexToken = indexToken;
            
        }

        string _indexToken;
        
        protected IBinConverter<IndexType> _propertyConverter;

        protected override void InitializeSeedFrom(FileStream fileStream)
        {
            var len = fileStream.Length == 0 ? 0 : (int)((fileStream.Length - SeedPosition) / Stride);
            _seed = new Seed32(len);
        }

        protected override void InitializeSeed()
        {
            _seed = new Seed32(0);
        }

        protected override void ReinitializeSeed(int recordsWritten)
        {
            _seed = new Seed32(recordsWritten);
        }

        protected override ISeed<SeedType> GetDefaultSeed<SeedType>()
        {
            return TypeFactory.GetSeedFor<SeedType>();
        }

        protected override long SaveSeed()
        {
            return 0;
        }

        public Func<EntityType, IndexType> IndexGet { get; protected set; }
        public override int Stride { get; protected set; }
        public override int SeedPosition {get; protected set;}

        public override int Load()
        {
            Trace.TraceInformation("Index filemanager loading");

            if (_formatter != null && _propertyConverter != null)
                Stride = _formatter.FormatObj(new IndexPropertyPair<IndexType, int>(_propertyConverter.Max, int.MaxValue)).Length;

            if (_indexToken != null)
                IndexGet = (Func<EntityType, IndexType>)Delegate.CreateDelegate(typeof(Func<EntityType, IndexType>), typeof(EntityType).GetProperty(_indexToken).GetGetMethod());

            var length = base.Load();

            return length;
        }

        public override int SaveSegment(IndexPropertyPair<IndexType, int> obj)
        {
            try
            {
                using (var inStream = _formatter.FormatObjStream(obj))
                {
                    if (inStream.Length > Stride || _seed.Peek() > MaxLength)
                        InvokeSaveFailed(obj, _seed.Peek(), GetStrideFor((int)inStream.Length), GetSizeWithGrowthFactor(_seed.Peek()));

                    var iseg = _seed.Increment();

                    using (var readLock = _rowSynchronizer.Lock(iseg, FileAccess.Write, FileShare.Read))
                    {
                        using (var stream = GetWritableFileStream(iseg, 1))
                        {
                            inStream.WriteAllTo(stream);

                            stream.Flush();
                        }
                    }

                    return iseg;
                }
            }
            catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format(_serializerError, jsEx.InnerException, jsEx)); throw; }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
        }

        public override void SaveSegment(IndexPropertyPair<IndexType, int> obj, int segment)
        {
            try
            {
                using (var inStream = _formatter.FormatObjStream(obj))
                {
                    if (inStream.Length > Stride || segment > MaxLength)
                        InvokeSaveFailed(obj, segment, GetStrideFor((int)inStream.Length), GetSizeWithGrowthFactor(segment));

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

        public override void DeleteSegment(int segment)
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
                    }
                }
            }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
        }

        public void UpdateFromTransaction(IList<TransactionIndexResult<IndexType>> indexes, IDisposable transaction)
        {
            Trace.TraceInformation("Index filemanager updating from transaction");

            try
            {
                var indexStreams = new Dictionary<IndexType, Stream>();

                foreach (var res in indexes)
                {
                    if (res.Action == Action.Delete)
                        indexStreams.Add(res.Index, new MemoryStream(new byte[Stride]));
                    else
                        indexStreams.Add(res.Index, _formatter.FormatObjStream(new IndexPropertyPair<IndexType, int>(res.Index, res.Segment)));
                }

                bool fail = false;
                int max = 0;
                int size = 0;

                //prevent new rows from being added until the seed has incremented all new rows.
                using (var lockAdd = _rowSynchronizer.Lock(new Range<int>(Length, int.MaxValue)))
                {
                    max = indexes.Where(r => r.Action == Action.Create).Count() + Length;
                    size = (int)indexStreams.Max(s => s.Value.Length);

                    if (size > Stride || max > MaxLength)
                        fail = true;       
                }

                if (fail)
                    //throw new NotSupportedException("Index stride increase not supported.");
                Rebuild(Guid.NewGuid()
                    , Math.Max(Stride, GetStrideFor(size))
                    , Math.Max(MaxLength, GetSizeWithGrowthFactor(max))
                    , SeedPosition);
                //InvokeUpdateFailed(indexes, transaction, (int)Math.Max(size, Stride), Math.Max(max, MaxLength));

                using (var lockSeg = _rowSynchronizer.LockAll())
                {
                    for (var x = 0; x < indexes.Count; x++)
                    {
                        var i = indexes[x];

                        if (i.Action == Action.Create)
                            i.IndexSegment = _seed.Increment();

                        if (i.IndexSegment <= 0)
                            throw new InvalidOperationException(string.Format("Index dbSegment not set {0}", i.IndexSegment));

                        if (i.IndexSegment > MaxLength)
                            throw new InvalidOperationException("Database length is to short for this transaction, rebuild the database first.");

                        using (var stream = GetWritableFileStream(i.IndexSegment, 1))
                        {
                            indexStreams[i.Index].WriteAllTo(stream);

                            stream.Flush();
                        }
                    }
                }
            }

            catch (IOException ioEx) { Trace.TraceError(String.Format(_ioError, "", ioEx)); throw; }
            catch (SystemException sysEx) { Trace.TraceError(sysEx.ToString()); throw; }
        }

        /// <summary>
        /// Rebuilds the index from the primary database file.
        /// </summary>
        /// <param name="fileMananger"></param>
        public void Rebuild(IQueryableFile fileMananger, int dbLength)
        {
            //Trace.TraceInformation("Index filemanager rebuilding");

            //if (fileMananger == null)
            //    throw new ArgumentNullException();

            //using (_rowSynchronizer.Lock(int.MaxValue, FileAccess.Read, FileShare.ReadWrite))
            //{
            //    Stride = _formatter.FormatObj(new IndexPropertyPair<IndexType, int>(_propertyConverter.Max, int.MaxValue)).Length;

            //    lock (_syncRoot)
            //    {
            //        var newFileName = Guid.NewGuid().ToString() + ".index" + ".rebuild";
            //        var fi = new FileInfo(newFileName);
            //        if (!fi.Exists)
            //        {
            //            using (var nfs = fi.Create())
            //            {
            //                nfs.SetLength(GetFileSizeFor(SeedPosition, dbLength, Stride));
            //                nfs.Flush();
            //            }
            //        }

            //        using (var lockAll = _rowSynchronizer.LockAll())
            //        {
            //            using (var fs = GetWritableFileStream(newFileName))
            //            {
            //                var seg = 0;
            //                foreach (var page in fileMananger.AsEnumerable())
            //                {
            //                    fs.Position = SeedPosition;

            //                    foreach (var obj in page)
            //                    {
            //                        seg++;
            //                        var index = obj.Value<IndexType>(_indexToken);
            //                        using (var stream = _formatter.FormatObjStream(new IndexPropertyPair<IndexType, int>(index, seg)))
            //                        {
            //                            stream.SetLength(Stride);
            //                            stream.WriteAllTo(fs);
            //                        }
            //                    }
            //                }
            //            }

            //            ReinitializeSeed(dbLength);
            //            ReplaceDataFile(newFileName, Stride, dbLength, SeedPosition);
            //        }
            //    }
            //}
        }

        public override void Reorganize<PropertyType>(IBinConverter<PropertyType> converter, Func<JObject, PropertyType> idSelector)
        {
            if (!(typeof(PropertyType) == typeof(IndexType)))
                throw new InvalidOperationException("invalid seed IdType specified.");

            base.Reorganize<PropertyType>(converter, idSelector);
        }

        #region UpdateFailed Event

        protected void InvokeUpdateFailed(IList<TransactionIndexResult<IndexType>> indexes, IDisposable transaction, int newStride, int newLength)
        {
            if (UpdateFailed != null)
                UpdateFailed(indexes, transaction, newStride, newLength);
        }

        public event UpdateFailed<IndexType> UpdateFailed;

        #endregion
    }
}
