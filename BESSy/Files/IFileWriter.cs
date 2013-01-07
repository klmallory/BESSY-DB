/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace BESSy.Files
{
    public interface IFileWriter<T>
    {
        void SaveToFile(T obj, string fileNamePath);
        void SaveToFile(T obj, string fileName, string path);
        void OverwriteStream(T obj, Stream stream);
    }
}
