using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Json;
using BESSy.Relational;

namespace BESSy.Tests.Mocks
{
    public class MockClassE : MockClassD
    {
        public MockClassE() : base() { }
        public MockClassE(IRelationalDatabase<int, MockClassD> repo) : base(repo) { }

        public string ReferenceCode { get; set; }
        public MockStruct Location { get; set; }
        public string[] NamesOfStuff { get; set; }
        public string TPSCoverSheet { get; set; }
        public double[] GetSomeCheckSum { get; set; }

        [JsonIgnore]
        public MockClassE Parent { get { return GetRelatedEntity("parent") as MockClassE; } set { SetRelatedEntity("parent", value); } }
    }
}
