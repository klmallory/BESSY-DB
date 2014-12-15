using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BESSy.Relational;

namespace BESSy.Tests.Mocks
{
    public class MockProxyFactory<IdType, EntityType> : IProxyFactory<IdType, EntityType>
    {
        public Func<EntityType, IdType> IdGet {  get; set; }
        public Action<EntityType, IdType> IdSet { get; set; }
        public string IdToken { get; set; }

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
