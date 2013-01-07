/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using Newtonsoft.Json;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Extensions;
using BESSy.Serialization.Converters;

namespace BESSy.Files
{
    public interface IBatchFileManager<T> : IFileManager, IDisposable
    {
        int BatchSize { get; }
        int GetBatchedSegmentCount(Stream stream);
        ISeed<IdType> LoadSeedFrom<IdType>(Stream stream);
        IList<T> LoadBatchFrom(Stream stream);
        long SaveSeed<IdType>(Stream stream, ISeed<IdType> seed);
        long SaveBatch(Stream stream, IList<T> objs, long atPosition);
    }

    //TODO: write tests for this class specifically.
    public class BatchFileManager<T> : IBatchFileManager<T>
    {
        //TODO: this needs to come from the Formatter, not the file manager. That way the delimeter can be specific to the encoding.
        internal readonly static ArraySegment<byte> SeedStart = new ArraySegment<byte>
            (new byte[] { 4, 4, 4, 4, 4, 4, 4, 4, 4 });

        //TODO: this needs to come from the Formatter, not the file manager. That way the delimeter can be specific to the encoding.
        internal readonly static ArraySegment<byte> SegmentDelimeter = new ArraySegment<byte>
            (new byte[] { 3, 3, 3, 3, 3, 3, 3, 3, 3 });

        public BatchFileManager(int bufferSize, IFormatter formatter) : this(2048, bufferSize, formatter)
        {

        }

        public BatchFileManager(int batchSize, int bufferSize, IFormatter formatter) : this(253952, batchSize, bufferSize, formatter)
        {
            
        }

        public BatchFileManager(int minSeedSize, int batchSize, int bufferSize, IFormatter formatter)
        {
            BatchSize = batchSize;
            _bufferSize = bufferSize;
            _formatter = formatter;
            _minSeedSize = minSeedSize;
        }

        object _syncRoot = new object();
        IFormatter _formatter;
        int _minSeedSize = 253952;
        int _bufferSize = 4096;
        string _error = "File name {0}, could not be found or accessed: {1}.";
        IBinConverter<int> _batchConverter = new BinConverter32();

        public string WorkingPath { get; set; }
        public int BatchSize { get; protected set; }

        public FileStream GetWritableFileStream(string fileNamePath)
        {
            return new FileStream(fileNamePath, FileMode.OpenOrCreate
                , FileSystemRights.Write | FileSystemRights.CreateFiles
                , FileShare.None, _bufferSize, FileOptions.WriteThrough);
        }

        public FileStream GetReadableFileStream(string fileNamePath)
        {
            return new FileStream(fileNamePath, FileMode.Open
                , System.Security.AccessControl.FileSystemRights.Read
                , FileShare.Read, _bufferSize, FileOptions.SequentialScan);
        }

        #region IBatchedFileManager<T> Members

