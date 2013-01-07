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
    public interface IFileReader<T>
    {
        T LoadFromFile(string fileNamePath);
        T LoadFromFile(string fileName, string path);
        Stream LoadAsStream(string fileName, string path);
    }
}
