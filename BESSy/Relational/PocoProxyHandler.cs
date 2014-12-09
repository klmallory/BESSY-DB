using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Threading;
using System.Threading.Tasks;
using BESSy.Cache;
using BESSy.Extensions;
using BESSy.Factories;
using BESSy.Files;
using BESSy.Indexes;
using BESSy.Json;
using BESSy.Json.Linq;
using BESSy.Parallelization;
using BESSy.Queries;
using BESSy.Replication;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using BESSy.Transactions;

namespace BESSy.Relational
{
    public class PocoProxyHandler<IdType, EntityType>
    {
        public static void CopyVaueField<T>(ref T cloneField, ref T originalField)
        {
            cloneField = originalField;
        }

        //public static void CopyReferenceField<T>(ref T cloneField, ref T originalField)
        //{
        //    originalField
        //}

        public static void HandleOnCollectionChanged(object proxy, string name, IEnumerable<EntityType> collection)
        {
            var p = proxy as IBESSyProxy<IdType, EntityType>;

            SetRelatedEntities(p, name, collection);
        }

        public static EntityType GetRelatedEntity(IBESSyProxy<IdType, EntityType> proxy, string name)
        {
            if (proxy == null || proxy.Bessy_Proxy_RelationshipIds == null || proxy.Bessy_Proxy_Repository == null)
                return default(EntityType);

            if (name.Contains("_"))
                throw new InvalidOperationException("The '_' character is not a valid field property character.");

            lock (proxy)
                if (!proxy.Bessy_Proxy_RelationshipIds.ContainsKey(name) || proxy.Bessy_Proxy_RelationshipIds[name].Length < 1)
                    return default(EntityType);

            return proxy.Bessy_Proxy_Repository.Fetch(proxy.Bessy_Proxy_RelationshipIds[name][0]);
        }

        public static void SetRelatedEntity(IBESSyProxy<IdType, EntityType> proxy, string propertyName, EntityType entity)
        {
            if (proxy == null || proxy.Bessy_Proxy_RelationshipIds == null || proxy.Bessy_Proxy_Repository == null)
                return;

            if (entity == null)
            {
                proxy.Bessy_Proxy_Repository.UpdateCascade(new Tuple<string, IEnumerable<IdType>,
                    IEnumerable<IdType>>(propertyName, new IdType[0],
                   proxy.Bessy_Proxy_RelationshipIds.ContainsKey(propertyName)
                   ? proxy.Bessy_Proxy_RelationshipIds[propertyName]
                   : new IdType[0]));

                proxy.Bessy_Proxy_RelationshipIds.Remove(propertyName);

                return;
            }

            var idsToDelete = entity != null
                && proxy.Bessy_Proxy_RelationshipIds.ContainsKey(propertyName)
                && !proxy.Bessy_Proxy_RelationshipIds[propertyName].Contains(proxy.Bessy_Proxy_Factory.IdGet(entity))
                    ? proxy.Bessy_Proxy_RelationshipIds[propertyName]
                    : new IdType[0];

            var id = proxy.Bessy_Proxy_Factory.IdGet(entity);

            if (!(entity is IBESSyProxy<IdType, EntityType>))
                id = proxy.Bessy_Proxy_Repository.Add(entity);

            lock (proxy)
            {
                var newIds = new IdType[] { id };

                if (!proxy.Bessy_Proxy_RelationshipIds.ContainsKey(propertyName))
                    proxy.Bessy_Proxy_RelationshipIds.Add(propertyName, newIds);
                else
                    proxy.Bessy_Proxy_RelationshipIds[propertyName] = newIds;

                proxy.Bessy_Proxy_Repository.UpdateCascade(new Tuple<string, IEnumerable<IdType>, IEnumerable<IdType>>(propertyName, newIds, idsToDelete));
            }
        }

        public static IList<EntityType> GetRelatedEntities(IBESSyProxy<IdType, EntityType> proxy, string name)
        {
            var entities = new ProxyWatchList<EntityType>();

            if (proxy == null || proxy.Bessy_Proxy_RelationshipIds == null || proxy.Bessy_Proxy_Repository == null)
                return entities;

            entities.OnCollectionChanged += new ProxyCollectionChanged<EntityType>(HandleOnCollectionChanged);

            lock (proxy)
            {
                var ids = proxy.Bessy_Proxy_RelationshipIds.ContainsKey(name) ? proxy.Bessy_Proxy_RelationshipIds[name] : new IdType[0];

                foreach (var id in ids)
                    entities.AddInternal(proxy.Bessy_Proxy_Repository.Fetch(id));
            }

            return entities;
        }

        public static void SetRelatedEntities(IBESSyProxy<IdType, EntityType> proxy, string name, IEnumerable<EntityType> entities)
        {
            if (proxy == null || proxy.Bessy_Proxy_RelationshipIds == null || proxy.Bessy_Proxy_Repository == null)
                return;

            var oldIds = new IdType[0];

            lock (proxy)
                oldIds = proxy.Bessy_Proxy_RelationshipIds.ContainsKey(name) ? proxy.Bessy_Proxy_RelationshipIds[name] : new IdType[0];

            if (entities == null)
            {
               if (oldIds.Length > 0)
                   proxy.Bessy_Proxy_Repository.UpdateCascade(new Tuple<string, IEnumerable<IdType>, IEnumerable<IdType>>(name, new IdType[0], oldIds));

                return;
            }

            IList<IBESSyProxy<IdType, EntityType>> entityProxies = new List<IBESSyProxy<IdType, EntityType>>();

            var newIds = new List<IdType>();
            var idsToDelete = new List<IdType>();
            var idsToAdd = new List<IdType>();

            foreach (var entity in entities)
            {
                if (entity == null)
                    continue;

                var id = proxy.Bessy_Proxy_Factory.IdGet(entity);

                if (!(entity is IBESSyProxy<IdType, EntityType>))
                    id = proxy.Bessy_Proxy_Repository.Add(entity);

                newIds.Add(id);
            }

            idsToDelete.AddRange(oldIds.Where(r => !newIds.Contains(r)));
            idsToAdd.AddRange(newIds.Where(c => oldIds.Contains(c)));

            lock (proxy)
            {
                if (proxy.Bessy_Proxy_RelationshipIds.ContainsKey(name))
                    proxy.Bessy_Proxy_RelationshipIds[name] = newIds.ToArray();
                else
                    proxy.Bessy_Proxy_RelationshipIds.Add(name, newIds.ToArray());
            }
            proxy.Bessy_Proxy_Repository.UpdateCascade(new Tuple<string, IEnumerable<IdType>, IEnumerable<IdType>>(name, newIds, idsToDelete));
        }
    }
}