        public int GetBatchedSegmentCount(Stream stream)
        {
            try
            {
                byte[] buffers = new byte[0];

                long pos = stream.Position;

                var len = stream.Length;

                if (len - pos < SegmentDelimeter.Array.Length)
                    return 0;

                lock (_syncRoot)
                {
                    stream.Position = pos;

                    var match = SegmentDelimeter.Array[0];
                    var delLength = SegmentDelimeter.Array.Length;

                    var buffer = new byte[_bufferSize];
                    int read = stream.Read(buffer, 0, buffer.Length);

                    var count = 0;

                    var br = new BinaryReader(stream);

                    ArraySegment<byte> s = new ArraySegment<byte>(buffer, 0, SeedStart.Array.Length);
                    if (!s.Equals<byte>(SeedStart))
                        count += _batchConverter.FromBytes(buffer.Take(4).ToArray());

                    while (read > SegmentDelimeter.Array.Length)
                    {
                        var index = Array.FindIndex(buffer, b => b == match);

                        while (index >= 0 && index <= buffer.Length - delLength)
                        {
                            ArraySegment<byte> b = new ArraySegment<byte>(buffer, index, delLength);

                            if (b.Equals<byte>(SegmentDelimeter))
                            {
                                stream.Position = pos + index + delLength;

                                if (stream.Position < len)
                                    count += br.ReadInt32();

                                break;
                            }

                            index = Array.FindIndex(buffer, index + 1, n => n == match);
                        }

                        //Array.Resize(ref buffers, buffers.Length + _bufferSize);
                        //Array.Copy(buffer.Take(_bufferSize).ToArray(), 0, buffers, buffers.Length - _bufferSize, _bufferSize);
                        //stream.Position -= delLength;
                        Array.Resize(ref buffers, buffers.Length + buffer.Length);
                        Array.Copy(buffer, 0, buffers, buffers.Length - buffer.Length, buffer.Length);
                        stream.Position -= delLength;

                        pos = stream.Position;

                        Array.Resize(ref buffer, _bufferSize);
                        read = stream.Read(buffer, 0, buffer.Length);
                    }

                    return count;
                }
            }
            catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format("File could not be deserialized : \r\n {0} \r\n {1}", jsEx.InnerException, jsEx)); throw; }
            catch (Exception ex) { Trace.TraceError(String.Format(_error, "", ex)); throw; }
        }

        public ISeed<IdType> LoadSeedFrom<IdType>(Stream stream)
        {
            try
            {
                byte[] buffers = new byte[0];

                if (stream.Length < SegmentDelimeter.Array.Length)
                    return default(ISeed<IdType>);

                lock (_syncRoot)
                {
                    var pos = stream.Position = 0;

                    var match = SegmentDelimeter.Array[0];
                    var delLength = SegmentDelimeter.Array.Length;

                    var buffer = new byte[_bufferSize];

                    int read = stream.Read(buffer, 0, buffer.Length);

                    ArraySegment<byte> s = new ArraySegment<byte>(buffer, 0, SeedStart.Array.Length);
                    if (!s.Equals<byte>(SeedStart))
                        return default(ISeed<IdType>);

                    var start = SeedStart.Array.Length + 4;
                    buffer = buffer.Skip(start).ToArray();

                    while (read > SegmentDelimeter.Array.Length)
                    {
                        var index = Array.FindIndex(buffer, b => b == match);

                        while (index >= 0 && index <= buffer.Length - delLength)
                        {
                            ArraySegment<byte> b = new ArraySegment<byte>(buffer, index, delLength);

                            if (b.Equals<byte>(SegmentDelimeter))
                            {
                                var last = buffers.Length;
                                var seg = buffer.Take(index).ToArray();
                                Array.Resize(ref buffers, last + index);
                                Array.Copy(seg, 0, buffers, last, seg.Length);

                                stream.Position = pos + start + index + delLength;
                                
                                var seed = _formatter.UnformatObj<ISeed<IdType>>(buffers);

                                if (seed.MinimumSeedStride >= stream.Position)
                                    stream.Position = seed.MinimumSeedStride;
                                else
                                    seed.MinimumSeedStride = (int)stream.Position;

                                return seed;
                            }

                            index = Array.FindIndex(buffer, index + 1, n => n == match);
                        }

                        Array.Resize(ref buffers, buffers.Length + buffer.Length - delLength);
                        Array.Copy(buffer, 0, buffers, buffers.Length - (buffer.Length - delLength), buffer.Length - delLength);

                        pos = stream.Position -= delLength;
                        start = 0;

                        Array.Resize(ref buffer, _bufferSize);
                        read = stream.Read(buffer, 0, buffer.Length);
                    }
                }
            }
            catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format("File could not be deserialized : \r\n {0} \r\n {1}", jsEx.InnerException, jsEx)); throw; }
            catch (Exception ex) { Trace.TraceError(String.Format(_error, "", ex)); throw; }

            return default(ISeed<IdType>);
        }

        public IList<T> LoadBatchFrom(Stream stream)
        {
            try
            {
                bool skipNext = false;
                byte[] buffers = new byte[0];

                long pos = stream.Position;
                var len = stream.Length;

                if (len - pos < SegmentDelimeter.Array.Length)
                    return new List<T>();

                lock (_syncRoot)
                {
                    stream.Position = pos;

                    var match = SegmentDelimeter.Array[0];
                    var delLength = SegmentDelimeter.Array.Length;

                    var buffer = new byte[_bufferSize];

                    int read = stream.Read(buffer, 0, buffer.Length);

                    int start = 0;
                    ArraySegment<byte> s = new ArraySegment<byte>(buffer, 0, SeedStart.Array.Length);
                    if (s.Equals<byte>(SeedStart))
                    {
                        skipNext = true;
                        buffer = buffer.Skip(SeedStart.Array.Length + 4).ToArray();
                        start = SeedStart.Array.Length + 4;
                    }
                    else
                    {
                        buffer = buffer.Skip(4).ToArray();
                        start = 4;
                    }

                    while (read > SegmentDelimeter.Array.Length)
                    {
                        var index = Array.FindIndex(buffer, b => b == match);

                        while (index >= 0 && index <= buffer.Length - delLength)
                        {
                            ArraySegment<byte> b = new ArraySegment<byte>(buffer, index, delLength);

                            if (b.Equals<byte>(SegmentDelimeter))
                            {
                                var last = buffers.Length;
                                var seg = buffer.Take(index).ToArray();
                                Array.Resize(ref buffers, last + index);
                                Array.Copy(seg, 0, buffers, last, seg.Length);

                                stream.Position = pos + start + index + delLength;

                                if (!skipNext)
                                    return _formatter.UnformatObj<IList<T>>(buffers);
                                else
                                {
                                    buffers = new byte[0];
                                    skipNext = false;
                                    break;
                                }
                            }

                            index = Array.FindIndex(buffer, index + 1, n => n == match);
                        }

                        Array.Resize(ref buffers, buffers.Length + buffer.Length - delLength);
                        Array.Copy(buffer, 0, buffers, buffers.Length - (buffer.Length - delLength), buffer.Length - delLength);

                        pos = stream.Position -= delLength;
                        start = 0;

                        Array.Resize(ref buffer, _bufferSize);
                        read = stream.Read(buffer, 0, buffer.Length);
                    }
                }
            }
            catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format("File could not be deserialized : \r\n {0} \r\n {1}", jsEx.InnerException, jsEx)); throw; }
            catch (Exception ex) { Trace.TraceError(String.Format(_error, "", ex)); throw; }

            return new List<T>();
        }

        public long SaveSeed<IdType>(Stream stream, ISeed<IdType> seed)
        {

#if DEBUG
            if (object.Equals(seed, default(ISeed<IdType>)))
                throw new ArgumentNullException("seed");
#endif

            try
            {
                byte[] formatted = _formatter.FormatObj(seed);

                lock (_syncRoot)
                {
                    stream.Position = 0;

                    stream.Write(SeedStart.Array, 0, SeedStart.Array.Length);

                    stream.Write(_batchConverter.ToBytes(1), 0, _batchConverter.Length);
                    stream.Write(formatted, 0, formatted.Length);
                    stream.Write(SegmentDelimeter.Array, 0, SegmentDelimeter.Array.Length);

                    var position = stream.Position;
                    
                    stream.Flush();

                    if (position < seed.MinimumSeedStride)
                        position = seed.MinimumSeedStride;

                    return position;
                }
            }
            catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format("File could not be serialized : \r\n {0} \r\n {1}", jsEx.InnerException, jsEx)); throw; }
            catch (Exception ex) { Trace.TraceError(String.Format(_error, "", ex)); throw; }
        }

        public long SaveBatch(Stream stream, IList<T> objs, long atPosition)
        {
#if DEBUG
            if (objs == null)
                throw new ArgumentNullException("obj");
#endif

            try
            {
                byte[] formatted = _formatter.FormatObj(objs);

                lock (_syncRoot)
                {

                    if (atPosition >= 0)
                        stream.Position = atPosition;

                    stream.Write(_batchConverter.ToBytes(objs.Count), 0, _batchConverter.Length);
                    stream.Write(formatted, 0, formatted.Length);
                    stream.Write(SegmentDelimeter.Array, 0, SegmentDelimeter.Array.Length);

                    var position = stream.Position;

                    stream.Flush();

                    return position;
                }
            }
            catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format("File could not be serialized : \r\n {0} \r\n {1}", jsEx.InnerException, jsEx)); throw; }
            catch (Exception ex) { Trace.TraceError(String.Format(_error, "", ex)); throw; }
        }
        

        #endregion

        public void Dispose()
        {
            
        }
    }
}
