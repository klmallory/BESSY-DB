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
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Security.AccessControl;

namespace BESSy.Extensions
{
    public static class FileExtensions
    {
        public static bool IsFileLocked(this FileInfo fileInfo)
        {
            FileStream stream = null;

            try
            {
                stream = fileInfo.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (FileNotFoundException) { return false; }
            catch (IOException) { return true; }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            return false;
        }

        public static bool TryOpenExistingMutex(string mutexName, out Mutex mutex)
        {
            mutex = null;

            try
            {
                mutex = Mutex.OpenExisting(mutexName, MutexRights.Modify | MutexRights.ReadPermissions | MutexRights.Synchronize);

                return mutex != null;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
