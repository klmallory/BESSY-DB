using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LZ4;
using System.IO;
using BESSy.Extensions;
using BESSy.Json;
using System.IO.Compression;
using BESSy.Json.Linq;
using System.Security;

namespace BESSy.Serialization
{
    [SecuritySafeCritical]
    public class LZ4ZipFormatter : IQueryableFormatter
    {
        public LZ4ZipFormatter(IQueryableFormatter serializer)
            : this(serializer, false, 1024000)
        { }

        public LZ4ZipFormatter(IQueryableFormatter serializer, bool highCompress, int blockSize)
        {
            _serializer = serializer;
            _highCompress = highCompress;
            _blockSize = blockSize;
        }

        int _blockSize;
        bool _highCompress;
        IQueryableFormatter _serializer;

        public Json.JsonSerializer Serializer
        {
            get { return _serializer.Serializer; }
        }

        public JsonSerializerSettings Settings
        {
            get
            {
                return _serializer != null ? _serializer.Settings : null;
            }
            set
            {
                if (_serializer != null && _serializer.Settings != null)
                    _serializer.Settings = value;
            }
        }

        public virtual bool Trim { get { return true; } }

        public virtual ArraySegment<byte> TrimMarker { get { return new ArraySegment<byte>(new byte[] { 1, 1, 1, 1 }); } }

        public JObject AsQueryableObj<T>(T obj)
        {
            if (obj != null)
                return JObject.FromObject(obj, Serializer);
            else
                return new JObject();
        }

        public Json.Linq.JObject Parse(System.IO.Stream inStream)
        {
            using (var stream = Unformat(inStream))
                return _serializer.Parse(stream);
        }

        public Stream Unparse(JObject token)
        {
            return Format(_serializer.Unparse(token));
        }

        public bool TryParse(Stream inStream, out JObject obj)
        {
            try
            {
                obj = Parse(inStream);
                return true;
            }
            catch (Exception) { obj = null; return false; }
        }

        public bool TryUnparse(JObject token, out Stream stream)
        {
            try
            {
                stream = Unparse(token);

                stream.Write(TrimMarker.Array, 0, TrimMarker.Array.Length);

                return true;
            }
            catch (Exception) { stream = null; return false; }

        }
        public bool TryFormatObj<T>(T obj, out byte[] buffer)
        {
            try
            {
                buffer = FormatObj<T>(obj);

                return true;
            }
            catch (JsonException) { }
            catch (SystemException) { }
            catch (ApplicationException) { }

            buffer = new byte[0];

            return false; 
        }

        public bool TryFormatObj<T>(T obj, out System.IO.Stream outStream)
        {
            try
            {
                outStream = FormatObjStream<T>(obj);

                return true;
            }
            catch (JsonException) { }
            catch (SystemException) { }
            catch (ApplicationException) { }

            outStream = new MemoryStream();

            return false; 
        }

        [SecuritySafeCritical]
        public byte[] FormatObj<T>(T obj)
        {
            var stream = _serializer.FormatObjStream<T>(obj);

            var compressed = new MemoryStream();

            var lz4 = new LZ4.LZ4Stream(compressed, System.IO.Compression.CompressionMode.Compress, _highCompress, _blockSize);

            stream.WriteAllTo(lz4);

            lz4.Flush();

            compressed.Write(TrimMarker.Array, 0, TrimMarker.Array.Length);

            return compressed.ToArray();
        }

        [SecuritySafeCritical]
        public System.IO.Stream FormatObjStream<T>(T obj)
        {
            var stream = _serializer.FormatObjStream<T>(obj);

            var compressed = new MemoryStream();

            var lz4 = new LZ4.LZ4Stream(compressed, System.IO.Compression.CompressionMode.Compress, _highCompress, _blockSize);

             stream.WriteAllTo(lz4);

             lz4.Flush();

             compressed.Write(TrimMarker.Array, 0, TrimMarker.Array.Length);

             return compressed;
        }

