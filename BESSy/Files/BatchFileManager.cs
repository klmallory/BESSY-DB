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
        void Replace(string fromFileName, string toFileName);
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

        //TODO: this needs to come from the Formatter, not the file manager. That way the delimeter can be specific to the encoding.
        internal readonly static ArraySegment<byte> BatchStart = new ArraySegment<byte>
            (new byte[] { 2, 2, 2, 2, 2, 2, 2, 2, 2 });

        public BatchFileManager(IFormatter formatter) : this(4096, Environment.SystemPageSize, formatter)
        {

        }

        public BatchFileManager(int batchSize, int bufferSize, IFormatter formatter)
        {
            BatchSize = batchSize;
            _bufferSize = bufferSize;
            _formatter = formatter;
        }

        object _syncRoot = new object();
        IFormatter _formatter;
        int _bufferSize = 4096;
        string _error = "File name {0}, could not be found or accessed: {1}.";
        IBinConverter<int> _batchConverter = new BinConverter32();

        protected void FindBatchStart(Stream stream)
        {
            stream.Position = 0;
            var match = BatchStart.Array[0];
            var start = stream.Position;
            var buffer = new byte[_bufferSize];
            var read = stream.Read(buffer, 0, buffer.Length);
            
            while (read > BatchStart.Array.Length)
            {
                var index = Array.FindIndex(buffer, b => b == match);

                while (index >= 0 && index <= buffer.Length - BatchStart.Array.Length)
                {
                    ArraySegment<byte> b = new ArraySegment<byte>(buffer, index, BatchStart.Array.Length);

                    if (b.Equals<byte>(BatchStart))
                    {
                        stream.Position = start + index + BatchStart.Array.Length;
                        return;
                    }

                    index = Array.FindIndex(buffer, index + 1, n => n == match);
                }

                start = stream.Position -= BatchStart.Array.Length;
                read = stream.Read(buffer, 0, buffer.Length);
            }
        }

        public string WorkingPath { get; set; }
        public int BatchSize { get; protected set; }

        public Stream GetWritableFileStream(string fileNamePath)
        {
            return new FileStream(fileNamePath, FileMode.OpenOrCreate
                , FileSystemRights.Write | FileSystemRights.CreateFiles
                , FileShare.None, _bufferSize, FileOptions.WriteThrough);
        }

        public Stream GetReadableFileStream(string fileNamePath)
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
                long pos = stream.Position;
                var len = stream.Length;
                if (len - pos < SegmentDelimeter.Array.Length)
                    return 0;

                lock (_syncRoot)
                {
                    var buffer = new byte[_bufferSize];

                    int read = stream.Read(buffer, 0, buffer.Length);
                    int count = 0;

                    ArraySegment<byte> s = new ArraySegment<byte>(buffer, 0, SeedStart.Array.Length);
                    if (s.Equals<byte>(SeedStart))
                    {
                        FindBatchStart(stream);
                        read = stream.Read(buffer, 0, buffer.Length);
                    }

                    ArraySegment<byte> c = new ArraySegment<byte>(buffer, 0, BatchStart.Array.Length);
                    if (c.Equals<byte>(BatchStart))
                    {
                        count = _batchConverter.FromBytes(buffer.Skip(BatchStart.Array.Length).Take(4).ToArray());
                        buffer = buffer.Skip(BatchStart.Array.Length + 4).ToArray();
                    }
                    else
                    {
                        count = _batchConverter.FromBytes(buffer.Take(4).ToArray());
                        buffer = buffer.Skip(4).ToArray();
                    }

                    pos = stream.Position;

                    var match = SegmentDelimeter.Array[0];
                    var delLength = SegmentDelimeter.Array.Length;

                    while (read > SegmentDelimeter.Array.Length)
                    {
                        var index = Array.FindIndex(buffer, b => b == match);

                        while (index >= 0 && index <= buffer.Length - delLength)
                        {
                            ArraySegment<byte> b = new ArraySegment<byte>(buffer, index, delLength);

                            if (b.Equals<byte>(SegmentDelimeter))
                            {
                                stream.Position = pos + index + delLength;

                                count += _batchConverter.FromStream(stream);

                                break;
                            }

                            index = Array.FindIndex(buffer, index + 1, n => n == match);
                        }

                        pos = stream.Position -= delLength;

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
                using (Stream bufferedStream = new MemoryStream())
                {
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

                        pos += (SeedStart.Array.Length);
                        buffer = buffer.Skip((int)pos).ToArray();

                        while (read > SegmentDelimeter.Array.Length)
                        {
                            var index = Array.FindIndex(buffer, b => b == match);

                            while (index >= 0 && index <= buffer.Length - delLength)
                            {
                                ArraySegment<byte> b = new ArraySegment<byte>(buffer, index, delLength);

                                if (b.Equals<byte>(SegmentDelimeter))
                                {
                                    bufferedStream.Write(buffer, 0, index);

                                    stream.Position = pos + index + delLength;

                                    var seed = _formatter.UnformatObj<ISeed<IdType>>(bufferedStream);

                                    if (seed.MinimumSeedStride >= stream.Position)
                                        stream.Position = seed.MinimumSeedStride;
                                    else
                                        seed.MinimumSeedStride = (int)stream.Position;

                                    stream.Position += SegmentDelimeter.Array.Length;

                                    return seed;
                                }

                                index = Array.FindIndex(buffer, index + 1, n => n == match);
                            }

                            bufferedStream.Write(buffer, 0, buffer.Length - delLength);

                            pos = stream.Position -= delLength;

                            Array.Resize(ref buffer, _bufferSize);
                            read = stream.Read(buffer, 0, buffer.Length);
                        }
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
                long pos = stream.Position;
                var len = stream.Length;
                if (len - pos < SegmentDelimeter.Array.Length)
                    return new List<T>();

                lock (_syncRoot)
                {
                    var buffer = new byte[_bufferSize];

                    int read = stream.Read(buffer, 0, buffer.Length);
                    int count = 0;

                    ArraySegment<byte> s = new ArraySegment<byte>(buffer, 0, SeedStart.Array.Length);
                    if (s.Equals<byte>(SeedStart))
                    {
                        FindBatchStart(stream);
                        read = stream.Read(buffer, 0, buffer.Length);
                    }

                    ArraySegment<byte> c = new ArraySegment<byte>(buffer, 0, BatchStart.Array.Length);
                    if (c.Equals<byte>(BatchStart))
                    {
                        count = _batchConverter.FromBytes(buffer.Skip(BatchStart.Array.Length).Take(4).ToArray());
                        buffer = buffer.Skip(BatchStart.Array.Length + 4).ToArray();
                    }
                    else
                    {
                        count = _batchConverter.FromBytes(buffer.Take(4).ToArray());
                        buffer = buffer.Skip(4).ToArray();
                    }

                    pos = stream.Position;

                    var match = SegmentDelimeter.Array[0];
                    var delLength = SegmentDelimeter.Array.Length;

                    using (var bufferStream = new MemoryStream())
                    {
                        while (read > SegmentDelimeter.Array.Length)
                        {
                            var index = Array.FindIndex(buffer, b => b == match);

                            while (index >= 0 && index <= buffer.Length - delLength)
                            {
                                ArraySegment<byte> b = new ArraySegment<byte>(buffer, index, delLength);

                                if (b.Equals<byte>(SegmentDelimeter))
                                {
                                    bufferStream.Write(buffer, 0, index);

                                    stream.Position = pos + index + delLength;

                                    if (count > 0)
                                        return _formatter.UnformatObj<T[]>(bufferStream).ToList();
                                    else
                                        return new List<T>();
                                }

                                index = Array.FindIndex(buffer, index + 1, n => n == match);
                            }

                            bufferStream.Write(buffer, 0, buffer.Length - delLength);

                            pos = stream.Position -= delLength;

                            read = stream.Read(buffer, 0, buffer.Length);
                        }
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
                using (var seedStream = _formatter.FormatObjStream(seed))
                {
                    seedStream.Position = 0;

                    lock (_syncRoot)
                    {
                        var buffer = new byte[_bufferSize];

                        stream.Position = 0;

                        stream.Write(SeedStart.Array, 0, SeedStart.Array.Length);

                        seedStream.WriteAllTo(stream);

                        stream.Write(SegmentDelimeter.Array, 0, SegmentDelimeter.Array.Length);

                        if (stream.Position < seed.MinimumSeedStride)
                            stream.Position = seed.MinimumSeedStride;

                        stream.Write(BatchStart.Array, 0, BatchStart.Array.Length);

                        var position = stream.Position;

                        stream.Flush();

                        return position;
                    }

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
                var formatted = _formatter.FormatObjStream(objs.ToArray());

                lock (_syncRoot)
                {
                    if (atPosition >= 0)
                        stream.Position = atPosition;

                    if (stream.Position == 0)
                        stream.Write(BatchStart.Array, 0, BatchStart.Array.Length);

                    stream.Write(_batchConverter.ToBytes(objs.Count), 0, _batchConverter.Length);

                    formatted.WriteAllTo(stream);

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

        public void Replace(string fromFileName, string toFileName)
        {
            File.Replace(fromFileName, toFileName, toFileName + ".old", true);
        }

        public void Dispose()
        {
            
        }
    }
}
