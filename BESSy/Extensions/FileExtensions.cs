using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

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
    }
}
