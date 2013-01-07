/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security;
using BESSy.Crypto;
using Newtonsoft.Json;
using SevenZip;
using SevenZip.LZMA;
using SECP = System.Security.Permissions;

namespace BESSy.Serialization
{
    //[SecurityCritical()]
    //[SECP.KeyContainerPermission(SECP.SecurityAction.Demand)]
    //[SECP.ReflectionPermission(SECP.SecurityAction.Demand)]
    public class QuickZipFormatter : ISafeFormatter
    {
        public static IDictionary<CoderPropID, object> DefaultProperties = new Dictionary<CoderPropID, object>()
        {
            {CoderPropID.DictionarySize, 16},
            {CoderPropID.PosStateBits, 2},
            {CoderPropID.LitContextBits, 3},
            {CoderPropID.LitPosBits, 0},
            {CoderPropID.Algorithm, 2},
            {CoderPropID.NumFastBytes, 128},
            {CoderPropID.MatchFinder, "bt4"},
            {CoderPropID.EndMarker, false}
        };

        public QuickZipFormatter(IFormatter serializer) : this(serializer, DefaultProperties)
        {        }

        public QuickZipFormatter(IFormatter serializer, IDictionary<CoderPropID, object> properties)
        {
            _serializer = serializer;
            _properties = properties;
        }

        Encoder _quickEncoder = new Encoder();
        Decoder _quickDecoder = new Decoder();
        IDictionary<CoderPropID, object> _properties;
        IFormatter _serializer;

        protected Stream Unzip(Stream inStream)
        {
            byte[] zipProps = new byte[5];

            if (inStream.Read(zipProps, 0, 5) != 5)
                throw (new Exception("input .lzma is too short"));

            _quickDecoder.SetDecoderProperties(zipProps);

            long outSize = 0;
            for (int i = 0; i < 8; i++)
            {
                int v = inStream.ReadByte();
                if (v < 0)
                    throw (new Exception("Can't Read 1"));
                outSize |= ((long)(byte)v) << (8 * i);
            }

            long compressedSize = inStream.Length - inStream.Position;

            Stream outStream = new MemoryStream();

            _quickDecoder.Code(inStream, outStream, compressedSize, outSize, null);

            outStream.Flush();

            return outStream;
        }

        /// <summary>
        /// Unzips the object of type <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T">Type of the object to format.</typeparam>
        /// <param name="obj">object to format.</param>
        /// <returns>raw compressed / encrypted data.</returns>
        public Byte[] FormatObj<T>(T obj)
        {
            if (obj == null)
                return null;

            var buffer = _serializer.FormatObj(obj);
            
            return Format(buffer);
        }

        /// <summary>
        /// Zips the raw buffered data.
        /// </summary>
        /// <param name="buffer">raw uncompressed / unencrypted data</param>
        /// <returns>the encrypted and / or compressed data</returns>
        public Byte[] Format(Byte[] buffer)
        {
            using (var inStream = new MemoryStream(buffer, false))
            {
                using (var stream = Format(inStream))
                {
                    byte[] outBuffer = new byte[stream.Length];

                    stream.Position = 0;

                    stream.Read(outBuffer, 0, outBuffer.Length);

                    return outBuffer;
                }
            }
        }

        /// <summary>
        /// Zips the uncompressed stream data.
        /// </summary>
        /// <param name="inStream">encrypted and / or uncompressed data steam.</param>
        /// <returns>compressed and encrypted data stream.</returns>
        public Stream Format(Stream inStream)
        {
            var stream = new MemoryStream();
            
            _quickEncoder.SetCoderProperties(_properties.Keys.ToArray(), _properties.Values.ToArray());

            //write header
            _quickEncoder.WriteCoderProperties(stream);

            //write uncrompressed size
            long uncompressedSize = (long)inStream.Length;
            for (int i = 0; i < 8; i++)
            {
                var bit = (Byte)(uncompressedSize >> (8 * i));
                stream.WriteByte(bit);
            }

            inStream.Position = 0;
            _quickEncoder.Code(inStream, stream, -1, -1, null);

            stream.Flush();

            return stream;
        }

        public T UnformatObj<T>(Byte[] buffer)
        {
            byte[] raw = buffer;

            using (var inStream = new MemoryStream(raw, false))
            {
                using (var stream = Unzip(inStream))
                {
                    var bytes = new Byte[stream.Length];

                    stream.Position = 0;

                    stream.Read(bytes, 0, bytes.Length);

                    return _serializer.UnformatObj<T>(bytes);
                }
            }
        }

        public T UnformatObj<T>(Stream inStream)
        {
            using (var outStream = Unzip(inStream))
            {
                var stream = outStream;
                try
                {
                    return _serializer.UnformatObj<T>(stream);
                }
                finally { stream.Dispose(); }
            }
        }

        public byte[] Unformat(Byte[] buffer)
        {
            using (var inStream = new MemoryStream(buffer, false))
            {
                using (var stream = Unzip(inStream))
                {
                    var bytes = new Byte[stream.Length];

                    stream.Position = 0;

                    stream.Read(bytes, 0, bytes.Length);

                    return bytes;
                }
            }
        }

        public Stream Unformat(Stream inStream)
        {
            var stream = Unzip(inStream);

            return stream;
        }

        #region ISafeFormatter Members

        public bool TryFormatObj<T>(T obj, out byte[] buffer)
        {
            buffer = new byte[] { };

            try
            {
                buffer = FormatObj(obj);

                return true;
            }
            catch (JsonException) { return false; }
            catch (SystemException) { return false; }
        }

        public bool TryUnformatObj<T>(byte[] buffer, out T obj)
        {
            obj = default(T);

            try
            {
                obj = UnformatObj<T>(buffer);

                return true;
            }
            catch (JsonException) { return false; }
            catch (SystemException) { return false; }
        }

        public bool TryUnformatObj<T>(Stream stream, out T obj)
        {
            obj = default(T);

            try
            {
                obj = UnformatObj<T>(stream);

                return true;
            }
            catch (JsonException) { return false; }
            catch (SystemException) { return false; }
        }

        #endregion
    }
}