        [SecuritySafeCritical]
        public T UnformatObj<T>(byte[] buffer)
        {
            Array.Resize(ref buffer, buffer.Length - TrimMarker.Count);

            using (var compressed = new MemoryStream(buffer))
            {
                var lz4 = new LZ4.LZ4Stream(compressed, System.IO.Compression.CompressionMode.Decompress, _highCompress, _blockSize);

                using (var uncompressed = new MemoryStream())
                {
                    lz4.WriteAllFromCurrentPositionTo(uncompressed);

                    return _serializer.UnformatObj<T>(uncompressed);
                }
            }
        }

        [SecuritySafeCritical]
        public T UnformatObj<T>(System.IO.Stream inStream)
        {
            inStream.Position = 0;
            inStream.SetLength(inStream.Length - TrimMarker.Count);

            var lz4 = new LZ4.LZ4Stream(inStream, System.IO.Compression.CompressionMode.Decompress, _highCompress, _blockSize);

            using (var uncompressed = new MemoryStream())
            {
                lz4.WriteAllFromCurrentPositionTo(uncompressed);

                return _serializer.UnformatObj<T>(uncompressed);
            }
        }

        public bool TryUnformatObj<T>(byte[] buffer, out T obj)
        {
            try
            {
                obj = UnformatObj<T>(buffer);

                return true;
            }
            catch (JsonException) { }
            catch (SystemException) { }
            catch (ApplicationException) { }

            obj = default(T);

            return false;
        }

        public bool TryUnformatObj<T>(System.IO.Stream stream, out T obj)
        {
            try
            {
                obj = UnformatObj<T>(stream);

                return true;
            }
            catch (JsonException) {  }
            catch (SystemException) {  }
            catch (ApplicationException) { }

            obj = default(T);

            return false;
        }

        [SecuritySafeCritical]
        public byte[] Format(byte[] buffer)
        {
            using (var output = new MemoryStream())
            {
                var lz4 = new LZ4.LZ4Stream(output, CompressionMode.Compress, _highCompress, _blockSize);

                using (var ms = new MemoryStream(buffer, true))
                {
                    ms.WriteAllTo(lz4);

                    lz4.Flush();

                    output.Write(TrimMarker.Array, 0, TrimMarker.Array.Length);

                    return output.ToArray();
                }
            }
        }

        [SecuritySafeCritical]
        public Stream Format(System.IO.Stream inStream)
        {
            var output = new MemoryStream();

            var lz4 = new LZ4.LZ4Stream(output, CompressionMode.Compress, _highCompress, _blockSize);

            inStream.Position = 0;

            inStream.WriteAllTo(lz4);

            lz4.Flush();

            output.Write(TrimMarker.Array, 0, TrimMarker.Array.Length);

            return output;
        }

        [SecuritySafeCritical]
        public byte[] Unformat(byte[] buffer)
        {
            Array.Resize(ref buffer, buffer.Length - TrimMarker.Count);

            var compressed = new MemoryStream(buffer);

            var lz4 = new LZ4.LZ4Stream(compressed, CompressionMode.Decompress, _highCompress, _blockSize);

            var uncompressed = new MemoryStream();

            lz4.WriteAllFromCurrentPositionTo(uncompressed);

            //uncompressed.Write(TrimMarker.Array, 0, TrimMarker.Array.Length);

            return uncompressed.ToArray();
        }

        [SecuritySafeCritical]
        public System.IO.Stream Unformat(System.IO.Stream inStream)
        {
            inStream.SetLength(inStream.Length - TrimMarker.Count);

            inStream.Position = 0;

            var lz4 = new LZ4.LZ4Stream(inStream, CompressionMode.Decompress, _highCompress, _blockSize);

            var uncompressed = new MemoryStream();

            lz4.WriteAllFromCurrentPositionTo(uncompressed);

            return uncompressed;
        }
    }
}
