/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace BESSy.Files
{
    public interface IFileManager : IDisposable
    {
        string WorkingPath { get; set; }
        Stream GetWritableFileStream(string fileNamePath);
        Stream GetReadableFileStream(string fileNamePath);
    }
}