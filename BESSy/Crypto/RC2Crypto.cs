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
using System.Security;
using System.Security.Cryptography;
using System.Text;
using BESSy.Extensions;
using SECP = System.Security.Permissions;
using System.Runtime;
using System.Runtime.InteropServices;

namespace BESSy.Crypto
{
    /// <summary>
    /// Provides a simple RC2 encryption algorithm.
    /// </summary>
    [SecurityCritical()]
    [SECP.KeyContainerPermission(SECP.SecurityAction.Demand)]
    [SECP.ReflectionPermission(SECP.SecurityAction.Demand)]
    [SECP.EnvironmentPermission(SECP.SecurityAction.Demand)]
    public class RC2Crypto : ICrypto
    {
        byte[] _simpleVector = new byte[8] { 124, 53, 89, 243, 163, 62, 47, 191 };
        Encoding _encoding;
       
        /// <summary>
        /// What's your Vector, Victor?
        /// </summary>
        /// <param name="vector"></param>
        [TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
        public RC2Crypto(SecureString vector)
            : this(vector, Encoding.ASCII)
        {

        }
        
        /// <summary>
        /// What's your Vector, Victor?
        /// </summary>
        /// <param name="vector"></param>
        /// <param name="encoding"></param>
        [TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
        public RC2Crypto(SecureString vector, Encoding encoding)
        {
            _simpleVector = GetKey(vector, KeySize);
            _encoding = encoding;
        }

        #region ICrypto Members

        /// <summary>
        /// The Length of the key
        /// </summary>
        public int KeySize
        {
            get { return 8; }
        }

        /// <summary>
        /// Gets the key to be used from the collection of key objects passed in.
        /// </summary>
        /// <param name="key">the secure string to derrive the key from.</param>
        /// <param name="keySize">the _length of the key.</param>
        /// <returns>the key</returns>
        [TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
        public byte[] GetKey(SecureString key, int keySize)
        {
#if DEBUG
            if (key == null || key.Length == 0)
                throw new ArgumentNullException("key", "key cannot be null");
#endif
            byte[] buf = new byte[keySize];

            IntPtr bstr = IntPtr.Zero;

            try
            {
                bstr = Marshal.SecureStringToBSTR(key);
                for (int i = 0; i < keySize; i++)
                    buf[i] = Marshal.ReadByte(bstr, (i * 7) % key.Length);
            }
            finally
            { Marshal.ZeroFreeBSTR(bstr); }

            return buf;
        }

        /// <summary>
        /// Encrypts the specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        [TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
        public string Encrypt(string value, byte[] key)
        {
            byte[] raw = _encoding.GetBytes(value);

            byte[] buffer = Encrypt(raw, key);

            return Convert.ToBase64String(buffer);
        }

        /// <summary>
        /// Encrypts the specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        [TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
        public byte[] Encrypt(byte[] value, byte[] key)
        {
            RC2 rc2 = RC2.Create();

            ICryptoTransform transform = rc2.CreateEncryptor(key, _simpleVector);

            rc2.Padding = PaddingMode.PKCS7;

            var encrypted = transform.TransformFinalBlock(value, 0, value.Length);

            return encrypted;
        }

        /// <summary>
        /// Encrypts the specified value.
        /// </summary>
        /// <param name="inStream"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        [TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
        public MemoryStream Encrypt(Stream inStream, byte[] key)
        {
            if (inStream.Length < 1)
                return new MemoryStream();

            RC2 rc2 = RC2.Create();
            ICryptoTransform transform = rc2.CreateEncryptor(key, _simpleVector);
            rc2.Padding = PaddingMode.PKCS7;

            var encryptedStream = new MemoryStream();
            byte[] buffer = new byte[Environment.SystemPageSize];

            var cryptoStream = new CryptoStream(encryptedStream, transform, CryptoStreamMode.Write);
            var read = inStream.Read(buffer, 0, buffer.Length);

            while (read > 0)
            {
                Array.Resize(ref buffer, read);
                cryptoStream.Write(buffer, 0, buffer.Length);

                read = inStream.Read(buffer, 0, buffer.Length);
            }

            cryptoStream.FlushFinalBlock();

            encryptedStream.Position = 0;

            return encryptedStream;
        }

        /// <summary>
        /// Decrypts the specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        [TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
        public string Decrypt(string value, byte[] key)
        {
            byte[] raw = Convert.FromBase64String(value);

            byte[] buffer = Decrypt(raw, key);

            return _encoding.GetString(buffer);
        }

        /// <summary>
        /// Decrypts the specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        [TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
        public byte[] Decrypt(byte[] value, byte[] key)
        {
            var decrypted = new byte[0];
            byte[] buffer = new byte[Environment.SystemPageSize];

            using (var inStream = new MemoryStream(value))
            {
                RC2 rc2 = RC2.Create();
                ICryptoTransform transform = rc2.CreateDecryptor(key, _simpleVector);
                rc2.Padding = PaddingMode.PKCS7;

                var sr = new StreamReader(inStream);

                using (var cryptoStream = new CryptoStream(inStream, transform, CryptoStreamMode.Read))
                {
                    inStream.Position = 0;
                    var read = cryptoStream.Read(buffer, 0, buffer.Length);

                    while (read > 0)
                    {
                        Array.Resize(ref buffer, read);
                        Array.Resize(ref decrypted, decrypted.Length + buffer.Length);
                        Array.Copy(buffer, 0, decrypted, decrypted.Length - buffer.Length, buffer.Length);

                        read = cryptoStream.Read(buffer, 0, buffer.Length);
                    }
                }
            }

            return decrypted;
        }

        /// <summary>
        /// Decrypts the specified <typeparamref name="System.IO.Stream"/>
        /// </summary>
        /// <param name="inStream"><typeparamref name="System.IO.Stream"/> to decrypt</param>
        /// <param name="key">the hash key</param>
        /// <returns>the decrypted <typeparamref name="System.IO.Stream"/></returns>
        [TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
        public MemoryStream Decrypt(Stream inStream, byte[] key)
        {
            RC2 rc2 = RC2.Create();
            ICryptoTransform transform = rc2.CreateDecryptor(key, _simpleVector);
            rc2.Padding = PaddingMode.PKCS7;

            var outStream = new MemoryStream();

            using (var cryptoStream = new CryptoStream(inStream, transform, CryptoStreamMode.Read))
            {
                
                byte[] buffer = new byte[Environment.SystemPageSize];

                var read = cryptoStream.Read(buffer, 0, buffer.Length);

                while (read > 0)
                {
                    outStream.Write(buffer, 0, read);

                    read = cryptoStream.Read(buffer, 0, buffer.Length);
                }
            }

            outStream.Flush();
            outStream.Position = 0;

            return outStream;
        }

        #endregion
    }
}
