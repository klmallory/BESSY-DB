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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BESSy.Cache;
using BESSy.Extensions;
using BESSy.Files;
using BESSy.Json;
using BESSy.Json.Linq;
using BESSy.Parallelization;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using BESSy.Transactions;

namespace BESSy.Enumerators
{
    public class PagedStreamingEnumerator : IEnumerator<Stream>, IEnumerable<Stream>
    {
        public PagedStreamingEnumerator(IStreamedFile file)
        {
            _file = file;
        }

        object _syncRoot = new object();

        int currentPage = -1;
        IStreamedFile _file;

        public int CurrentPage { get { return currentPage; } set { currentPage = value; } }

        public IEnumerator<Stream> GetEnumerator()
        {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this;
        }

        public Stream Current
        {
            get 
            {
                lock (_syncRoot)
                {
                    return _file.GetPageStream(currentPage);
                }
            }
        }

        object IEnumerator.Current { get { return Current; } }

        public bool MoveNext()
        {
            lock (_syncRoot)
            {
                currentPage++;

                if (currentPage >= _file.Pages)
                {
                    currentPage = -1;
                    return false;
                }

                return true;
            }
        }

        public void Reset()
        {
            lock (_syncRoot)
                currentPage = -1;
        }

        public void Dispose()
        {
            lock (_syncRoot)
            _file = null;
        }
    }
}
