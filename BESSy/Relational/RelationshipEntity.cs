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

namespace BESSy.Relational
{


    public class RelationshipEntity<IdType>
    {
        public RelationshipEntity(IRepository<RelationshipEntity<IdType>, IdType> repo)
        {
            Repository = repo;

            _relationshipIds = new Dictionary<string, IdType>();
        }

        object _syncRoot = new object();

        protected RelationshipEntity<IdType> GetRelatedEntity(string name)
        {
            if (name.Contains("_"))
                throw new InvalidOperationException("The '_' character is not a valid field name character.");

            if (!_relationshipIds.ContainsKey(name))
                return null;

            return Repository.Fetch(_relationshipIds[name]) as RelationshipEntity<IdType>;
        }

        protected void SetRelatedEntity(string name, RelationshipEntity<IdType> entity)
        {
            if (name.Contains("_"))
                throw new InvalidOperationException("The '_' character is not a valid field name character.");

            if (entity == null)
            {
                _relationshipIds.Remove(name);
            }
            else
            {
                if (!_relationshipIds.ContainsKey(name))
                    _relationshipIds.Add(name, Repository.AddOrUpdate(entity, entity.Id));
                else
                    _relationshipIds[name] = Repository.AddOrUpdate(entity, entity.Id);
            }
        }

        protected IEnumerable<RelationshipEntity<IdType>> GetRelatedEntities(string name)
        {
            if (name.Contains("_"))
                throw new InvalidOperationException("The '_' character is not a valid field name character.");

            List<RelationshipEntity<IdType>> entities = new List<RelationshipEntity<IdType>>();

            foreach (var rel in _relationshipIds.Where(r => r.Key.StartsWith(name + "_")).OrderBy(o => o.Value))
            {
                if (rel.Value == null)
                    continue;

                entities.Add(Repository.Fetch(rel.Value) as RelationshipEntity<IdType>);
            }

            return entities;
        }

        protected void SetRelatedEntities(string name, IEnumerable<RelationshipEntity<IdType>> entities)
        {
            if (name.Contains("_"))
                throw new InvalidOperationException("The '_' character is not a valid field name character.");

            _relationshipIds.Where(r => r.Key.StartsWith(name + "_")).ToList().ForEach(r => _relationshipIds.Remove(r.Key));

            if (entities == null)
                return;

            foreach (var ent in entities)
            {
                if (ent == null)
                    continue;

                var key = name + "_" + ent.Id.ToString();

                if (_relationshipIds.ContainsKey(key))
                    _relationshipIds[key] = Repository.AddOrUpdate(ent, ent.Id);
                else
                    _relationshipIds.Add(key, Repository.AddOrUpdate(ent, ent.Id));
            }
        }

        [JsonProperty("_relationshipIds")]
        private IDictionary<string, IdType> _relationshipIds { get; set; }

        [JsonIgnore]
        internal IRepository<RelationshipEntity<IdType>, IdType> Repository { get; set; }
        protected void SetRepository(IRepository<RelationshipEntity<IdType>, IdType> repo) { Repository = repo; }

        public IdType Id { get; set; }
    }
}
