/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace BESSy.Crypto
{
    public interface ICrypto
    {
        /// <summary>
        /// Gets the size of the key for this crypto.
        /// </summary>
        /// <value>The size of the key.</value>
        int KeySize { get; }

        /// <summary>
        /// Return the key from the hash.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="keySize"></param>
        /// <returns></returns>
        byte[] GetKey(object[] hash, int keySize);

        /// <summary>
        /// Encrypts the specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="key">The key.</param>
        /// <param name="encoding">The encoding.</param>
        /// <returns></returns>
        string Encrypt(string value, byte[] key);

        /// <summary>
        /// Encrypts the specified byte array.
        /// </summary>
        /// <param name="value">The byte array to encrypt.</param>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        byte[] Encrypt(byte[] value, byte[] key);

        /// <summary>
        /// Encrypts the specified stream.
        /// </summary>
        /// <param name="stream"><typeparamref name="System.IO.Stream"/> to encrypt.</param>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        MemoryStream Encrypt(Stream stream, byte[] key);

        /// <summary>
        /// Decrypts the specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        string Decrypt(string value, byte[] key);

        /// <summary>
        /// Decrypts the specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        byte[] Decrypt(byte[] value, byte[] key);

        /// <summary>
        /// Decrypts the specified <typeparamref name="System.IO.Stream"/>
        /// </summary>
        /// <param name="inStream"><typeparamref name="System.IO.Stream"/> to decrypt</param>
        /// <param name="key">the hash key</param>
        /// <returns>the decrypted <typeparamref name="System.IO.Stream"/></returns>
        MemoryStream Decrypt(Stream inStream, byte[] key);
    }
}
