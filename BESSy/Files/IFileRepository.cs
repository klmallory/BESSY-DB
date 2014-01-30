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
using BESSy.Json.Linq;

namespace BESSy.Files
{
    public interface IQueryableFile
    {
        int Pages { get; }
        JObject[] GetPage(int page);
        IEnumerable<JObject[]> AsEnumerable();
        IEnumerable<JObject[]> AsReverseEnumerable();
    }

    public interface IFileRepository<EntityType> : IFileManager, IFileWriter<EntityType>, IFileReader<EntityType>, IBinWriter, IDisposable
    {
    }

    public interface IBinWriter
    {
        void SaveToFile(byte[] data, string fileNamePath);
        void SaveToFile(byte[] data, string fileName, string path);
        void OverwriteStream(byte[] data, Stream stream);
    }

    public interface IFileManager : IDisposable
    {
        string WorkingPath { get; set; }
        Stream GetWritableFileStream(string fileNamePath);
        Stream GetReadableFileStream(string fileNamePath);
    }

    public interface IFileWriter<T>
    {
        void SaveToFile(T obj, string fileNamePath);
        void SaveToFile(T obj, string fileName, string path);
        void OverwriteStream(T obj, Stream stream);
    }

    public interface IFileReader<T>
    {
        T LoadFromFile(string fileNamePath);
        T LoadFromFile(string fileName, string path);
        Stream LoadAsStream(string fileName, string path);
    }
}
