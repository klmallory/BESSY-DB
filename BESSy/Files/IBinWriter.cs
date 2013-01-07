using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace BESSy.Files
{
    public interface IBinWriter
    {
        void SaveToFile(byte[] data, string fileNamePath);
        void SaveToFile(byte[] data, string fileName, string path);
        void OverwriteStream(byte[] data, Stream stream);
    }
}
