/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
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

namespace BESSy.Crypto
{
    /// <summary>
    /// Provides a simple RC2 encryption algorithm.
    /// </summary>
    [SecurityCritical()]
    [SECP.KeyContainerPermission(SECP.SecurityAction.Demand)]
    public class RC2Crypto : ICrypto
    {
        byte[] simpleVector = new byte[8] { 124, 53, 89, 243, 163, 62, 47, 191 };

        public RC2Crypto()
        {
        }

        public RC2Crypto(byte[] vector)
        {
            simpleVector = vector;
        }

        #region ICypressCrypto Members

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
        /// <param name="key">the object array to derrive the key from.</param>
        /// <param name="keySize">the length of the key.</param>
        /// <returns>the key</returns>
        public byte[] GetKey(object[] key, int keySize)
        {
#if DEBUG
            if (key.IsNullOrEmpty())
                throw new ArgumentNullException("key", "key cannot be null");
#endif
            Random rnd = new Random(key.GetHashCode());

            byte[] buf = new byte[keySize];

            for (int i = 0; i < keySize; i++)
                buf[i] = Convert.ToByte((key[i % key.Length].GetHashCode() * i) % 256);
            
            return buf;
        }

        /// <summary>
        /// Encrypts the specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="key">The key.</param>
        /// <param name="encoding">The encoding.</param>
        /// <returns></returns>
        public string Encrypt(string value, byte[] key, Encoding encoding)
        {
            byte[] raw = encoding.GetBytes(value);

            byte[] buffer = (this as ICrypto).Encrypt(raw, key);

            return Convert.ToBase64String(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Encrypts the specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public byte[] Encrypt(byte[] value, byte[] key)
        {
            byte[] retVal = new byte[0];

            RC2 rc2 = RC2.Create();

            ICryptoTransform transform = rc2.CreateEncryptor(key, simpleVector);

            rc2.Padding = PaddingMode.PKCS7;

            retVal = transform.TransformFinalBlock(value, 0, value.Length);

            return retVal;
        }

        /// <summary>
        /// Decrypts the specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="key">The key.</param>
        /// <param name="encoding">The encoding.</param>
        /// <returns></returns>
        public string Decrypt(string value, byte[] key, Encoding encoding)
        {
            byte[] raw = Convert.FromBase64String(value);

            byte[] buffer = (this as ICrypto).Decrypt(raw, key);

            return encoding.GetString(buffer);
        }

        /// <summary>
        /// Decrypts the specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public byte[] Decrypt(byte[] value, byte[] key)
        {
            RC2 rc2 = RC2.Create();

            ICryptoTransform transform = rc2.CreateDecryptor(key, simpleVector);

            rc2.Padding = PaddingMode.PKCS7;

            var retVal = transform.TransformFinalBlock(value, 0, value.Length);

            return retVal;
        }

        /// <summary>
        /// Decrypts the specified <typeparamref name="System.IO.Stream"/>
        /// </summary>
        /// <param name="inStream"><typeparamref name="System.IO.Stream"/> to decrypt</param>
        /// <param name="key">the hash key</param>
        /// <returns>the decrypted <typeparamref name="System.IO.Stream"/></returns>
        public Stream Decrypt(Stream inStream, byte[] key)
        {
            RC2 rc2 = RC2.Create();

            ICryptoTransform transform = rc2.CreateDecryptor(key, simpleVector);

            rc2.Padding = PaddingMode.PKCS7;

            var buffer = new byte[inStream.Length];
            inStream.Position = 0;
            inStream.Read(buffer, 0, buffer.Length);

            var raw = transform.TransformFinalBlock(buffer, 0, buffer.Length);

            var outStream = new MemoryStream(raw);

            return outStream;
        }

        #endregion
    }
}
