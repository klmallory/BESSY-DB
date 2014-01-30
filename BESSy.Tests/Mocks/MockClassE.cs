using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Json;
using BESSy.Relational;

namespace BESSy.Tests.Mocks
{
    internal class MockClassE : MockClassD
    {
        public MockClassE(IRepository<RelationshipEntity<int>, int> repo) : base(repo) { }

        public string ReferenceCode { get; set; }
        public MockStruct Location { get; set; }
        public string[] NamesOfStuff { get; set; }
        public string TPSCoverSheet { get; set; }
        public double[] GetSomeCheckSum { get; set; }

        [JsonIgnore]
        public MockClassE Parent { get { return GetRelatedEntity("parent") as MockClassE; } set { SetRelatedEntity("parent", value); } }
    }
}
