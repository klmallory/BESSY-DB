/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Json;
using BESSy.Relational;

namespace BESSy.Tests.Mocks
{
    public class MockClassD : RelationshipEntity<int, MockClassD>
    {
        public MockClassD() : base() { }
        public MockClassD(IRelationalDatabase<int, MockClassD> repo) : base(repo) { }

        public Guid ReplicationID { get; set; }
        public virtual string Name { get; set; }
        public virtual string CatalogName { get { return Name == null || Name.Length < 1 ? "_" : Name.Substring(0, 1).ToUpper(); } }
        public virtual string CatalogNameNull { get { return null; } }

        [JsonIgnore]
        public MockClassD HiC { get { return GetRelatedEntity("hic") as MockClassD; } set { SetRelatedEntity("hic", value); } }

        [JsonIgnore]
        public IEnumerable<MockClassD> LowBall { get { return GetRelatedEntities("lowball").Cast<MockClassD>(); } set { SetRelatedEntities("lowball", value); } }

        public void SetRepository(IRelationalDatabase<int, MockClassD> repo) { base.Repository = repo; }

        public override bool CascadeDelete { get { return true; } }
    }
}
