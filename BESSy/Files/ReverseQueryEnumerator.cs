using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Json.Linq;

namespace BESSy.Files
{
    public class ReverseQueryEnumerator : IEnumerator<JObject[]>, IEnumerable<JObject[]>
    {
        public ReverseQueryEnumerator(IQueryableFile file)
        {
            _file = file;
            currentPage = file.Pages + 1;
        }

        object _syncRoot = new object();
        int currentPage = 0;
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
                    var objs = _file.GetPage(currentPage);

                    Array.Reverse(objs);

                    return objs;
                }
            }
        }

        object System.Collections.IEnumerator.Current { get { return Current; } }

        public bool MoveNext()
        {
            lock (_syncRoot)
            {
                currentPage--;

                if (currentPage < 0)
                {
                    currentPage = _file.Pages + 1;
                    return false;
                }

                return true;
            }
        }

        public void Reset()
        {
            lock (_syncRoot)
                currentPage = _file.Pages + 1;
        }

        public void Dispose()
        {
            //don't dispose, just drop the reference.
            lock (_syncRoot)
                _file = null;
        }
    }
}
