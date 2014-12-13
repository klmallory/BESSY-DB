using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Relational;
using BESSy.Json;
using BESSy.Reflection;

namespace BESSy.Tests.Mocks
{
    public class MockProxyDomain : MockDomain, IBESSyProxy<int, MockClassA>
    {
        public MockProxyDomain()
            : base()
        {
            Bessy_Proxy_RelationshipIds = new Dictionary<string, int[]>();
        }

        public MockProxyDomain(IPocoRelationalDatabase<int, MockClassA> repository, IProxyFactory<int, MockClassA> factory) : this()
        {
            Bessy_Proxy_Repository = repository;
            Bessy_Proxy_Factory = factory;
        }

        public void Bessy_Proxy_Shallow_Copy_From(MockClassA entity)
        {
            var instance = entity as MockDomain;

            if (instance == null)
                return;

            var exposed = DynamicMemberManager.GetManager(instance);
            var local = DynamicMemberManager.GetManager(this);

            local._fieldTest = exposed._fieldTest;

            LittleId = instance.LittleId;

            if (instance.GetSomeCheckSum != null)
                GetSomeCheckSum = instance.GetSomeCheckSum.ToArray();

            BigId = instance.BigId;
            DecAnimal = instance.DecAnimal;
            Location = instance.Location;
            MyDate = instance.MyDate;
            Name = instance.Name;
            ReferenceCode = instance.ReferenceCode;
            ReplicationID = instance.ReplicationID;
            Unsigned16 = instance.Unsigned16;
            Unsigned32 = instance.Unsigned32;
            Unsigned64 = instance.Unsigned64;

            Bessy_Proxy_OldId = Bessy_Proxy_Factory.IdGet(instance);
            Bessy_Proxy_Simple_Type_Name = "BESSy.Tests.Mocks.MockDomain";
        }

        public void Bessy_Proxy_Deep_Copy_From(MockClassA entity)
        {
            var instance = entity as MockDomain;

            if (instance == null)
                return;

            ADomain = instance.ADomain;
            BDomain = instance.BDomain;
            CDomain = instance.CDomain;

            if(instance.CDomains != null)
                CDomains = instance.CDomains.ToList();

            if (instance.BDomains != null)
                BDomains = instance.BDomains.ToArray();

            Friend = instance.Friend;
        }

        [JsonIgnore]
        public bool Bessy_OnCascade_Delete { get; set; }

        [JsonIgnore]
        public IPocoRelationalDatabase<int, MockClassA> Bessy_Proxy_Repository { get; set; }

        [JsonIgnore]
        public IProxyFactory<int, MockClassA> Bessy_Proxy_Factory { get; set; }

        [JsonProperty("$relationshipIds")]
        public IDictionary<string, int[]> Bessy_Proxy_RelationshipIds { get; set; }

        [JsonProperty("$oldId")]
        public int Bessy_Proxy_OldId { get; set; }

        [JsonProperty("$simpleTypeName")]
        public string Bessy_Proxy_Simple_Type_Name { get; set; }

        [JsonIgnore]
        public override MockClassB BDomain
        {
            get { base.BDomain = PocoProxyHandler<int, MockClassA>.GetRelatedEntity(this, "BDomain") as MockClassB; return base.BDomain; }
            set { base.BDomain = value; PocoProxyHandler<int, MockClassA>.SetRelatedEntity(this, "BDomain", value); }
        }

        [JsonIgnore]
        public override MockClassC CDomain
        {
            get { base.CDomain = PocoProxyHandler<int, MockClassA>.GetRelatedEntity(this, "CDomain") as MockClassC; return base.CDomain; }
            set { base.CDomain = value; PocoProxyHandler<int, MockClassA>.SetRelatedEntity(this, "CDomain", value); }
        }

        [JsonIgnore]
        public override IList<MockClassC> CDomains
        {
            get { base.CDomains = PocoProxyHandler<int, MockClassA>.GetRelatedEntities(this, "CDomains").Cast<MockClassC>().ToList(); return base.CDomains; }
            set { base.CDomains = value; PocoProxyHandler<int, MockClassA>.SetRelatedEntities(this, "CDomains", value); }
        }

        [JsonIgnore]
        public override MockClassB[] BDomains
        {
            get { base.BDomains = PocoProxyHandler<int, MockClassA>.GetRelatedEntities(this, "BDomains").Cast<MockClassB>().ToArray(); return base.BDomains; }
            set { base.BDomains = value; PocoProxyHandler<int, MockClassA>.SetRelatedEntities(this, "BDomains", value); }
        }
    }
}
