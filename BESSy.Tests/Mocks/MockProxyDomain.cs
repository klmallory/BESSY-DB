using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Relational;
using BESSy.Json;
using BESSy.Reflection;
using System.Reflection;
using BESSy.Json.Linq;

namespace BESSy.Tests.Mocks
{
    [BESSyIgnore]
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

        public void Bessy_Proxy_JCopy_From(JObject instance)
        {
            if (instance == null)
                return;

            var fields = new string[] { "_fieldTest", "_fieldTest2", "<BDomain>k__BackingField", "<CDomain>k__BackingField", "<BDomains>k__BackingField", "<MyHashMash>k__BackingField", "<Friend>k__BackingField" };

            var tokens = new string[] { "_fieldTest", "_fieldTest2", "<BDomain>k__BackingField", "<CDomain>k__BackingField", "<BDomains>k__BackingField", "<MyHashMash>k__BackingField", "<Friend>k__BackingField" };

            PocoProxyHandler<int, MockClassA>.CopyJFields(this, instance, this.GetType(), fields, tokens);

            JToken token;

            if (instance.TryGetValue("GetSomeCheckSum", out token))
                GetSomeCheckSum = token.ToObject<double[]>(Bessy_Proxy_Repository.Formatter.Serializer);

            if (instance.TryGetValue("$relationshipIds", out token))
                Bessy_Proxy_RelationshipIds = token.ToObject<Dictionary<string, int[]>>(Bessy_Proxy_Repository.Formatter.Serializer);

            if (instance.TryGetValue("$id", out token))
                Bessy_Proxy_OldIdHash = token.Value<string>();

            if (instance.TryGetValue("MyHashMash", out token))
                MyHashMash = token.ToObject<Dictionary<int, string>>(Bessy_Proxy_Repository.Formatter.Serializer);

            if (instance.TryGetValue("ADomain", out token))
                ADomain = token.ToObject<MockClassA>();

            if (instance.TryGetValue("Location", out token))
                Location = token.ToObject<MockStruct>(Bessy_Proxy_Repository.Formatter.Serializer);

            if (instance.TryGetValue("LittleId", out token))
                LittleId = token.ToObject<short>();

            if (instance.TryGetValue("Id", out token))
                Id = token.ToObject<int>();

            if (instance.TryGetValue("BigId", out token))
                BigId = token.ToObject<Int64>();

            if (instance.TryGetValue("DecAnimal", out token))
                DecAnimal = token.ToObject<decimal>();

            if (instance.TryGetValue("MyDate", out token))
                MyDate = token.ToObject<DateTime>();

            if (instance.TryGetValue("Name", out token))
                Name = token.ToObject<string>();

            if (instance.TryGetValue("ReferenceCode", out token))
                ReferenceCode = token.ToObject<string>();

            if (instance.TryGetValue("ReplicationID", out token))
                ReplicationID = token.ToObject<Guid>();

            if (instance.TryGetValue("Unsigned16", out token))
                Unsigned16 = token.ToObject<UInt16>();

            if (instance.TryGetValue("Unsigned32", out token))
                Unsigned32 = token.ToObject<UInt32>();

            if (instance.TryGetValue("Unsigned64", out token))
                Unsigned64 = token.ToObject<UInt64>();
        }

        public void Bessy_Proxy_Shallow_Copy_From(MockClassA entity)
        {
            var instance = entity as MockDomain;

            if (instance == null)
                return;

            var fields = new string[2] { "_fieldTest", "_fieldTest2" };

            PocoProxyHandler<int, MockClassA>.CopyFields(this, instance, this.GetType(), instance.GetType(), fields);

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
            MyHashMash = instance.MyHashMash;

            Bessy_Proxy_OldIdHash =   "BESSy.Tests.Mocks.MockDomain" + Bessy_Proxy_Factory.IdGet(instance).ToString();
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
        public IPocoRelationalDatabase<int, MockClassA> Bessy_Proxy_Repository { get; set; }

        [JsonIgnore]
        public IProxyFactory<int, MockClassA> Bessy_Proxy_Factory { get; set; }

        [JsonProperty("$relationshipIds")]
        public IDictionary<string, int[]> Bessy_Proxy_RelationshipIds { get; set; }

        [JsonProperty("$id")]
        public string Bessy_Proxy_OldIdHash { get; set; }

        [JsonIgnore]
        public MockClassB Bull
        {
            get { return base.BDomain; }
        }

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
