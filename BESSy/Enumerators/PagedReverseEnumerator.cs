using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Files;
using System.Collections;

namespace BESSy.Enumerators
{
    public class PagedReverseEnumerator<ItemType> : IEnumerator<ItemType[]>, IEnumerable<ItemType[]>
    {
        public PagedReverseEnumerator(IPagedFile<ItemType> file)
        {
            _file = file;
            currentPage = file.Pages + 1;
        }

        object _syncRoot = new object();
        int currentPage = 0;
        IPagedFile<ItemType> _file;


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
                    var objs = _file.GetPage(currentPage);

                    Array.Reverse(objs);

                    return objs;
                }
            }
        }

        object IEnumerator.Current { get { return Current; } }

        public virtual bool MoveNext()
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

        public virtual void Reset()
        {
            lock (_syncRoot)
                currentPage = _file.Pages + 1;
        }

        public virtual void Dispose()
        {
            //don't dispose, just drop the reference.
            lock (_syncRoot)
                _file = null;
        }
    }
}
