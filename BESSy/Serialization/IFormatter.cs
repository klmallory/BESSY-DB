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
using System.IO;
using System.Collections.Generic;
using BESSy.Json.Linq;
using BESSy.Json;

namespace BESSy.Serialization
{
    public interface IQueryableFormatter : ISafeFormatter
    {
        JsonSerializerSettings Settings { get; set; }
        JsonSerializer Serializer { get; }
        JObject AsQueryableObj<T>(T obj);
        JObject Parse(Stream inStream);
        bool TryParse(Stream inStream, out JObject obj);
        Stream Unparse(JObject token);
        bool TryUnparse(JObject token, out Stream stream);
    }

    public interface ILinqFormatter : IFormatter
    {
        IEnumerable<JArray> AsQueryable();
    }

    public interface ISafeFormatter : IFormatter
    {
        bool TryFormatObj<T>(T obj, out byte[] buffer);
        bool TryFormatObj<T>(T obj, out Stream outStream);
        bool TryUnformatObj<T>(byte[] buffer, out T obj);
        bool TryUnformatObj<T>(Stream stream, out T obj);
    }

    public interface IFormatter : IBinFormatter
    {
        byte[] FormatObj<T>(T obj);
        Stream FormatObjStream<T>(T obj);
        T UnformatObj<T>(byte[] buffer);
        T UnformatObj<T>(Stream inStream);
        bool Trim { get; }
        int TrimTerms { get; }
    }

    public interface IBinFormatter
    {
        byte[] Format(byte[] buffer);
        Stream Format(Stream inStream);
        byte[] Unformat(byte[] buffer);
        Stream Unformat(Stream inStream);
    }
}
