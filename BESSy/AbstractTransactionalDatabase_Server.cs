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
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BESSy.Cache;
using BESSy.Extensions;
using BESSy.Factories;
using BESSy.Files;
using BESSy.Indexes;
using BESSy.Parallelization;
using BESSy.Queries;
using BESSy.Replication;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using BESSy.Transactions;
using BESSy.Json;
using BESSy.Json.Linq;

namespace BESSy
{
    public partial class AbstractTransactionalDatabase<IdType, EntityType>
    {
        //TODO: Build KTree for this.
        private object syncRoot = new object();
        protected IDictionary<string, Type> typeLookups = new Dictionary<string, Type>();

        protected virtual Type GetTypeFor(JToken name)
        {
            Type type = null;
            var typeName = name.Value<string>();

            lock (syncRoot)
                if (typeLookups.ContainsKey(typeName))
                    type = typeLookups[typeName];

            if (type == null)
            {
                type = Type.GetType(typeName);
                lock (syncRoot)
                {
                    if (typeLookups.Count > TaskGrouping.ArrayLimit / 4)
                        typeLookups.Clear();

                    typeLookups.Add(typeName, type);
                }
            }
            return type;
        }

        public virtual IdType AddJObj(JObject obj)
        {
            JToken name;
            
            if (!obj.TryGetValue("$type", out name))
                throw new ArgumentException("'$type' property was not found on the passed in parameter 'obj'.");

            Type type = GetTypeFor(name);

            return AddJObj(type, obj);
        }

        public virtual IdType AddJObj(Type type, JObject obj)
        {
            lock (_syncOperations)
                _operations.Push(2);

            try
            {
                var id = Seed.Increment();
                
                var idValue = obj.SelectToken(_idToken);

                if (!Seed.Passive)
                {
                    if (idValue != null && _idConverter.Compare(idValue.Value<IdType>(), default(IdType)) != 0)
                        throw new DuplicateKeyException("Id was already set on this object, you can only set the primary name of a new object with a passive core");

                    var sVal = JToken.FromObject(id, Formatter.Serializer);

                    if (idValue == null)
                        obj.Add(_idToken, sVal);
                    else
                        idValue.Replace(sVal);
                }
                else
                    id = obj.SelectToken(_idToken).Value<IdType>();
                        
                using (var tLock = _transactionManager.GetActiveTransaction(false))
                {
                    tLock.Transaction.EnlistObj(Action.Create, id, obj.ToObject(type, Formatter.Serializer));

                    return id;
                }
            }
            finally { lock (_syncOperations) _operations.Pop(); }
        }

        public virtual IdType AddOrUpdateJObj(JObject obj, IdType id)
        {
            JToken name;
            if (!obj.TryGetValue("$type", out name))
                throw new ArgumentException("'$type' property was not found on the passed in parameter 'obj'.");

            var type = Type.GetType(name.Value<string>());

            return AddOrUpdateJObj(type, obj, id);
        }

        public virtual IdType AddOrUpdateJObj(Type type, JObject obj, IdType id)
        {
            if (_idConverter.Compare(id, default(IdType)) == 0)
            { return AddJObj(type, obj); }
            else
            { Update(type, obj, id); return id; }
        }

        public virtual void Update(JObject obj, IdType id)
        {
            JToken name;
            if (!obj.TryGetValue("$type", out name))
                throw new ArgumentException("'$type' property was not found on the passed in parameter 'obj'.");

            var type = Type.GetType(name.Value<string>());

            Update(type, obj, id);
        }

        public virtual void Update(Type type, JObject obj, IdType id)
        {
            var newId = obj.SelectToken(_idToken).Value<IdType>();
            var deleteFirst = (_idConverter.Compare(newId, id) != 0);
            var item = obj.ToObject(type, Formatter.Serializer);

            lock (_syncOperations)
                _operations.Push(3);

            try
            {
                using (var tLock = _transactionManager.GetActiveTransaction(false))
                {
                    var oldSeg = _primaryIndex.FetchSegment(id);

                    if (deleteFirst)
                    {
                        tLock.Transaction.EnlistObj(Action.Delete, id, item);
                        tLock.Transaction.EnlistObj(Action.Create, newId, item);
                    }
                    else
                        tLock.Transaction.EnlistObj(Action.Update, newId, item);
                }
            }
            finally { lock (_syncOperations) _operations.Pop(); }
        }

        public virtual JObject FetchJObj(IdType id)
        {
            JObject obj = null;

            if (_transactionManager.HasActiveTransactions)
            {
                using (var t = _transactionManager.GetActiveTransaction(false))
                {
                    if (t.Transaction != null && t.Transaction.EnlistCount > 0)
                    {
                        var i = t.Transaction.GetEnlistedActions().LastOrDefault(a => _idConverter.Compare(id, a.Key) == 0);

                        if (_idConverter.Compare(default(IdType), i.Key) != 0)
                            if (i.Value.Action != Action.Delete)
                                return Formatter.AsQueryableObj(i.Value.Entity);
                            else
                                return null;
                    }
                }
            }
            lock (_stagingCache)
            {
                if (_stagingCache.Count > 0 && _stagingCache.GetCache().Any(s => s.Value.ContainsKey(id)))
                {
                    obj = _stagingCache.GetCache().Where(s => s.Value.ContainsKey(id)).Last().Value[id];

                    return obj;
                }
            }

            var seg = _primaryIndex.FetchSegment(id);

            if (seg > 0)
               obj = _fileManager.LoadJObjectFrom(seg);

            return obj;
        }

