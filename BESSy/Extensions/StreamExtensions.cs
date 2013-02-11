using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace BESSy.Extensions
{
    public static class StreamExtensions
    {
        public static void WriteAllTo(this Stream inStream, Stream outStream)
        {
            var buffer = new byte[Environment.SystemPageSize];

            inStream.Position = 0;
            var read = inStream.Read(buffer, 0, buffer.Length);

            while (read > 0)
            {
                outStream.Write(buffer, 0, read);
                read = inStream.Read(buffer, 0, buffer.Length);
            }
        }

        public static bool WriteSegmentTo(this Stream inStream, Stream outStream, int bufferSize, int newStride, int oldStride)
        {
            bool isEmpty = false;
            int total = 0;
            var buffer = new byte[bufferSize];

            var read = inStream.Read(buffer, 0, buffer.Length);

            while (read > 0)
            {
                isEmpty |= Array.TrueForAll(buffer, a => a == 0);
                outStream.Write(buffer, 0, read);
                total += read;

                Array.Resize(ref buffer, total - oldStride > bufferSize ? bufferSize : total - oldStride);
                read = inStream.Read(buffer, 0, buffer.Length);
            }

            outStream.Position = outStream.Position + (newStride - oldStride);

            return !isEmpty;
        }
    }
}
