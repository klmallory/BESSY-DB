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
using BESSy.Extensions;
using BESSy.Seeding;
using BESSy.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BESSy.Files
{
    public interface IQueryableFileManager<EntityType> : ISegmentedFileManager<EntityType>
    {
        IEnumerable<JObject> AsQueryable();
    }

    public interface ISegmentedFileManager<EntityType> : IFileManager, IDisposable
    {
        long SaveSegment(byte[] buffer, Stream stream, long atPosition);
        EntityType LoadSegmentFrom(Stream stream);
        ISeed<IdType> LoadSeedFrom<IdType>(Stream stream);
        long SaveSeed<IdType>(Stream stream, ISeed<IdType> seed);
    }

    public class SegmentedFileManager<EntityType> : ISegmentedFileManager<EntityType>
    {
        //TODO: this needs to come from the Formatter, not the file manager. That way the delimeter can be specific to the encoding.
        internal readonly static ArraySegment<byte> SeedStart = new ArraySegment<byte>
            (new byte[] { 4, 4, 4, 4, 4, 4, 4, 4, 4 });

        internal readonly static ArraySegment<byte> SegmentDelimeter = new ArraySegment<byte>
            (new byte[] { 3, 3, 3, 3, 3, 3, 3, 3, 3 });

        public SegmentedFileManager(IFormatter formatter)
            : this(Environment.SystemPageSize.Clamp(2048, 8192), formatter)
        {
        }

        public SegmentedFileManager(int bufferSize, IFormatter formatter)
            : this(formatter)
        {
            _bufferSize = bufferSize;
            _formatter = formatter;
        }

        object _syncRoot = new object();
        IFormatter _formatter;
        int _bufferSize = 1024;
        string _error = "File name {0}, could not be found or accessed: {1}.";

        public string WorkingPath { get; set; }
        public int RowSize { get; protected set; }

        public FileStream GetWritableFileStream(string fileNamePath)
        {
            return new FileStream(fileNamePath, FileMode.OpenOrCreate
            , FileAccess.ReadWrite, FileShare.ReadWrite
            , _bufferSize, true);
        }

        public FileStream GetReadableFileStream(string fileNamePath)
        {
            return new FileStream(fileNamePath, FileMode.Open
                , FileAccess.Read, FileShare.ReadWrite
                , _bufferSize, true);
        }

        public long SaveSeed<IdType>(Stream stream, ISeed<IdType> seed)
        {

#if DEBUG
            if (object.Equals(seed, default(ISeed<IdType>)))
                throw new ArgumentNullException("seed");
#endif

            try
            {
                byte[] buffer = _formatter.FormatObj(seed);

                if (buffer.Length > seed.MinimumSeedStride)
                {
                    seed.MinimumSeedStride = buffer.Length;
                    buffer = _formatter.FormatObj(seed);
                }
                else
                    Array.Resize(ref buffer, seed.MinimumSeedStride);

                stream.Position = 0;

                stream.Write(SeedStart.Array, 0, SeedStart.Array.Length);
                stream.Write(buffer, 0, buffer.Length);
                stream.Write(SegmentDelimeter.Array, 0, SegmentDelimeter.Array.Length);

                var position = stream.Position;

                stream.Flush();

                return position;
            }
            catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format("File could not be serialized : \r\n {0} \r\n {1}", jsEx.InnerException, jsEx)); throw; }
            catch (UnauthorizedAccessException uaaEx) { Trace.TraceError(String.Format(_error, "", uaaEx)); throw; }
            catch (ArgumentNullException agnEx) { Trace.TraceError(String.Format(_error, "", agnEx)); throw; }
            catch (DriveNotFoundException rnfEx) { Trace.TraceError(String.Format(_error, "", rnfEx)); throw; }
            catch (DirectoryNotFoundException dnfEx) { Trace.TraceError(String.Format(_error, "", dnfEx)); throw; }
            catch (FileNotFoundException fnfEx) { Trace.TraceError(String.Format(_error, "", fnfEx)); throw; }
            catch (ArgumentException argEx) { Trace.TraceError(String.Format(_error, "", argEx)); throw; }
            catch (PathTooLongException ptlEx) { Trace.TraceError(String.Format(_error, "", ptlEx)); throw; }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_error, "", ioEx)); throw; }
        }

        public long SaveSegment(byte[] buffer, Stream stream, long atPosition = -1)
        {
#if DEBUG
            if (buffer.Length > RowSize)
                throw new InternalBufferOverflowException("buffer is larger ({0} bytes) than the current rowSize ({1} bytes); rebuild the data file before adding this entity.");
#endif
            try
            {
                if (atPosition >= 0)
                    stream.Position = atPosition;

                stream.Write(buffer, 0, buffer.Length);
                stream.Write(SegmentDelimeter.Array, 0, SegmentDelimeter.Array.Length);

                var position = stream.Position;

                stream.Flush();

                return position;
            }
            catch (UnauthorizedAccessException uaaEx) { Trace.TraceError(String.Format(_error, "", uaaEx)); throw; }
            catch (ArgumentNullException agnEx) { Trace.TraceError(String.Format(_error, "", agnEx)); throw; }
            catch (DriveNotFoundException rnfEx) { Trace.TraceError(String.Format(_error, "", rnfEx)); throw; }
            catch (DirectoryNotFoundException dnfEx) { Trace.TraceError(String.Format(_error, "", dnfEx)); throw; }
            catch (FileNotFoundException fnfEx) { Trace.TraceError(String.Format(_error, "", fnfEx)); throw; }
            catch (ArgumentException argEx) { Trace.TraceError(String.Format(_error, "", argEx)); throw; }
            catch (PathTooLongException ptlEx) { Trace.TraceError(String.Format(_error, "", ptlEx)); throw; }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_error, "", ioEx)); throw; }
        }


        public ISeed<IdType> LoadSeedFrom<IdType>(Stream stream)
        {
            try
            {
                byte[] buffers = new byte[0];

                if (stream.Length < SegmentDelimeter.Array.Length)
                    return default(ISeed<IdType>);

                stream.Position = 0;

                var match = SegmentDelimeter.Array[0];
                var delLength = SegmentDelimeter.Array.Length;

                var buffer = new byte[_bufferSize + delLength];

                int read = stream.Read(buffer, 0, buffer.Length);

                ArraySegment<byte> s = new ArraySegment<byte>(buffer, 0, SeedStart.Array.Length);
                if (!s.Equals<byte>(SeedStart))
                    return default(ISeed<IdType>);

                while (read > SegmentDelimeter.Array.Length)
                {
                    var index = Array.FindIndex(buffer, b => b == match);

                    while (index >= 0 && index <= _bufferSize)
                    {
                        ArraySegment<byte> b = new ArraySegment<byte>(buffer, index, delLength);

                        if (b.Equals<byte>(SegmentDelimeter))
                        {
                            var last = buffers.Length;
                            var seg = buffer.Take(index).ToArray();
                            Array.Resize(ref buffers, last + index);
                            Array.Copy(seg, 0, buffers, last, seg.Length);

                            stream.Position = index + delLength;

                            var seed = _formatter.UnformatObj<ISeed<IdType>>(buffers.Skip(SeedStart.Array.Length).ToArray());

                            RowSize = seed.Stride;
                        }

                        index = Array.FindIndex(buffer, index + 1, n => n == match);
                    }

                    Array.Resize(ref buffers, buffers.Length + (_bufferSize - delLength));
                    Array.Copy(buffer.Take(_bufferSize).ToArray(), 0, buffers, buffers.Length - (_bufferSize - delLength), _bufferSize - delLength);
                    stream.Position -= delLength;
                    read = stream.Read(buffer, 0, buffer.Length);
                }
            }
            catch (JsonSerializationException jsEx) { Trace.TraceError(String.Format("File could not be deserialized : \r\n {0} \r\n {1}", jsEx.InnerException, jsEx)); throw; }
            catch (UnauthorizedAccessException uaaEx) { Trace.TraceError(String.Format(_error, "", uaaEx)); }
            catch (ArgumentNullException agnEx) { Trace.TraceError(String.Format(_error, "", agnEx)); }
            catch (DriveNotFoundException rnfEx) { Trace.TraceError(String.Format(_error, "", rnfEx)); }
            catch (DirectoryNotFoundException dnfEx) { Trace.TraceError(String.Format(_error, "", dnfEx)); }
            catch (FileNotFoundException fnfEx) { Trace.TraceError(String.Format(_error, "", fnfEx)); }
            catch (ArgumentException argEx) { Trace.TraceError(String.Format(_error, "", argEx)); }
            catch (PathTooLongException ptlEx) { Trace.TraceError(String.Format(_error, "", ptlEx)); }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_error, "", ioEx)); }

            return default(ISeed<IdType>);
        }

        public EntityType LoadSegmentFrom(Stream stream)
        {
            try
            {
                byte[] buffers = new byte[0];

                long pos = stream.Position;

                var match = SegmentDelimeter.Array[0];
                var delLength = SegmentDelimeter.Array.Length;

                var buffer = new byte[_bufferSize + delLength];
                int read = stream.Read(buffer, 0, buffer.Length);

                while (read > 0)
                {
                    var index = Array.FindIndex(buffer, b => b == match);

                    while (index >= 0 && index <= _bufferSize)
                    {
                        ArraySegment<byte> b = new ArraySegment<byte>(buffer, index, delLength);

                        if (b.Equals<byte>(SegmentDelimeter))
                        {
                            var last = buffers.Length;
                            var seg = buffer.Take(index).ToArray();
                            Array.Resize(ref buffers, last + index);
                            Array.Copy(seg, 0, buffers, last, seg.Length);

                            stream.Position = pos + index + delLength;

                            return _formatter.UnformatObj<EntityType>(buffers);
                        }

                        index = Array.FindIndex(buffer, index + 1, n => n == match);
                    }

                    Array.Resize(ref buffers, buffers.Length + _bufferSize);
                    Array.Copy(buffer.Take(_bufferSize).ToArray(), 0, buffers, buffers.Length - _bufferSize, _bufferSize);
                    stream.Position -= delLength;
                    pos = stream.Position;
                    read = stream.Read(buffer, 0, buffer.Length);
                }
            }
            catch (UnauthorizedAccessException uaaEx) { Trace.TraceError(String.Format(_error, "", uaaEx)); }
            catch (ArgumentNullException agnEx) { Trace.TraceError(String.Format(_error, "", agnEx)); }
            catch (DriveNotFoundException rnfEx) { Trace.TraceError(String.Format(_error, "", rnfEx)); }
            catch (DirectoryNotFoundException dnfEx) { Trace.TraceError(String.Format(_error, "", dnfEx)); }
            catch (FileNotFoundException fnfEx) { Trace.TraceError(String.Format(_error, "", fnfEx)); }
            catch (ArgumentException argEx) { Trace.TraceError(String.Format(_error, "", argEx)); }
            catch (PathTooLongException ptlEx) { Trace.TraceError(String.Format(_error, "", ptlEx)); }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_error, "", ioEx)); }

            return default(EntityType);
        }

        public void Dispose()
        {

        }
    }
}
