using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy
{
    public delegate void AsyncFetch<T>(T item);
    public delegate void AsyncAdd<I>(I index);

    public interface IAsyncRepository<T, I>
    {
        AsyncAdd<I> BeginAdd(T item);
        void BeginFlush(IList<T> dataSource);
        void BeginAddOrUpdate(T item);
        void BeginUpdate(T item);
    }

    public interface IReadOnlyAsyncRepository<T, I>
    {
        bool Stale { get; }
        void BeginFetch(I id);
    }
}
