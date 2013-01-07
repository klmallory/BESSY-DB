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

namespace BESSy.Files
{
    public interface ISegmentedFileManager<EntityType> : IFileManager, IDisposable
    {
        void SaveSegment(EntityType entity, Stream stream, int segment);
        EntityType LoadSegmentFrom(Stream stream, int segment);
        int GetSegmentCount(string fileNamePath);
        int GetSegmentCount(Stream stream);
        ISeed<IdType> LoadSeedFrom<IdType>(Stream stream);
        long SaveSeed<IdType>(Stream stream, ISeed<IdType> seed);
    }

    public class SegmentedFileManager<T> : ISegmentedFileManager<T>
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

        public FileStream GetWritableFileStream(string fileNamePath)
        {
            return new FileStream(fileNamePath, FileMode.OpenOrCreate
            , FileSystemRights.Write | FileSystemRights.CreateFiles
            , FileShare.None, _bufferSize, FileOptions.Asynchronous);
        }

        public FileStream GetReadableFileStream(string fileNamePath)
        {
            return new FileStream(fileNamePath, FileMode.Open
                , System.Security.AccessControl.FileSystemRights.Read
                , FileShare.Read, _bufferSize, FileOptions.RandomAccess);
        }

        public IList<T> LoadFromFile(string fileName, string path)
        {
            var filePath = Path.Combine(path, fileName);

            return LoadFromFile(filePath);
        }

