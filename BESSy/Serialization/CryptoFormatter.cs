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
using System.Text;
using BESSy.Crypto;
using Newtonsoft.Json;
using SevenZip;
using SevenZip.LZMA;
using SECP = System.Security.Permissions;

namespace BESSy.Serialization
{
    [SecurityCritical()]
    [SECP.KeyContainerPermission(SECP.SecurityAction.Demand)]
    [SECP.ReflectionPermission(SECP.SecurityAction.Demand)]
    [SECP.EnvironmentPermission(SECP.SecurityAction.Demand)]
    public class CryptoFormatter : ISafeFormatter
    {
        public CryptoFormatter(ICrypto cryptoProvider, IFormatter serializer, object[] hash)
        {
            _crypto = cryptoProvider;
            _serializer = serializer;
            _hash = hash;
        }

        ICrypto _crypto;
        IFormatter _serializer;
        object[] _hash;

        #region ISafeFormatter Members

        public bool TryFormatObj<T>(T obj, out Stream outStream)
        {
            try
            {
                outStream = FormatObjStream(obj);

                return true;
            }
            catch (SystemException) { }

            outStream = null;
            return false;
        }

        public bool TryFormatObj<T>(T obj, out byte[] buffer)
        {
            try
            {
                buffer = FormatObj(obj);

                return true;
            }
            catch (SystemException) { }

            buffer = new byte[0];
            return false;
        }

        public bool TryUnformatObj<T>(byte[] buffer, out T obj)
        {
            try
            {
                obj = UnformatObj<T>(buffer);

                return true;
            }
            catch (SystemException) { }

            obj = default(T);
            return false;
        }

        public bool TryUnformatObj<T>(Stream stream, out T obj)
        {
            try
            {
                obj = UnformatObj<T>(stream);

                return true;
            }
            catch (SystemException) { }

            obj = default(T);
            return false;
        }

        #endregion

        #region IFormatter Members

        public Stream FormatObjStream<T>(T obj)
        {
            using (var stream = _serializer.FormatObjStream(obj))
            {
                stream.Position = 0;
                var outStream = _crypto.Encrypt(stream, _crypto.GetKey(_hash, _crypto.KeySize));

                outStream.Position = 0;
                return outStream;
            }
        }

        public byte[] FormatObj<T>(T obj)
        {
            var buffer = _serializer.FormatObj(obj);

            buffer = _crypto.Encrypt(buffer, _crypto.GetKey(_hash, _crypto.KeySize));

            return buffer;
        }

        public T UnformatObj<T>(byte[] buffer)
        {
            buffer = _crypto.Decrypt(buffer, _crypto.GetKey(_hash, _crypto.KeySize));

            return _serializer.UnformatObj<T>(buffer);
        }

        public T UnformatObj<T>(System.IO.Stream inStream)
        {
            inStream.Position = 0;

            var stream = _crypto.Decrypt(inStream, _crypto.GetKey(_hash, _crypto.KeySize));

            return _serializer.UnformatObj<T>(stream);
        }

        #endregion

        #region IBinFormatter Members

        public byte[] Format(byte[] buffer)
        {
            return _crypto.Encrypt(buffer, _crypto.GetKey(_hash, _crypto.KeySize));
        }

        public Stream Format(Stream inStream)
        {
            inStream.Position = 0;

            return _crypto.Encrypt(inStream, _crypto.GetKey(_hash, _crypto.KeySize));
        }

        public byte[] Unformat(byte[] buffer)
        {
            return _crypto.Decrypt(buffer, _crypto.GetKey(_hash, _crypto.KeySize));
        }

        public Stream Unformat(Stream inStream)
        {
            inStream.Position = 0;

            return _crypto.Decrypt(inStream, _crypto.GetKey(_hash, _crypto.KeySize));
        }

        #endregion

    }
}
