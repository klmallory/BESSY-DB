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
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security;
using System.Text;
using BESSy.Crypto;
using BESSy.Json;
using SevenZip;
using SevenZip.LZMA;
using SECP = System.Security.Permissions;
using System.Runtime.InteropServices;

namespace BESSy.Serialization
{
    [SecurityCritical()]
    [SECP.KeyContainerPermission(SECP.SecurityAction.Demand)]
    [SECP.ReflectionPermission(SECP.SecurityAction.Demand)]
    [SECP.EnvironmentPermission(SECP.SecurityAction.Demand)]
    public class CryptoFormatter : ISafeFormatter
    {
        public CryptoFormatter(ICrypto cryptoProvider, IFormatter serializer, SecureString hash)
        {
            _crypto = cryptoProvider;
            _serializer = serializer;
            _key = _crypto.GetKey(hash, _crypto.KeySize);
        }

        ICrypto _crypto;
        IFormatter _serializer;
        byte[] _key;

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

        public bool Trim { get { return true; } }

        public Stream FormatObjStream<T>(T obj)
        {
            using (var stream = _serializer.FormatObjStream(obj))
            {
                stream.Position = 0;
                var outStream = _crypto.Encrypt(stream, _key);

                outStream.Position = 0;
                return outStream;
            }
        }

        public byte[] FormatObj<T>(T obj)
        {
            var buffer = _serializer.FormatObj(obj);

            buffer = _crypto.Encrypt(buffer, _key);

            return buffer;
        }

        public T UnformatObj<T>(byte[] buffer)
        {
            buffer = _crypto.Decrypt(buffer, _key);

            return _serializer.UnformatObj<T>(buffer);
        }

        public T UnformatObj<T>(System.IO.Stream inStream)
        {
            inStream.Position = 0;

            var stream = _crypto.Decrypt(inStream, _key);

            return _serializer.UnformatObj<T>(stream);
        }

        #endregion

        #region IBinFormatter Members

        public byte[] Format(byte[] buffer)
        {
            return _crypto.Encrypt(buffer, _key);
        }

        public Stream Format(Stream inStream)
        {
            inStream.Position = 0;

            return _crypto.Encrypt(inStream, _key);
        }

        public byte[] Unformat(byte[] buffer)
        {
            return _crypto.Decrypt(buffer, _key);
        }

        public Stream Unformat(Stream inStream)
        {
            inStream.Position = 0;

            return _crypto.Decrypt(inStream, _key);
        }

        #endregion

    }
}