        public IList<T> LoadFromFile(string fileNamePath)
        {
            try
            {
                if (!File.Exists(fileNamePath))
                    return new List<T>();

                lock (_syncRoot)
                    using (var stream = GetReadableFileStream(fileNamePath))
                        return _formatter.UnformatObj<IList<T>>(stream);
            }
            catch (UnauthorizedAccessException uaaEx) { Trace.TraceError(String.Format(_error, fileNamePath, uaaEx)); }
            catch (ArgumentNullException agnEx) { Trace.TraceError(String.Format(_error, fileNamePath, agnEx)); }
            catch (DriveNotFoundException rnfEx) { Trace.TraceError(String.Format(_error, fileNamePath, rnfEx)); }
            catch (DirectoryNotFoundException dnfEx) { Trace.TraceError(String.Format(_error, fileNamePath, dnfEx)); }
            catch (FileNotFoundException fnfEx) { Trace.TraceError(String.Format(_error, fileNamePath, fnfEx)); }
            catch (ArgumentException argEx) { Trace.TraceError(String.Format(_error, fileNamePath, argEx)); }
            catch (PathTooLongException ptlEx) { Trace.TraceError(String.Format(_error, fileNamePath, ptlEx)); }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_error, fileNamePath, ioEx)); }

            return new List<T>();
        }

        public Stream LoadAsStream(string fileName, string path)
        {
            var filePath = Path.Combine(path, fileName);

            return LoadAsStream(filePath);
        }

        public Stream LoadAsStream(string fileNamePath)
        {
            if (!File.Exists(fileNamePath))
                return null;

            try
            {
                using (var stream = GetReadableFileStream(fileNamePath))
                {
                    return _formatter.Unformat(stream);
                }
            }
            catch (UnauthorizedAccessException uaaEx) { Trace.TraceError(String.Format(_error, fileNamePath, uaaEx)); }
            catch (ArgumentNullException agnEx) { Trace.TraceError(String.Format(_error, fileNamePath, agnEx)); }
            catch (DriveNotFoundException rnfEx) { Trace.TraceError(String.Format(_error, fileNamePath, rnfEx)); }
            catch (DirectoryNotFoundException dnfEx) { Trace.TraceError(String.Format(_error, fileNamePath, dnfEx)); }
            catch (FileNotFoundException fnfEx) { Trace.TraceError(String.Format(_error, fileNamePath, fnfEx)); }
            catch (ArgumentException argEx) { Trace.TraceError(String.Format(_error, fileNamePath, argEx)); }
            catch (PathTooLongException ptlEx) { Trace.TraceError(String.Format(_error, fileNamePath, ptlEx)); }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_error, fileNamePath, ioEx)); }

            return null;
        }

        public void SaveToFile(IList<T> obj, string fileName, string path)
        {
#if DEBUG
            if (obj == null)
                throw new ArgumentNullException("obj");
#endif
            var filePath = Path.Combine(path, fileName);

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            SaveToFile(obj, filePath);

        }

        public void SaveToFile(IList<T> obj, string fileNamePath)
        {

            try
            {
                lock (_syncRoot)
                    using (var stream = GetWritableFileStream(fileNamePath))
                    {
                        var data = _formatter.FormatObj(obj);

                        stream.Position = 0;

                        stream.Write(data, 0, data.Length);

                        stream.SetLength(data.Length);

                        stream.Flush();

                        stream.Close();
                    }
            }
            catch (UnauthorizedAccessException uaaEx) { Trace.TraceError(String.Format(_error, fileNamePath, uaaEx)); }
            catch (ArgumentNullException agnEx) { Trace.TraceError(String.Format(_error, fileNamePath, agnEx)); }
            catch (DriveNotFoundException rnfEx) { Trace.TraceError(String.Format(_error, fileNamePath, rnfEx)); }
            catch (DirectoryNotFoundException dnfEx) { Trace.TraceError(String.Format(_error, fileNamePath, dnfEx)); }
            catch (FileNotFoundException fnfEx) { Trace.TraceError(String.Format(_error, fileNamePath, fnfEx)); }
            catch (ArgumentException argEx) { Trace.TraceError(String.Format(_error, fileNamePath, argEx)); }
            catch (PathTooLongException ptlEx) { Trace.TraceError(String.Format(_error, fileNamePath, ptlEx)); }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_error, fileNamePath, ioEx)); }
        }

        public void SaveToFile(byte[] data, string fileName, string path)
        {
#if DEBUG
            if (data == null)
                throw new ArgumentNullException("obj");
#endif

            var filePath = Path.Combine(path, fileName);

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            SaveToFile(data, filePath);

        }

        public void SaveToFile(byte[] data, string fileNamePath)
        {
            try
            {
                var zipped = _formatter.Format(data);

                lock (_syncRoot)
                    using (var stream = GetWritableFileStream(fileNamePath))
                    {
                        stream.Position = 0;

                        stream.Write(data, 0, data.Length);

                        stream.SetLength(data.Length);

                        stream.Flush();

                        stream.Close();
                    }
            }
            catch (UnauthorizedAccessException uaaEx) { Trace.TraceError(String.Format(_error, fileNamePath, uaaEx)); throw; }
            catch (ArgumentNullException agnEx) { Trace.TraceError(String.Format(_error, fileNamePath, agnEx)); throw; }
            catch (DriveNotFoundException rnfEx) { Trace.TraceError(String.Format(_error, fileNamePath, rnfEx)); throw; }
            catch (DirectoryNotFoundException dnfEx) { Trace.TraceError(String.Format(_error, fileNamePath, dnfEx)); throw; }
            catch (FileNotFoundException fnfEx) { Trace.TraceError(String.Format(_error, fileNamePath, fnfEx)); throw; }
            catch (ArgumentException argEx) { Trace.TraceError(String.Format(_error, fileNamePath, argEx)); throw; }
            catch (PathTooLongException ptlEx) { Trace.TraceError(String.Format(_error, fileNamePath, ptlEx)); throw; }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_error, fileNamePath, ioEx)); throw; }
        }

        public void SaveToStream(byte[] data, Stream stream)
        {
            try
            {
                var zipped = _formatter.Format(data);

                lock (_syncRoot)
                {
                    stream.Position = 0;

                    stream.Write(data, 0, data.Length);

                    stream.SetLength(data.Length);

                    stream.Flush();

                    stream.Close();
                }
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

        public void OverwriteStream(byte[] data, Stream stream)
        {
#if DEBUG
            if (data == null)
                throw new ArgumentNullException("data");
#endif

            try
            {
                lock (_syncRoot)
                {
                    var zipped = _formatter.Format(data);

                    stream.Position = 0;

                    stream.Write(data, 0, data.Length);

                    stream.SetLength(data.Length);

                    stream.Flush();
                }
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

        public void OverwriteStream(IList<T> obj, Stream stream)
        {
#if DEBUG
            if (obj == null)
                throw new ArgumentNullException("obj");
#endif

            try
            {
                lock (_syncRoot)
                {
                    var zipped = _formatter.FormatObj(obj);

                    stream.Position = 0;

                    stream.Write(zipped, 0, zipped.Length);

                    stream.SetLength(zipped.Length);

                    stream.Flush();
                }
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

        #region ISegmentedFileManager<T> Members

        public int GetSegmentCount(string fileNamePath)
        {
            using (var stream = GetReadableFileStream(fileNamePath))
                return GetSegmentCount(stream);
        }

        public int GetSegmentCount(Stream stream)
        {
            int count = 0;

            try
            {
                int read = 1;

                var match = SegmentDelimeter.Array[0];
                var delLength = SegmentDelimeter.Array.Length;

                while (read > 0)
                {
                    var pos = stream.Position;

                    var buffer = new byte[_bufferSize + delLength];

                    read = stream.Read(buffer, 0, buffer.Length);

                    ArraySegment<byte> s = new ArraySegment<byte>(buffer, 0, SeedStart.Array.Length);
                    if (s.Equals<byte>(SeedStart))
                        count -= 1;

                    var index = Array.FindIndex(buffer, b => b == match);

                    while (index >= 0 && index <= _bufferSize)
                    {
                        ArraySegment<byte> aSeg = new ArraySegment<byte>(buffer, index, delLength);

                        if (aSeg == SegmentDelimeter)
                        {
                            count++;

                            stream.Position = pos + index + delLength + 1;

                            break;
                        }

                        index = Array.FindIndex(buffer, index + 1, n => n == match);
                    }
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

            return count;
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

        public long SaveSegmentToStream(T obj, Stream stream, long atPosition = -1)
        {
#if DEBUG
            if (obj == null)
                throw new ArgumentNullException("obj");
#endif
            try
            {
                byte[] zipped = _formatter.FormatObj(obj);

                if (atPosition >= 0)
                    stream.Position = atPosition;

                stream.Write(zipped, 0, zipped.Length);
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

                            return _formatter.UnformatObj<ISeed<IdType>>(buffers.Skip(SeedStart.Array.Length).ToArray());
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

        public T LoadSegmentFrom(Stream stream)
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

                            return _formatter.UnformatObj<T>(buffers);
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

            return default(T);
        }

        public IList<T> LoadAllSegments(string fileNamePath)
        {
            using (var stream = GetReadableFileStream(fileNamePath))
            {
                return LoadAllSegments(stream);
            }
        }

        public IList<T> LoadAllSegments(Stream stream)
        {
            var segments = new List<T>();

            var item = LoadSegmentFrom(stream);

            while (!object.Equals(item, default(T)))
            {
                segments.Add(item);

                item = LoadSegmentFrom(stream);
            }

            return segments;
        }

        public void SaveAllSegments(string fileNamePath, IList<T> items)
        {
            using (var stream = GetWritableFileStream(fileNamePath))
            {
                SaveAllSegments(stream, items);
            }
        }

        public void SaveAllSegments(Stream stream, IList<T> items)
        {
            long pos = 0;

            foreach (var i in items)
            {
                pos = SaveSegmentToStream(i, stream, pos);
            }
        }

        #endregion

        public void Dispose()
        {

        }

        #region ISegmentedFileManager<T> Members

        public void SaveSegment(T entity, Stream stream, int segment)
        {
            throw new NotImplementedException();
        }

        public T LoadSegmentFrom(Stream stream, int segment)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
