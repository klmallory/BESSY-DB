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

namespace BESSy
{
    public enum Action
    {
        Create,
        Update,
        Delete
    }

    public interface IQueryableRepository<EntityType> : IQueryableReadOnlyRepository<EntityType>
    {
        int Delete(Func<JObject, bool> selector);
        int Update(Func<JObject, bool> selector, params Action<EntityType>[] updates);
    }

    public interface IQueryableReadOnlyRepository<EntityType>
    {
        IList<EntityType> Select(Func<JObject, bool> selector);
        IList<EntityType> SelectFirst(Func<JObject, bool> selector, int max);
        IList<EntityType> SelectLast(Func<JObject, bool> selector, int max);
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
    /// Contract for a repository that requires initialization.
    /// </summary>
    public interface ILoad
    {
        /// <summary>
        /// Initialize the repository from it's underlying device.
        /// </summary>
        /// <returns>The amount of records contained in this repository's underlying device.</returns>
        int Load();
    }

    /// <summary>
    /// Contract for a repository that supports saving data directly to it's source on demand.
    /// </summary>
    /// <typeparam name="EntityType">Entity Type</typeparam>
    public interface IFlush<EntityType>
    {
        void Flush(IList<EntityType> data);
        bool FileFlushQueueActive { get; }
    }

    /// <summary>
    /// Contract for a repository that supports a write through to it's source on demand.
    /// </summary>
    public interface IFlush
    {
        void Flush();
        bool FileFlushQueueActive { get; }
    }

    /// <summary>
    /// Contract for a repository that supports basic CRUD operations.
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
    /// Contract for a repository that supports basic read operations.
    /// </summary>
    /// <typeparam name="T">Stored Type</typeparam>
    /// <typeparam name="I">Key Type</typeparam>
    public interface IReadOnlyRepository<T, I> : IDisposable
    {
        T Fetch(I id);
        int Length { get; }
        void Clear();
    }
}