        #region Remote Queryable Interface

        public virtual int UpdateScalar(Type updateType, Func<JObject, bool> selector, Action<JObject> update)
        {
            try
            {
                var count = 0;
                lock (_syncTrans)
                {
                    if (_transactionManager.CurrentTransaction != null)
                        using (var tLock = _transactionManager.GetActiveTransaction(false))
                            if (tLock.Transaction.EnlistCount > 0)
                                foreach (var jObj in tLock.Transaction.GetEnlistedItems()
                                    .Where(s => s != null)
                                    .Select(s => Formatter.AsQueryableObj(s))
                                    .Where(selector)
                                    .ToList())
                                {
                                    update.Invoke(jObj);

                                    tLock.Transaction.EnlistObj(Action.Update, jObj.SelectToken(_idToken, true).Value<IdType>(), jObj.ToObject(updateType, Formatter.Serializer));
                                    count++;
                                }
                }
                using (var tLock = _transactionManager.GetActiveTransaction(false))
                {
                    foreach (var page in _fileManager.AsEnumerable())
                    {
                        foreach (var jObj in page.Where(o => selector(o)))
                        {
                            update.Invoke(jObj);

                            tLock.Transaction.EnlistObj(Action.Update, jObj.SelectToken(_idToken, true).Value<IdType>(), jObj.ToObject(updateType, Formatter.Serializer));
                            count++;
                        }
                    }
                }

                return count;
            }
            catch (Exception ex) { throw new QueryExecuteException("Update Query Execution Failed.", ex); }
        }


        public virtual IList<JObject> SelectJObj(Func<JObject, bool> selector)
        {
            try
            {
                var list = new List<JObject>();

                if (_transactionManager.CurrentTransaction != null)
                    using (var tLock = _transactionManager.GetActiveTransaction(false))
                        if (tLock.Transaction.EnlistCount > 0)
                            list.AddRange(tLock.Transaction.GetEnlistedItems()
                                .Where(s => s != null)
                                .Select(s => Formatter.AsQueryableObj(s))
                                .Where(selector));

                lock (_stagingCache)
                    if (_stagingCache.Count > 0)
                        list.AddRange(_stagingCache.GetCache()
                             .Where(s => s.Value != null)
                            .SelectMany(s => s.Value.Values)
                            .Where(v => v != null)
                            .Where(selector));

                foreach (var page in _fileManager.AsEnumerable())
                    foreach (var obj in page.Where(o => selector(o)))
                        list.Add(obj);

                return list.GroupBy(k => k.Value<IdType>(_idToken)).Select(f => f.First()).ToList();
            }
            catch (Exception ex) { throw new QueryExecuteException("Select Query Execution Failed.", ex); }
        }

        public virtual IList<JObject> SelectJObjFirst(Func<JObject, bool> selector, int max)
        {
            try
            {
                var list = new List<JObject>();

                if (_transactionManager.CurrentTransaction != null)
                    using (var tLock = _transactionManager.GetActiveTransaction(false))
                        if (list.Count < max && tLock.Transaction.EnlistCount > 0)
                            list.AddRange(tLock.Transaction.GetEnlistedItems()
                                .Where(s => s != null)
                                .Select(s => Formatter.AsQueryableObj(s))
                                .Where(selector)
                                .Take(max));

                lock (_stagingCache)
                    if (list.Count < max && _stagingCache.Count > 0)
                        list.AddRange(_stagingCache.GetCache()
                            .Where(s => s.Value != null)
                            .SelectMany(s => s.Value.Values)
                            .Where(v => v != null)
                            .Where(selector)
                            .Take(max));

                if (list.Count < max)
                {
                    foreach (var page in _fileManager.AsEnumerable())
                    {
                        list.AddRange(page.Where(o => selector(o)));

                        if (list.Count >= max)
                            break;
                    }
                }

                return list.GroupBy(k => k.Value<IdType>(_idToken)).Select(f => f.First()).Take(Math.Min(list.Count, max)).ToList();
            }
            catch (Exception ex) { throw new QueryExecuteException("SelectFirst Query Execution Failed.", ex); }
        }

        public virtual IList<JObject> SelectJObjLast(Func<JObject, bool> selector, int max)
        {
            try
            {
                var list = new List<JObject>();

                if (_transactionManager.CurrentTransaction != null)
                    using (var tLock = _transactionManager.GetActiveTransaction(false))
                        if (list.Count < max && tLock.Transaction.EnlistCount > 0)
                            list.AddRange(tLock.Transaction.GetEnlistedItems()
                                .Where(s => s != null)
                                .Select(s => Formatter.AsQueryableObj(s))
                                .Where(selector)
                                .Reverse());

                lock (_stagingCache)
                    if (list.Count < max && _stagingCache.Count > 0)
                        list.AddRange(_stagingCache.GetCache()
                            .Where(s => s.Value != null)
                            .SelectMany(s => s.Value.Values)
                            .Where(v => v != null)
                            .Where(selector)
                            .Reverse());

                if (list.Count < max)
                {
                    foreach (var page in _fileManager.AsReverseEnumerable())
                    {
                        list.AddRange(page.Where(o => selector(o)));

                        if (list.Count >= max)
                            break;
                    }
                }

                return list.GroupBy(k => k.Value<IdType>(_idToken)).Select(f => f.First()).Take(Math.Min(list.Count, max)).ToList();
            }
            catch (Exception ex) { throw new QueryExecuteException("SelectLast Query Execution Failed.", ex); }
        }

        #endregion
    }
}
