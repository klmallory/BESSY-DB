/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.AccessControl;
using BESSy.Serialization;
using BESSy.Extensions;

namespace BESSy.Files
{
    public class XmlFileManager<T> : IFileRepository<T> where T : class
    {
        public XmlFileManager()
        {

        }

        public XmlFileManager(string workingPath)
        {
            WorkingPath = workingPath;
        }

        int _bufferSize = 1024;

        public string WorkingPath { get; set; }

        public Stream GetWritableFileStream(string fileNamePath)
        {
            return new FileStream(fileNamePath, FileMode.OpenOrCreate
            , FileSystemRights.Write | FileSystemRights.CreateFiles
            , FileShare.None, _bufferSize, FileOptions.SequentialScan);
        }

        public Stream GetReadableFileStream(string fileNamePath)
        {
            return new FileStream(fileNamePath, FileMode.Open
                , System.Security.AccessControl.FileSystemRights.Read
                , FileShare.Read, _bufferSize, FileOptions.SequentialScan);
        }

        public virtual T LoadFromFile(string fileName, string path)
        {
            var fullPath = Path.Combine(WorkingPath, path, fileName);

            return LoadFromFile(fullPath);
        }

        public virtual T LoadFromFile(string fileNamePath)
        {
            using (var stream = LoadAsStream(fileNamePath))
            {
                if (stream == null)
                    return default(T);

                using (var reader = new StreamReader(stream))
                {
                    var xml = reader.ReadToEnd();

                    if (xml.IsNullOrEmpty())
                    {
                        Trace.TraceWarning("File Was Empty: {0}", fileNamePath);
                        return null;
                    }

                    return XmlSerializationHelper.Deserialize<T>(xml);
                }
            }
        }

        public virtual Stream LoadAsStream(string fileName, string path)
        {
            var fullPath = Path.Combine(WorkingPath, path , fileName);

            return LoadAsStream(fullPath);
        }

        public virtual Stream LoadAsStream(string fileNamePath)
        {
            var fi = new FileInfo(fileNamePath);

            if (!fi.Exists)
            {
                Trace.TraceError("File Not Found: {0}", fileNamePath);
                return null;
            }
            //hmm, this isn't really an unhandle-able situation. Sometimes this is legite.
            //throw new FileNotFoundException(string.Format("File {0} not found", fullPath));

            return fi.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        public virtual void SaveToFile(T obj, string fileName, string path)
        {
            var fullPath = Path.Combine(WorkingPath, path, fileName);

            SaveToFile(obj, fullPath);
        }

        public virtual void SaveToFile(T obj, string fileNamePath)
        {
            var fi = new FileInfo(fileNamePath);

            using (var fs = fi.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
            {
                if (!fs.CanWrite)
                    throw new FileLoadException(string.Format("Can not write to file {0}", fi.FullName));

                OverwriteStream(obj, fs);
            }
        }

        public virtual void OverwriteStream(T obj, Stream stream)
        {
            var data = XmlSerializationHelper.Serialize(obj, Encoding.UTF8);

            stream.SetLength(Encoding.UTF8.GetByteCount(data));
            stream.Position = 0;

            using (var sw = new StreamWriter(stream))
            {
                sw.Write(data);
                sw.Flush();
                sw.Close();
            }
        }

        public void OverwriteStream(byte[] data, Stream stream)
        {
            stream.SetLength(data.Length);
            stream.Position = 0;

            using (var sw = new StreamWriter(stream))
            {
                sw.Write(data);
                sw.Flush();
                sw.Close();
            }
        }

        public void SaveToFile(byte[] data, string fileName, string path)
        {
            var fullPath = Path.Combine(WorkingPath, path, fileName);

            SaveToFile(data, fullPath);
        }

        public void SaveToFile(byte[] data, string fileNamePath)
        {
            var fi = new FileInfo(fileNamePath);

            using (var fs = fi.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
            {
                if (!fs.CanWrite)
                    throw new FileLoadException(string.Format("Can not write to file {0}", fi.FullName));

                OverwriteStream(data, fs);
            }
        }

        public virtual void Dispose()
        {
            
        }
    }
}
