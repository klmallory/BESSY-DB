﻿/*
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
using System.Text;
using BESSy.Crypto;
using BESSy.Json;
using BESSy.Json.Linq;

namespace BESSy.Serialization
{
    [SecuritySafeCritical]
    public class QueryCryptoFormatter : CryptoFormatter, IQueryableFormatter
    {
        public QueryCryptoFormatter(ICrypto cryptoProvider, IQueryableFormatter serializer, SecureString hash)
            : base(cryptoProvider, serializer, hash)
        {
            _serializer = serializer;
        }

        protected IQueryableFormatter _serializer;

        public JsonSerializer Serializer { get { return _serializer.Serializer; } }

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

        public JObject AsQueryableObj<T>(T obj)
        {
            if (obj != null)
                return JObject.FromObject(obj, Serializer);
            else
                return new JObject();
        }

        public JObject Parse(Stream inStream)
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
                return true;
            }
            catch (Exception) { stream = null; return false; }
        }
    }
}
