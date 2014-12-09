using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Files;
using System.Collections;

namespace BESSy.Enumerators
{
    public class PagedEnumerator<ItemType> : IEnumerator<ItemType[]>, IEnumerable<ItemType[]>
    {
        public PagedEnumerator(IPagedFile<ItemType> file)
        {
            _file = file;
        }

        object _syncRoot = new object();

        int currentPage = -1;
        IPagedFile<ItemType> _file;

        object IEnumerator.Current { get { return Current; } }

        public int CurrentPage { get { return currentPage; } set { currentPage = value; } }

        public virtual IEnumerator<ItemType[]> GetEnumerator()
        {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this;
        }

        public virtual ItemType[] Current
        {
            get
            {
                lock (_syncRoot)
                {
                    return _file.GetPage(currentPage);
                }
            }
        }

        public virtual bool MoveNext()
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

        public virtual void Reset()
        {
            lock (_syncRoot)
                currentPage = -1;
        }

        public virtual void Dispose()
        {
            lock (_syncRoot)
                _file = null;
        }
    }
}
