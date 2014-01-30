/*
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

namespace BESSy.Extensions
{
    public static class StreamExtensions
    {
        public static void Trim(this Stream inStream)
        {
            var buffer = new byte[Environment.SystemPageSize];

            var lastNonZeroIndex = -1;
            var total = 0;

            var read = inStream.Read(buffer, 0, buffer.Length);

            while (read > 0)
            {
                var last = Array.FindLastIndex(buffer, b => b != 0);
                if (last >= 0)
                    lastNonZeroIndex = total + last;

                total += read;

                read = inStream.Read(buffer, 0, buffer.Length);
            }


            if (lastNonZeroIndex >= 0)
                inStream.SetLength(lastNonZeroIndex + 1);
        }

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

        public static void WriteAllTo(this Stream inStream, Stream outStream, int minLength)
        {
            var buffer = new byte[Environment.SystemPageSize];

            inStream.SetLength(minLength);

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
            bool isEmpty = true;
            int total = 0;
            var buffer = new byte[bufferSize];

            var read = inStream.Read(buffer, 0, buffer.Length);

            while (read > 0)
            {
                isEmpty &= Array.TrueForAll(buffer, a => a == 0);
                outStream.Write(buffer, 0, read);
                total += read;

                Array.Resize(ref buffer, oldStride - total > bufferSize ? bufferSize : oldStride - total);
                read = inStream.Read(buffer, 0, buffer.Length);
            }

            outStream.Position = outStream.Position + (newStride - oldStride);

            return !isEmpty;
        }

        public static bool WriteSegmentToWithTrim(this Stream inStream, Stream outStream, int bufferSize, int newStride, int oldStride, out int lastNonZeroIndex)
        {
            lastNonZeroIndex = -1;
            bool isEmpty = true;
            int total = 0;
            var buffer = new byte[bufferSize];

            var read = inStream.Read(buffer, 0, buffer.Length);

            while (read > 0)
            {
                isEmpty &= Array.TrueForAll(buffer, a => a == 0);
                var last = Array.FindLastIndex(buffer, b => b != 0);
                if (last >= 0)
                    lastNonZeroIndex = total + last;

                outStream.Write(buffer, 0, read);
                total += read;

                Array.Resize(ref buffer, oldStride - total > bufferSize ? bufferSize : oldStride - total);
                read = inStream.Read(buffer, 0, buffer.Length);
            }

            outStream.Position = outStream.Position + (newStride - oldStride);

            return !isEmpty;
        }
    }
}
