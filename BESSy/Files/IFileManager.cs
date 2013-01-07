/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.IO;

namespace BESSy.Files
{
    public interface IFileManager : IDisposable
    {
        string WorkingPath { get; set; }
        FileStream GetWritableFileStream(string fileNamePath);
        FileStream GetReadableFileStream(string fileNamePath);
    }
}