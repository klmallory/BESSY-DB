using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace BESSy.Files
{
    public static class FileManager
    {
        static string _error = "File name {0}, could not be found or accessed: {1}.";

        public static void Create(string fileNamePath)
        {
            if (File.Exists(fileNamePath))
                return;

            try
            {
                using (var file = new FileStream(fileNamePath, FileMode.Create, System.Security.AccessControl.FileSystemRights.CreateFiles, FileShare.None, 2048, FileOptions.None))
                {
                    file.Close();
                }
            }
            catch (UnauthorizedAccessException uaaEx) { Trace.TraceError(String.Format(_error, fileNamePath, uaaEx)); }
            catch (ArgumentNullException agnEx) { Trace.TraceError(String.Format(_error, fileNamePath, agnEx)); }
            catch (DriveNotFoundException rnfEx) { Trace.TraceError(String.Format(_error, fileNamePath, rnfEx)); }
            catch (DirectoryNotFoundException dnfEx) { Trace.TraceError(String.Format(_error, fileNamePath, dnfEx)); }
            catch (FileNotFoundException fnfEx) { Trace.TraceError(String.Format(_error, fileNamePath, fnfEx)); }
            catch (ArgumentException argEx) { Trace.TraceError(String.Format(_error, fileNamePath, argEx)); }
            catch (PathTooLongException ptlEx) { Trace.TraceError(String.Format(_error, fileNamePath, ptlEx)); }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_error, fileNamePath, ioEx)); }
        }

        public static void Delete(string fileNamePath)
        {
            try
            {
                if (File.Exists(fileNamePath))
                    File.Delete(fileNamePath);
            }
            catch (UnauthorizedAccessException uaaEx) { Trace.TraceError(String.Format(_error, fileNamePath, uaaEx)); }
            catch (ArgumentNullException agnEx) { Trace.TraceError(String.Format(_error, fileNamePath, agnEx)); }
            catch (DriveNotFoundException rnfEx) { Trace.TraceError(String.Format(_error, fileNamePath, rnfEx)); }
            catch (DirectoryNotFoundException dnfEx) { Trace.TraceError(String.Format(_error, fileNamePath, dnfEx)); }
            catch (FileNotFoundException fnfEx) { Trace.TraceError(String.Format(_error, fileNamePath, fnfEx)); }
            catch (ArgumentException argEx) { Trace.TraceError(String.Format(_error, fileNamePath, argEx)); }
            catch (PathTooLongException ptlEx) { Trace.TraceError(String.Format(_error, fileNamePath, ptlEx)); }
            catch (IOException ioEx) { Trace.TraceError(String.Format(_error, fileNamePath, ioEx)); }
        }

        public static void Delete(string fileName, string path)
        {
            var filePath = Path.Combine(path, fileName);

            Delete(filePath);
        }
    }
}
