/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Tests.Mocks
{
    internal class MockClassD : RelationshipEntity<int>
    {
        public MockClassD(IRepository<RelationshipEntity<int>, int> repo) : base(repo) { }

        public Guid ReplicationID { get; set; }
        public virtual string Name { get; set; }
        public virtual string CatalogName { get { return Name == null || Name.Length < 1 ? "_" : Name.Substring(0, 1).ToUpper(); } }
        public virtual string CatalogNameNull { get { return null; } }

        MockClassD HiC { get { return GetRelatedEntity("hic") as MockClassD; } set { SetRelatedEntity("hic", value); } }
    }

    internal static class ExtendMockClassD
    {
        public static MockClassD WithName(this MockClassD mock, string name)
        {
            mock.Name = name;

            return mock;
        }

        public static MockClassD WithId(this MockClassD mock, int id)
        {
            mock.Id = id;

            return mock;
        }
    }
}
