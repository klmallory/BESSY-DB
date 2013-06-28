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
