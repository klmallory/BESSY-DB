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
using Newtonsoft.Json.Linq;
using BESSy.Synchronization;

namespace BESSy.Files
{
    public class QueryEnumerator : IEnumerator<JObject[]>, IEnumerable<JObject[]>
    {
        public QueryEnumerator(IQueryableFile file)
        {
            _file = file;
        }

        object _syncRoot = new object();

        int currentPage = -1;
        IQueryableFile _file;

        public IEnumerator<JObject[]> GetEnumerator()
        {
            return this;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this;
        }

        public JObject[] Current
        {
            get 
            {
                lock (_syncRoot)
                {
                    return _file.GetPage(currentPage);
                }
            }
        }

        object System.Collections.IEnumerator.Current { get { return Current; } }

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
