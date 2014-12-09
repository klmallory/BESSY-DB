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
using BESSy.Json;
using BESSy.Cache;
using System.Threading;
using BESSy.Indexes;

namespace BESSy.Relational
{
    public abstract class RelationshipEntity<IdType, EntityType> : IRelationalAccessor<IdType, EntityType>, IRelationalEntity<IdType, EntityType> where EntityType : class, IRelationalEntity<IdType, EntityType>
    {
        public RelationshipEntity() { }

        public RelationshipEntity(IRelationalDatabase<IdType, EntityType> repo) 
        { 
            Repository = repo;
            _relationshipIds = new Dictionary<string, IdType[]>();
        }

        object _syncRoot = new object();

        [JsonProperty("$relationshipIds")]
        private IDictionary<string, IdType[]> _relationshipIds { get; set; }

        [JsonIgnore]
        IDictionary<string, IdType[]> IRelationalAccessor<IdType, EntityType>.RelationshipIds { get { return _relationshipIds; } }

        protected void HandleOnCollectionChanged(string name, IEnumerable<EntityType> collection)
        {
            SetRelatedEntities(name, collection);
        }

        protected EntityType GetRelatedEntity(string name)
        {
            if (name.Contains("_"))
                throw new InvalidOperationException("The '_' character is not a valid field property character.");

            lock (_syncRoot)
                if (!_relationshipIds.ContainsKey(name) || _relationshipIds[name].Length < 1)
                    return default(EntityType);

            return Repository.Fetch(_relationshipIds[name][0]) as EntityType;
        }

        protected void SetRelatedEntity(string name, EntityType entity)
        {
            if (name.Contains("_"))
                throw new InvalidOperationException("The '_' character is not a valid field property character.");

            lock (_syncRoot)
            {
                var idsToDelete = _relationshipIds.ContainsKey(name) && !_relationshipIds[name].Contains(entity.Id) ? _relationshipIds[name] : new IdType[0];
                var newIds = entity != null ? new IdType[] { Repository.AddOrUpdate(entity, entity.Id) } : new IdType[0];

                if (entity == null)
                    _relationshipIds.Remove(name);
                else
                {
                    if (!_relationshipIds.ContainsKey(name))
                        _relationshipIds.Add(name, newIds);
                    else
                        _relationshipIds[name] = newIds;
                }

                Repository.UpdateCascade(new Tuple<string, IEnumerable<IdType>, IEnumerable<IdType>>(name, newIds , idsToDelete));
            }
        }

        protected IEnumerable<EntityType> GetRelatedEntities(string name)
        {
            var entities = new WatchList<EntityType>();

            entities.OnCollectionChanged += new CollectionChanged<EntityType>(HandleOnCollectionChanged);

            lock (_syncRoot)
            {
                var ids = _relationshipIds.ContainsKey(name) ? _relationshipIds[name] : new IdType[0];

                foreach (var id in ids)
                    entities.AddInternal(Repository.Fetch(id));
            }

            return entities;
        }

        protected void SetRelatedEntities(string name, IEnumerable<EntityType> entities)
        {
            if (entities == null)
                entities = new List<EntityType>();
               
            var ents = entities.Where(e => e != null);

            lock (_syncRoot)
            {
                foreach (var e in ents)
                    e.Id = Repository.AddOrUpdate(e, e.Id);

                var newIds = ents.Select(e => e.Id);
                var oldIds = _relationshipIds.ContainsKey(name) ? _relationshipIds[name] : new IdType[0];

                var idsToDelete = oldIds.Where(r => !newIds.Contains(r));
                var idsToAdd = newIds.Where(c => oldIds.Contains(c));
                
                if (_relationshipIds.ContainsKey(name))
                    _relationshipIds[name] = newIds.ToArray();
                else
                    _relationshipIds.Add(name, newIds.ToArray());

                Repository.UpdateCascade(new Tuple<string, IEnumerable<IdType>, IEnumerable<IdType>>(name, newIds, idsToDelete));
            }
        }

        [JsonIgnore]
        public IRelationalDatabase<IdType, EntityType> Repository { internal get; set; }

        public abstract bool CascadeDelete { get; }

        public IdType Id { get; set; }
    }
}
