using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Json.Utilities;
using BESSy.Relational;

namespace BESSy.Tests.Mocks
{
    public class MockProxyFactory<IdType, EntityType> : IProxyFactory<IdType, EntityType>
    {
        public Func<EntityType, IdType> IdGet {  get; set; }
        public Action<EntityType, IdType> IdSet { get; set; }
        public string IdToken { get; set; }
        public string ExternalIdToken { get; set; }
        public Func<EntityType, string> ExternalIdGet { get; set; }
        public Action<EntityType, string> ExternalIdSet { get; set; }

        public EntityType GetInstanceFor(IPocoRelationalDatabase<IdType, EntityType> repository, Json.Linq.JObject instance)
        {
            if (instance == null)
                return default(EntityType);

            var typeName = instance.Value<string>("$type");
            string tn;
            string an;

            ReflectionUtils.SplitFullyQualifiedTypeName(typeName, out tn, out an);

            if (typeof(EntityType) == typeof(MockClassA) && tn == "BESSy.Tests.Mocks.MockProxyDomain")
            {
                var entity = new MockProxyDomain(repository as IPocoRelationalDatabase<int, MockClassA>, this as IProxyFactory<int, MockClassA>);

                entity.Bessy_Proxy_JCopy_From(instance);

                return (EntityType)(object)entity;
            }
            else if (typeof(EntityType) == typeof(MockClassA) && tn == "BESSy.Tests.Mocks.MockProxyC")
            {
                var entity = new MockProxyC(repository as IPocoRelationalDatabase<int, MockClassA>, this as IProxyFactory<int, MockClassA>);

                entity.Bessy_Proxy_JCopy_From(instance);

                return (EntityType)(object)entity;
            }

            return default(EntityType);
        }

        public T GetInstanceFor<T>(IPocoRelationalDatabase<IdType, EntityType> repository) where T : EntityType
        {
            if (typeof(EntityType) == typeof(MockClassA) && typeof(T) == typeof(MockDomain))
                return (T)(object)(new MockProxyDomain(repository as IPocoRelationalDatabase<int, MockClassA>, this as IProxyFactory<int, MockClassA>));

            throw new Exception("No Bueno.");
        }

        public T GetInstanceFor<T>(IPocoRelationalDatabase<IdType, EntityType> repository, T instance) where T : EntityType
        {
            if (typeof(EntityType) == typeof(MockClassA) && instance is MockDomain)
            {
                var arg = instance != null ? (MockDomain)(object)instance : null;
                return (T)(object)(new MockProxyDomain(repository as IPocoRelationalDatabase<int, MockClassA>, this as IProxyFactory<int, MockClassA>));
            }
            else if (typeof(EntityType) == typeof(MockClassA) && instance is MockClassC)
            {
                var arg = instance != null ? (MockClassC)(object)instance : null;
                return (T)(object)(new MockProxyC(repository as IPocoRelationalDatabase<int, MockClassA>, this as IProxyFactory<int, MockClassA>));
            }

            throw new Exception("No Bueno.");
        }

        public Type GetProxyTypeFor(Type type)
        {
            if (type == typeof(MockDomain))
            {
                return typeof(MockProxyDomain);
            }
            else if (type == typeof(MockClassC))
            {
                return typeof(MockProxyC);
            }

            return type;
        }
        
    }
}
