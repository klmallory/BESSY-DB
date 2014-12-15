using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Relational;
using BESSy.Json;
using BESSy.Reflection;

namespace BESSy.Tests.Mocks
{
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

        [JsonProperty("$oldId")]
        public int Bessy_Proxy_OldId { get; set; }

        [JsonProperty("$simpleTypeName")]

        public string Bessy_Proxy_Simple_Type_Name { get; set; }

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
