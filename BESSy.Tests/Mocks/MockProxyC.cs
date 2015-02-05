using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Relational;
using BESSy.Json;
using BESSy.Reflection;
using BESSy.Json.Linq;

namespace BESSy.Tests.Mocks
{
    [BESSyIgnore]
    public class MockProxyC : MockClassC, IBESSyProxy<int, MockClassA>
    {
        public MockProxyC()
        {
            Bessy_Proxy_RelationshipIds = new Dictionary<string, int[]>();
        }

        public MockProxyC(IPocoRelationalDatabase<int, MockClassA> repository, IProxyFactory<int, MockClassA> factory)
            : this()
        {
            Bessy_Proxy_Repository = repository;
            Bessy_Proxy_Factory = factory;
        }

        public MockProxyC(IPocoRelationalDatabase<int, MockClassA> repository, IProxyFactory<int, MockClassA> factory, MockClassC instance)
            : this(repository, factory)
        {
            if (instance == null)
                return;
        }


        public void Bessy_Proxy_JCopy_From(JObject instance)
        {
            if (instance == null)
                return;

            var fields = new string[2] { "_fieldTest", "_fieldTest2" };

            var tokens = new string[2] { "_fieldTest", "_fieldTest2" };

            PocoProxyHandler<int, MockClassA>.CopyJFields(this, instance, this.GetType(), fields, tokens);

            JToken token;

            if (instance.TryGetValue("GetSomeCheckSum", out token))
                GetSomeCheckSum = token.ToObject<double[]>(Bessy_Proxy_Repository.Formatter.Serializer);

            if (instance.TryGetValue("$relationshipIds", out token))
                Bessy_Proxy_RelationshipIds = token.ToObject<IDictionary<string, int[]>>(Bessy_Proxy_Repository.Formatter.Serializer);

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

            if (instance.TryGetValue("$id", out token))
                Bessy_Proxy_OldIdHash = token.ToObject<string>();
        }

        public void Bessy_Proxy_Shallow_Copy_From(MockClassA entity)
        {
            var instance = entity as MockClassC;

            if (instance == null)
                return;

            if (instance.GetSomeCheckSum != null)
                GetSomeCheckSum = instance.GetSomeCheckSum.ToArray();

            LittleId = instance.LittleId;
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

            Bessy_Proxy_OldIdHash = "BESSy.Tests.Mocks.MockDomain" + Bessy_Proxy_Factory.IdGet(instance).ToString();
        }

        public void Bessy_Proxy_Deep_Copy_From(MockClassA entity)
        {
            var instance = entity as MockClassC;

            if (instance == null)
                return;

            Friend = instance.Friend;
            Other = instance.Other;


        }

        [JsonIgnore]
        public bool Bessy_OnCascade_Delete { get; set; }

        [JsonIgnore]
        public IPocoRelationalDatabase<int, MockClassA> Bessy_Proxy_Repository { get; set; }

        [JsonIgnore]
        public IProxyFactory<int, MockClassA> Bessy_Proxy_Factory { get; set; }

        [JsonProperty("$relationshipIds")]
        public IDictionary<string, int[]> Bessy_Proxy_RelationshipIds { get; set; }

        [JsonProperty("$id")]
        public string Bessy_Proxy_OldIdHash { get; set; }

        [JsonIgnore]
        public override MockClassC Friend
        {
            get { base.Friend = PocoProxyHandler<int, MockClassA>.GetRelatedEntity(this, "Friend") as MockClassC; return base.Friend; }
            set { base.Friend = value; PocoProxyHandler<int, MockClassA>.SetRelatedEntity(this, "Friend", value); }
        }

        [JsonIgnore]
        public override MockDomain Other
        {
            get { base.Other = PocoProxyHandler<int, MockClassA>.GetRelatedEntity(this, "Other") as MockDomain; return base.Other; }
            set { base.Other = value; PocoProxyHandler<int, MockClassA>.SetRelatedEntity(this, "Other", value); }
        }
    }
}
