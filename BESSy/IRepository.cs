/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy
{
    public enum Action
    {
        Create,
        Update,
        Delete,
    }

    public interface ILinqRepository<T, I> : IReadOnlyRepository<T, I>
    {
        IEnumerable<T> Take(int count);
        IEnumerable<T> Skip(int count);
        IEnumerable<T> Where(Func<T, bool> query);
        T First(Func<T, bool> query);
        T FirstOrDefault(Func<T, bool> query);
        T Last(Func<T, bool> query);
        T LastOrDefault(Func<T, bool> query);
        IEnumerable<TResult> OfType<TResult>();
        IQueryable<T> AsQueryable();
    }

    /// <summary>
    /// Contract for a repository that supports saving data directly to it's source on demand.
    /// </summary>
    /// <typeparam name="EntityType">Entity Type</typeparam>
    public interface IFlush<EntityType>
    {
        void Flush(IList<EntityType> data);
    }

    /// <summary>
    /// Contract for a repository that supports a write through to it's source on demand.
    /// </summary>
    public interface IFlush
    {
        void Flush();
    }

    /// <summary>
    /// Repository Interface
    /// </summary>
    /// <typeparam name="T">Stored Type</typeparam>
    /// <typeparam name="I">Key Type</typeparam>
    public interface IRepository<T, I> : IReadOnlyRepository<T, I>
    {
        I Add(T item);
        void AddOrUpdate(T item, I id);
        void Update(T item, I id);
        void Delete(I id);
    }

    /// <summary>
    /// Read Only Repository
    /// </summary>
    /// <typeparam name="T">Stored Type</typeparam>
    /// <typeparam name="I">Key Type</typeparam>
    public interface IReadOnlyRepository<T, I> : IDisposable
    {
        T Fetch(I id);
        int Count();
        void Clear();
    }
}
