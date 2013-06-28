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
using Newtonsoft.Json;

namespace BESSy
{
    public class RelationshipEntity<IdType>
    {
        public RelationshipEntity(IRepository<RelationshipEntity<IdType>, IdType> repo)
        {
            _repo = repo;
        }

        private object _syncRoot = new object();

        [JsonIgnore]
        private IRepository<RelationshipEntity<IdType>, IdType> _repo;

        [JsonProperty("_relationshipIds")]
        private IDictionary<string, IdType> _relationshipIds = new Dictionary<string, IdType>();

        protected RelationshipEntity<IdType> GetRelatedEntity(string name)
        {
            if (!_relationshipIds.ContainsKey(name))
                return null;

            return _repo.Fetch(_relationshipIds[name]);
        }

        protected void SetRelatedEntity(string name, RelationshipEntity<IdType> entity)
        {
            if (!_relationshipIds.ContainsKey(name))
                _relationshipIds.Add(name, entity.Id);
            else if (entity != null)
                _relationshipIds[name] = entity.Id;
            else   
                _relationshipIds.Remove(name);

            _repo.AddOrUpdate(entity, entity.Id);
        }

        public IdType Id { get; set; }
    }
}
