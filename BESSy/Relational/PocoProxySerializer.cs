using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BESSy.Json.Serialization;

namespace BESSy.Relational
{
    public class PocoProxySerializer : DefaultContractResolver
    {
        public PocoProxySerializer() : base()
        {
            IgnoreSerializableInterface = true;
        }

        public Func<Type, Type> GetTypeFor { get; set; }

        public override JsonContract ResolveContract(Type type)
        {
            if (type.GetInterface("IBESSyProxy`2") != null)
                return base.ResolveContract(type);
            
            var proxyType = GetTypeFor(type);

            return base.ResolveContract(proxyType);
        }
    }
}
