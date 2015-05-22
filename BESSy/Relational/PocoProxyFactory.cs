/*
Copyright (c) 2011,2012,2013 Kristen Mallory dba Klink

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Security;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using BESSy.Cache;
using BESSy.Extensions;
using BESSy.Factories;
using BESSy.Files;
using BESSy.Indexes;
using BESSy.Json;
using BESSy.Json.Linq;
using BESSy.Json.Utilities;
using BESSy.Parallelization;
using BESSy.Queries;
using BESSy.Reflection;
using BESSy.Replication;
using BESSy.Seeding;
using BESSy.Serialization;
using BESSy.Serialization.Converters;
using BESSy.Synchronization;
using BESSy.Transactions;
using Microsoft.CSharp.RuntimeBinder;

namespace BESSy.Relational
{
    public interface IBESSyProxy<IdType, EntityType>
    {
        IPocoRelationalDatabase<IdType, EntityType> Bessy_Proxy_Repository { get; set; }
        IProxyFactory<IdType, EntityType> Bessy_Proxy_Factory { get; set; }
        string Bessy_Proxy_OldIdHash { get; set; }
        IDictionary<string, IdType[]> Bessy_Proxy_RelationshipIds { get; set; }
        void Bessy_Proxy_Shallow_Copy_From(EntityType entity);
        void Bessy_Proxy_Deep_Copy_From(EntityType entity);
        void Bessy_Proxy_JCopy_From(JObject instance);
    }

    public interface IProxyFactory<IdType, EntityType>
    {
        string IdToken { get; set; }
        Func<EntityType, IdType> IdGet { get; set; }
        Action<EntityType, IdType> IdSet { get; set; }

        string ExternalIdToken { get; set; }
        Func<EntityType, String> ExternalIdGet { get; set; }
        Action<EntityType, String> ExternalIdSet { get; set; }

        Type GetProxyTypeFor(Type type);
        T GetInstanceFor<T>(IPocoRelationalDatabase<IdType, EntityType> repository, T instance) where T : EntityType;
        EntityType GetInstanceFor(IPocoRelationalDatabase<IdType, EntityType> repository, JObject instance);
    }

    [SecuritySafeCritical]
    public class PocoProxyFactory<IdType, EntityType> : IProxyFactory<IdType, EntityType>, IDisposable
    {
        CustomAttributeBuilder cabPartialTrust = new CustomAttributeBuilder(typeof(AllowPartiallyTrustedCallersAttribute).GetConstructor(new Type[0]), new object[0]);
        CustomAttributeBuilder cabJsonIgnore = new CustomAttributeBuilder(typeof(JsonIgnoreAttribute).GetConstructor(new Type[0]), new object[0]);
        CustomAttributeBuilder cabJsonPropertyIds = new CustomAttributeBuilder(typeof(JsonPropertyAttribute).GetConstructor(new Type[] { typeof(string) }), new object[] { "$relationshipIds" });
        CustomAttributeBuilder cabJsonPropertyOldId = new CustomAttributeBuilder(typeof(JsonPropertyAttribute).GetConstructor(new Type[] { typeof(string) }), new object[] { "$oldId" });
        CustomAttributeBuilder cabJsonPropertySimpleTypeName = new CustomAttributeBuilder(typeof(JsonPropertyAttribute).GetConstructor(new Type[] { typeof(string) }), new object[] { "$simpleTypeName" });
        CustomAttributeBuilder cabSecSafeCrit = new CustomAttributeBuilder(typeof(SecuritySafeCriticalAttribute).GetConstructor(new Type[0]), new object[0]);

        ConstructorInfo ciIdsBuilder = typeof(System.Collections.Generic.Dictionary<string, IdType[]>)
                .GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { }, null);

        Type idType = typeof(IdType);
        Type entityType = typeof(EntityType);

        readonly MethodInfo getRelatedEntity = typeof(PocoProxyHandler<IdType, EntityType>).GetMethod("GetRelatedEntity",
            BindingFlags.Static | BindingFlags.Public, null,
            new Type[] { typeof(BESSy.Relational.IBESSyProxy<IdType, EntityType>), typeof(String) }, null);

        readonly MethodInfo setRelatedEntity = typeof(PocoProxyHandler<IdType, EntityType>).GetMethod("SetRelatedEntity",
            BindingFlags.Static | BindingFlags.Public, null,
            new Type[] { typeof(BESSy.Relational.IBESSyProxy<IdType, EntityType>), typeof(String), typeof(EntityType) },
            null);

        readonly MethodInfo getRelatedEntities = typeof(PocoProxyHandler<IdType, EntityType>).GetMethod("GetRelatedEntities"
            , BindingFlags.Static | BindingFlags.Public, null,
            new Type[] { typeof(BESSy.Relational.IBESSyProxy<IdType, EntityType>), typeof(String) }, null);

        readonly MethodInfo setRelatedEntities = typeof(PocoProxyHandler<IdType, EntityType>).GetMethod("SetRelatedEntities",
            BindingFlags.Static | BindingFlags.Public, null,
            new Type[] { typeof(BESSy.Relational.IBESSyProxy<IdType, EntityType>), typeof(String), typeof(IEnumerable<EntityType>) },
            null);

        readonly MethodInfo getType = typeof(Object).GetMethod("GetType", 
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new Type[] { }, null);

        readonly MethodInfo copyFields = typeof(BESSy.Relational.PocoProxyHandler<IdType, EntityType>).GetMethod("CopyFields", 
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, 
            null, new Type[]{ typeof(Object), typeof(Object), typeof(Type), typeof(Type), typeof(String[]) },  null);

        readonly MethodInfo copyJFields = typeof(BESSy.Relational.PocoProxyHandler<IdType, EntityType>).GetMethod("CopyJFields",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null, new Type[] { typeof(Object), typeof(JObject), typeof(Type), typeof(String[]), typeof(String[]) }, null);

        readonly MethodInfo getFormatter = typeof(BESSy.ITransactionalDatabase<IdType, JObject>).GetMethod("get_Formatter",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new Type[] { }, null);

        readonly MethodInfo getSerializer = typeof(IQueryableFormatter).GetMethod("get_Serializer",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new Type[] { }, null);

        readonly MethodInfo jTokenValue = typeof(JToken).GetMethod("Value",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new Type[] { typeof(Object) }, null);

        readonly MethodInfo jTokenTryGetValue = typeof(JObject).GetMethod("TryGetValue",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new Type[] { typeof(String), typeof(JToken).MakeByRefType() }, null);

        MethodInfo stringConcat = typeof(String).GetMethod("Concat",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null, new Type[] { typeof(Object), typeof(Object) }, null);

        MethodInfo idToString = typeof(IdType).GetMethod("ToString",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new Type[] { }, null);


        readonly MethodInfo getIdFromFactory = null;

        readonly MethodInfo getIdInvoke = typeof(System.Func<EntityType, IdType>).GetMethod("Invoke");

        readonly MethodInfo getExternalIdFromFactory = null;

        readonly MethodInfo getExternalIdInvoke = typeof(System.Func<EntityType, String>).GetMethod("Invoke");

        bool useTransientAssembly = true;
        string assemblyName = "BESSy.Proxy";

        public PocoProxyFactory()
        {
            getIdFromFactory = this.GetType().GetMethod("get_IdGet");
            getExternalIdFromFactory = this.GetType().GetMethod("get_ExternalIdGet");
        }

        public PocoProxyFactory(string assemblyName, bool useTransientAssembly)
            : this()
        {
            this.useTransientAssembly = useTransientAssembly;
            this.assemblyName = assemblyName;
        }

        object _syncRoot = new object();
        IDictionary<string, Type> _typeCache = new Dictionary<string, Type>();
        IDictionary<string, AssemblyBuilder> _assemblyBuilderCache = new Dictionary<string, AssemblyBuilder>();
        IDictionary<string, Assembly> _assemblyCache = new Dictionary<string, Assembly>();

        public Func<EntityType, IdType> IdGet { get; set; }
        public Action<EntityType, IdType> IdSet { get; set; }
        public string IdToken { get; set; }

        public Func<EntityType, String> ExternalIdGet { get; set; }
        public Action<EntityType, String> ExternalIdSet { get; set; }
        public string ExternalIdToken { get; set; }

        private Assembly LoadAssembly(string an)
        {
            lock (_syncRoot)
                if (_assemblyCache.ContainsKey(an))
                    return _assemblyCache[an];

            lock (_syncRoot)
                if (_assemblyBuilderCache.ContainsKey(an))
                    return _assemblyBuilderCache[an];

            try
            {
                var assembly = Assembly.LoadWithPartialName(an);

                if (assembly != null)
                    return assembly;

                Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

                //load partial name first for dynamic assemblies
                for (var i = 0; i < loadedAssemblies.Length; i++)
                    if (loadedAssemblies[i].FullName.StartsWith(assemblyName + ","))
                    { assembly = loadedAssemblies[i]; break; }

                return assembly;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Unable to load assembly {0}", ex);
            }

            return null;
        }

        public T GetInstanceFor<T>(IPocoRelationalDatabase<IdType, EntityType> repository, T instance) where T : EntityType
        {
            return GetProxyFor<T>(repository, instance);
        }

        public EntityType GetInstanceFor(IPocoRelationalDatabase<IdType, EntityType> repository, JObject instance)
        {
           return  GetProxyFor(repository, instance);
        }

        protected EntityType GetProxyFor(IPocoRelationalDatabase<IdType, EntityType> repository, JObject instance)
        {
            var typeName = instance.Value<string>("$type");
            string tn;
            string an;

            ReflectionUtils.SplitFullyQualifiedTypeName(typeName, out tn, out an);
            var assembly = LoadAssembly(an);

            if (assembly == null)
            {
                var baseAssemblyName = an.Replace("BESSy.Proxy.", "");
                assembly = BuildDomainProxies(baseAssemblyName);

                if (assembly == null)
                    throw new ProxyCreationException(string.Format("Could not create proxy for serialized type {0}", typeName));
            }

            var key = _typeCache.Keys.FirstOrDefault(k => k.StartsWith(typeName));

            if (!string.IsNullOrWhiteSpace(key))
            {
                var proxy = (EntityType)Activator.CreateInstance(_typeCache[key], repository, this);

                var iProxy = proxy as IBESSyProxy<IdType, EntityType>;

                if (iProxy != null)
                    iProxy.Bessy_Proxy_JCopy_From(instance);

                return proxy;
            }
            else
                throw new ProxyCreationException(string.Format("Type not found. Could not create proxy for serialized type {0}", typeName));
        }

        public Type GetProxyTypeFor(Type type)
        {
            var ttw = type;

            if (ttw.GetInterface("IBESSyProxy`2", true) != null)
                return ttw;

            while (ttw.DeclaringType != null)
                if (ttw.DeclaringType.GetInterface("IBESSyProxy`2", true) != null)
                    return ttw.DeclaringType;
                else
                    ttw = ttw.DeclaringType;

            ttw = type;

            while (ttw.Assembly.IsDynamic)
            {
                if (ttw.BaseType == null)
                    throw new ProxyCreationException(string.Format("Unable to create proxy of another proxy: {0}", type));

                ttw = ttw.BaseType;
            }

            var name = "BESSy.Proxy." + Path.GetFileNameWithoutExtension(ttw.Module.Name);

            lock (_syncRoot)
                if (!_assemblyBuilderCache.ContainsKey(name))
                    BuildDomainProxies(name);

            lock (_syncRoot)
                if (_typeCache.ContainsKey(ttw.AssemblyQualifiedName))
                    return _typeCache[ttw.AssemblyQualifiedName];
                else
                    throw new ProxyCreationException(string.Format("Proxy not found for type of {0}, for assembly {1}", type.FullName, type.Assembly.FullName));
        }

        protected T GetProxyFor<T>(IPocoRelationalDatabase<IdType, EntityType> repository, T instance) where T : EntityType
        {
            if (instance == null)
                return default(T);

            if (instance is IBESSyProxy<IdType, EntityType>)
                return instance;

            var inType = instance.GetType();
            var typeName = inType.AssemblyQualifiedName;

            while (inType.Assembly.IsDynamic)
            {
                if (inType.BaseType == null)
                    throw new ProxyCreationException(string.Format("Unable to create proxy of another proxy: {0}", instance.GetType()));

                inType = inType.BaseType;
                typeName = inType.AssemblyQualifiedName;
            }

            var name = "BESSy.Proxy." + Path.GetFileNameWithoutExtension(inType.Module.Name);

            lock (_syncRoot)
                if (!_assemblyBuilderCache.ContainsKey(name))
                    BuildDomainProxies(name);

            lock (_syncRoot)
                if (_typeCache.ContainsKey(typeName))
                    return (T)Activator.CreateInstance(_typeCache[typeName], repository, this);
                else
                    throw new ProxyCreationException(string.Format("Proxy not found for type of {0}, for assembly {1}", inType.FullName, inType.Assembly.FullName));
        }

        private bool IsEntityType(Type type)
        {
            if (type.IsArray)
                type = type.GetElementType();

            else if (type.IsGenericType && type.GetInterface("IEnumerable") != null && type.GetGenericArguments().Length == 1)
                type = type.GetGenericArguments()[0];

            if (type == entityType)
                return true;
            else if (type.BaseType == entityType)
                return true;
            else if (type.BaseType != null && type != type.BaseType && type.BaseType != typeof(ValueType) && type.BaseType != typeof(Object))
                return IsEntityType(type.BaseType);

            return false;
        }

        #region Proxy Builders

        protected AssemblyBuilder BuildDomainProxies(string name)
        {
            var assBuilder = GetAssemblyBuilder(name, entityType.Assembly);

            lock (_syncRoot)
                _assemblyBuilderCache.Add(name, assBuilder);

            ModuleBuilder moduleBuilder = null;

            if (useTransientAssembly)
                moduleBuilder = assBuilder.DefineDynamicModule(name);
            else
                moduleBuilder = assBuilder.DefineDynamicModule(name, name + ".dll", true);

            Type proxyType = null;

            foreach (var type in entityType.Assembly.GetTypes())
            {
                if (!IsEntityType(type) || type.IsAbstract || type.GetCustomAttributes(true).Any(a => a is BESSyIgnoreAttribute))
                    continue;

                proxyType = BuildProxyForType(type, moduleBuilder);

                lock (_syncRoot)
                {
                    _typeCache.Add(type.AssemblyQualifiedName, proxyType);
                    _typeCache.Add(proxyType.AssemblyQualifiedName, proxyType);
                }
            }

            try
            {
                if (!useTransientAssembly)
                    assBuilder.Save(name + ".dll");
            }
            catch (Exception) { Trace.TraceError("Couldn't save assembly file"); }

            return assBuilder;
        }

        protected Type BuildProxyForType(Type instanceType, ModuleBuilder moduleBuilder)
        {
            var originalType = instanceType;

            while (instanceType.Assembly.IsDynamic)
            {
                if (instanceType.BaseType != null)
                    instanceType = instanceType.BaseType;
                else
                    throw new ProxyCreationException(string.Format("Unable to create proxy of another proxy: {0}", instanceType));
            }

            var aqn = instanceType.AssemblyQualifiedName;
            var sn = instanceType.Name;

            //we can't override dictionaries
            var propOverrides = instanceType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => IsEntityType(p.PropertyType)).ToList();

            var typeName = moduleBuilder.ScopeName + "." + originalType.Name + "BESSyProxy";

            var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Public);

            typeBuilder.AddInterfaceImplementation(typeof(IBESSyProxy<IdType, EntityType>));
            typeBuilder.SetParent(instanceType);

            //var siteContainer = GetSiteContainer(typeName, typeBuilder, out shallowSiteContainerFields);

            MethodBuilder getFactory = null;
            MethodBuilder setFactory = null;

            MethodBuilder getOldId = null;
            MethodBuilder setOldId = null;

            MethodBuilder getIds = null;
            MethodBuilder setIds = null;

            MethodBuilder getRepo = null;
            MethodBuilder setRepo = null;

            var cascadeProp = BuildProperty(typeBuilder, "Bessy_OnCascade_Delete", typeof(bool));
            var repoProp = BuildProperty(typeBuilder, "Bessy_Proxy_Repository", typeof(IPocoRelationalDatabase<IdType, EntityType>), out getRepo, out setRepo);
            var factoryProp = BuildProperty(typeBuilder, "Bessy_Proxy_Factory", typeof(IProxyFactory<IdType, EntityType>), out getFactory, out setFactory);
            var idProp = BuildProperty(typeBuilder, "Bessy_Proxy_RelationshipIds", typeof(IDictionary<string, IdType[]>), out getIds, out setIds);
            var oldIdProp = BuildProperty(typeBuilder, "Bessy_Proxy_OldIdHash", typeof(string), out getOldId, out setOldId);

            repoProp.SetCustomAttribute(cabJsonIgnore);
            factoryProp.SetCustomAttribute(cabJsonIgnore);
            idProp.SetCustomAttribute(cabJsonPropertyIds);
            oldIdProp.SetCustomAttribute(cabJsonPropertyOldId);

            var gets = new List<MethodBuilder>();
            var sets = new List<MethodBuilder>();

            foreach (var p in propOverrides.Where(p => p.PropertyType.GetInterface("IEnumerable") == null))
                BuildProperty(typeBuilder, factoryProp, p, gets, sets);

            foreach (var p in propOverrides.Where(p => p.PropertyType.GetInterface("IEnumerable") != null))
                BuildEnumerableProperty(typeBuilder, factoryProp, p, gets, sets);

            //var jCopyMethod = BuildJObjectConstructor()
            var defaultCtor = BuildDefaultConstructor(typeBuilder, entityType, setIds);
            var initCtor = BuildInitializeConstructor(typeBuilder, defaultCtor, setFactory, setRepo);

            var shallowCopyMethod = BuildShallowCopy(typeBuilder, propOverrides, sets, originalType, getFactory, setOldId);
            var deepCopyMethod = BuildDeepCopy(typeBuilder, propOverrides, sets, originalType, getFactory, setOldId);
            var jCopyMethod = BuildJCopyFrom(typeBuilder, propOverrides, originalType, setIds, getRepo);

            var type = typeBuilder.CreateType();

            return type;
        }

        [SecuritySafeCritical]
        private AssemblyBuilder GetAssemblyBuilder(string assemblyName, Assembly evidenceAssembly)
        {
            AssemblyBuilder assBuilder = null;

            lock (_syncRoot)
            {
                if (_assemblyBuilderCache.ContainsKey(assemblyName))
                    return _assemblyBuilderCache[assemblyName];
            }

#if MONO
            var bessyAss = Assembly.Load("BESSy");
#endif
#if NET40
            var bessyAss = Assembly.Load("BESSy");
#endif
#if NET45
            var bessyAss = Assembly.Load("BESSy_45");
#endif
#if NET451
            var bessyAss = Assembly.Load("BESSy_451");
#endif

            if (bessyAss == null)
                throw new ProxyCreationException("You're missing something bro.");

            var bessyName = bessyAss.GetName();

            var name = new AssemblyName(assemblyName);
            name.Version = new Version(1, 0, 0, 0);
            name.VersionCompatibility = System.Configuration.Assemblies.AssemblyVersionCompatibility.SameMachine;
            name.Flags = bessyName.Flags;
            name.ProcessorArchitecture = bessyName.ProcessorArchitecture;
            name.CultureInfo = bessyName.CultureInfo;

            AppDomain domain = AppDomain.CurrentDomain;

            if (useTransientAssembly)
                assBuilder = domain.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run, null, SecurityContextSource.CurrentAppDomain);
            else
                assBuilder = domain.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndSave, null, SecurityContextSource.CurrentAppDomain);

            assBuilder.DefineVersionInfoResource(bessyName.FullName, bessyName.Version.ToString(), "", "", "");

            assBuilder.SetCustomAttribute(cabPartialTrust);

            return assBuilder;
        }

        protected PropertyBuilder BuildProperty(TypeBuilder tBuilder, string name, Type propertyType)
        {
            PropertyBuilder pBuilder = tBuilder.DefineProperty(name, PropertyAttributes.None, CallingConventions.HasThis, propertyType, new Type[0]);
            var backing = tBuilder.DefineField("<" + name + ">k_BackingField", propertyType, FieldAttributes.Private);

            var getter = BuildGetter(tBuilder, name, propertyType, backing);
            var setter = BuildSetter(tBuilder, name, propertyType, backing);

            pBuilder.SetGetMethod(getter);
            pBuilder.SetSetMethod(setter);

            return pBuilder;
        }

        protected PropertyBuilder BuildProperty(TypeBuilder tBuilder, string name, Type propertyType, out MethodBuilder getter, out MethodBuilder setter)
        {
            PropertyBuilder pBuilder = tBuilder.DefineProperty(name, PropertyAttributes.None, CallingConventions.HasThis, propertyType, new Type[0]);
            var backing = tBuilder.DefineField("<" + name + ">k_BackingField", propertyType, FieldAttributes.Private);

            getter = BuildGetter(tBuilder, name, propertyType, backing);
            setter = BuildSetter(tBuilder, name, propertyType, backing);

            pBuilder.SetGetMethod(getter);
            pBuilder.SetSetMethod(setter);

            return pBuilder;
        }

        protected MethodBuilder BuildGetter(TypeBuilder tBuilder, string name, Type propertyType, FieldBuilder backingField)
        {
            // Declaring method builder
            // Method attributes
            System.Reflection.MethodAttributes methodAttributes =
                  System.Reflection.MethodAttributes.Public
                | System.Reflection.MethodAttributes.Virtual
                | System.Reflection.MethodAttributes.Final
                | System.Reflection.MethodAttributes.HideBySig
                | System.Reflection.MethodAttributes.NewSlot;

            MethodBuilder method = tBuilder.DefineMethod("get_" + name, methodAttributes, CallingConventions.HasThis, propertyType, new Type[0]);
            ILGenerator Generator = method.GetILGenerator();

            Generator.Emit(OpCodes.Ldarg_0);
            Generator.Emit(OpCodes.Ldfld, backingField);
            Generator.Emit(OpCodes.Ret);

            return method;
        }

        protected MethodBuilder BuildSetter(TypeBuilder tBuilder, string name, Type propertyType, FieldBuilder backingField)
        {
            // Declaring method builder
            // Method attributes
            System.Reflection.MethodAttributes methodAttributes =
                  System.Reflection.MethodAttributes.Public
                | System.Reflection.MethodAttributes.Virtual
                | System.Reflection.MethodAttributes.Final
                | System.Reflection.MethodAttributes.HideBySig
                | System.Reflection.MethodAttributes.NewSlot;

            MethodBuilder method = tBuilder.DefineMethod("set_" + name, methodAttributes, CallingConventions.HasThis, typeof(void), new Type[] { propertyType });

            ParameterBuilder value = method.DefineParameter(1, ParameterAttributes.None, "value");
            ILGenerator gen = method.GetILGenerator();

            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Stfld, backingField);
            gen.Emit(OpCodes.Ret);

            return method;
        }

        protected PropertyBuilder BuildProperty(TypeBuilder tBuilder, PropertyBuilder factoryMethod, PropertyInfo propInfo, List<MethodBuilder> gets, List<MethodBuilder> sets)
        {
            var pType = propInfo.PropertyType;

            while (pType.Assembly.IsDynamic)
            {
                if (pType.BaseType != null)
                    pType = pType.BaseType;
                else
                    throw new ProxyCreationException(string.Format("Unable to create proxy of another proxy: {0}", propInfo.PropertyType));
            }

            var pb = tBuilder.DefineProperty(propInfo.Name, PropertyAttributes.None, CallingConventions.HasThis, pType, new Type[0]);

            pb.SetCustomAttribute(cabJsonIgnore);

            MethodBuilder get = null;
            MethodBuilder set = null;

            if (propInfo.CanRead && propInfo.CanWrite)
            {
                get = BuildGetter(tBuilder, factoryMethod, propInfo, pType);
                set = BuildSetter(tBuilder, factoryMethod, propInfo, pType);

                gets.Add(get);
                sets.Add(set);

                pb.SetGetMethod(get);
                pb.SetSetMethod(set);
            }
            else if (propInfo.CanRead)
            {
                get = BuildReadonlyGetter(tBuilder, factoryMethod, propInfo, pType);

                gets.Add(get);

                pb.SetGetMethod(get);
            }


            return pb;
        }

        private MethodBuilder BuildReadonlyGetter(TypeBuilder tBuilder, PropertyBuilder factoryMethod, PropertyInfo propInfo, Type pType)
        {
            MethodInfo baseGet = propInfo.GetGetMethod();

            if (!baseGet.IsVirtual)
                throw new ProxyCreationException("Proxied properties must be marked virtual.");

            MethodInfo factoryGet = factoryMethod.GetGetMethod();

            MethodBuilder method = tBuilder.DefineMethod("get_" + propInfo.Name, baseGet.Attributes | System.Reflection.MethodAttributes.Virtual, CallingConventions.HasThis, pType, new Type[0]);
            tBuilder.DefineMethodOverride(method, baseGet);

            ILGenerator gen = method.GetILGenerator();

            LocalBuilder CS10000 = gen.DeclareLocal(pType);

            Label label10 = gen.DefineLabel();
            // Writing body
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Call, baseGet);
            gen.Emit(OpCodes.Stloc_0);
            gen.Emit(OpCodes.Br_S, label10);
            gen.MarkLabel(label10);
            gen.Emit(OpCodes.Ldloc_0);
            gen.Emit(OpCodes.Ret);
            // finished
            return method;
        }

        protected MethodBuilder BuildGetter(TypeBuilder tBuilder, PropertyBuilder factoryMethod, PropertyInfo propInfo, Type pType)
        {
            MethodInfo baseGet = propInfo.GetGetMethod();
            MethodInfo baseSet = propInfo.GetSetMethod();

            if (!baseGet.IsVirtual || !baseSet.IsVirtual)
                throw new ProxyCreationException("Proxied properties must be marked virtual.");

            MethodInfo factoryGet = factoryMethod.GetGetMethod();

            MethodBuilder method = tBuilder.DefineMethod("get_" + propInfo.Name, baseGet.Attributes, CallingConventions.HasThis, pType, new Type[0]);
           tBuilder.DefineMethodOverride(method, baseGet);

            ILGenerator gen = method.GetILGenerator();

            LocalBuilder CS10000 = gen.DeclareLocal(pType);

            Label label33 = gen.DefineLabel();

            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldstr, propInfo.Name);
            gen.Emit(OpCodes.Call, getRelatedEntity);
            gen.Emit(OpCodes.Isinst, pType);
            gen.Emit(OpCodes.Call, baseSet);
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Call, baseGet);
            gen.Emit(OpCodes.Stloc_0);
            gen.Emit(OpCodes.Br_S, label33);
            gen.MarkLabel(label33);
            gen.Emit(OpCodes.Ldloc_0);
            gen.Emit(OpCodes.Ret);
            // finished
            return method;
        }

        protected MethodBuilder BuildSetter(TypeBuilder tBuilder, PropertyBuilder factoryMethod, PropertyInfo propInfo, Type pType)
        {
            MethodInfo baseGet = propInfo.GetGetMethod();
            MethodInfo baseSet = propInfo.GetSetMethod();

            if (!baseGet.IsVirtual || !baseSet.IsVirtual)
                throw new ProxyCreationException("Proxied properties must be marked virtual.");

            MethodInfo factoryGet = factoryMethod.GetGetMethod();

            MethodBuilder method = tBuilder.DefineMethod("set_" + propInfo.Name, baseSet.Attributes, CallingConventions.HasThis, typeof(void), new Type[] { pType });
            tBuilder.DefineMethodOverride(method, baseSet);

            // Setting return type
            method.SetReturnType(typeof(void));

            // Parameter value
            ParameterBuilder value = method.DefineParameter(1, ParameterAttributes.None, "value");

            ILGenerator gen = method.GetILGenerator();

            // Writing body
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Call, baseSet);
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldstr, propInfo.Name);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Call, setRelatedEntity);
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ret);

            return method;
        }

        protected PropertyBuilder BuildEnumerableProperty(TypeBuilder tBuilder, PropertyBuilder factoryMethod, PropertyInfo propInfo, List<MethodBuilder> gets, List<MethodBuilder> sets)
        {
            var pType = propInfo.PropertyType;

            while (pType.Assembly.IsDynamic)
            {
                if (pType.BaseType != null)
                    pType = pType.BaseType;
                else
                    throw new ProxyCreationException(string.Format("Unable to create proxy of another proxy: {0}", propInfo.PropertyType));
            }

            var pb = tBuilder.DefineProperty(propInfo.Name, PropertyAttributes.None, CallingConventions.HasThis, pType, new Type[0]);
            pb.SetCustomAttribute(cabJsonIgnore);

            Type innerType = null;

            if (propInfo.PropertyType.IsArray)
                innerType = propInfo.PropertyType.GetElementType();
            else
                innerType = propInfo.PropertyType.GetGenericArguments().First();

            if (propInfo.CanRead && propInfo.CanWrite)
            {
                var get = BuildEnumerableGetter(tBuilder, factoryMethod, innerType, propInfo);
                var set = BuildEnumerableSetter(tBuilder, factoryMethod, innerType, propInfo);

                gets.Add(get);
                sets.Add(set);

                pb.SetGetMethod(get);
                pb.SetSetMethod(set);
            }
            else if (propInfo.CanRead)
            {
                var get = BuildEnumerableReadonlyGetter(tBuilder, factoryMethod, innerType, propInfo);

                gets.Add(get);

                pb.SetGetMethod(get);
            }

            return pb;
        }

        private MethodBuilder BuildEnumerableReadonlyGetter(TypeBuilder tBuilder, PropertyBuilder factoryMethod, Type innerType, PropertyInfo propInfo)
        {
            MethodInfo baseGet = propInfo.GetGetMethod();

            if (!baseGet.IsVirtual)
                throw new ProxyCreationException("Proxied properties must be marked virtual.");

            MethodInfo factoryGet = factoryMethod.GetGetMethod();

            MethodInfo cast = typeof(Enumerable).GetMethod("Cast").MakeGenericMethod(innerType);
            //,
            //BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            //null, new Type[] { typeof(IEnumerable<>).MakeGenericType(innerType) }, null);

            MethodInfo toEnumerable = null;

            if (propInfo.PropertyType.IsArray)
                toEnumerable = typeof(Enumerable).GetMethod("ToArray").MakeGenericMethod(innerType);
            else
                toEnumerable = typeof(Enumerable).GetMethod("ToList").MakeGenericMethod(innerType);

            MethodBuilder method = tBuilder.DefineMethod("get_" + propInfo.Name, baseGet.Attributes, CallingConventions.HasThis, propInfo.PropertyType, new Type[0]);
            tBuilder.DefineMethodOverride(method, baseGet);

            ILGenerator gen = method.GetILGenerator();

            LocalBuilder CS10000 = gen.DeclareLocal(propInfo.PropertyType);

            Label label10 = gen.DefineLabel();
            // Writing body
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Call, baseGet);
            gen.Emit(OpCodes.Stloc_0);
            gen.Emit(OpCodes.Br_S, label10);
            gen.MarkLabel(label10);
            gen.Emit(OpCodes.Ldloc_0);
            gen.Emit(OpCodes.Ret);
            // finished
            return method;
        }

        protected MethodBuilder BuildEnumerableGetter(TypeBuilder tBuilder, PropertyBuilder factoryMethod, Type innerType, PropertyInfo propInfo)
        {
            MethodInfo baseGet = propInfo.GetGetMethod();
            MethodInfo baseSet = propInfo.GetSetMethod();

            if (!baseGet.IsVirtual || !baseSet.IsVirtual)
                throw new ProxyCreationException("Proxied properties must be marked virtual.");

            MethodInfo factoryGet = factoryMethod.GetGetMethod();

            MethodInfo cast = typeof(Enumerable).GetMethod("Cast").MakeGenericMethod(innerType);
                //,
                //BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                //null, new Type[] { typeof(IEnumerable<>).MakeGenericType(innerType) }, null);

            MethodInfo toEnumerable = null;

            if (propInfo.PropertyType.IsArray)
                toEnumerable = typeof(Enumerable).GetMethod("ToArray").MakeGenericMethod(innerType);
            else
                toEnumerable = typeof(Enumerable).GetMethod("ToList").MakeGenericMethod(innerType);

            MethodBuilder method = tBuilder.DefineMethod("get_" + propInfo.Name, baseGet.Attributes, CallingConventions.HasThis, propInfo.PropertyType, new Type[0]);
            tBuilder.DefineMethodOverride(method, baseGet);

            ILGenerator gen = method.GetILGenerator();

            LocalBuilder lb100000 = gen.DeclareLocal(propInfo.PropertyType);
            var label38 = gen.DefineLabel();

            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldstr, propInfo.Name);
            gen.Emit(OpCodes.Call, getRelatedEntities);
            gen.Emit(OpCodes.Call, cast);
            gen.Emit(OpCodes.Call, toEnumerable);
            gen.Emit(OpCodes.Call, baseSet);
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Call, baseGet);
            gen.Emit(OpCodes.Stloc_0);
            gen.Emit(OpCodes.Br_S, label38);
            gen.MarkLabel(label38);
            gen.Emit(OpCodes.Ldloc_0);
            gen.Emit(OpCodes.Ret);

            return method;
        }

        protected MethodBuilder BuildEnumerableSetter(TypeBuilder tBuilder, PropertyBuilder factoryMethod, Type innerType, PropertyInfo propInfo)
        {
            MethodInfo baseGet = propInfo.GetGetMethod();
            MethodInfo baseSet = propInfo.GetSetMethod();

            if (!baseGet.IsVirtual || !baseSet.IsVirtual)
                throw new ProxyCreationException("Proxied properties must be marked virtual.");

            MethodInfo factoryGet = factoryMethod.GetGetMethod();

            MethodBuilder method = tBuilder.DefineMethod("set_" + propInfo.Name, baseSet.Attributes, CallingConventions.HasThis, typeof(void), new Type[] { propInfo.PropertyType });
            tBuilder.DefineMethodOverride(method, baseSet);

            MethodInfo cast = typeof(Enumerable).GetMethod(
                "Cast",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, new Type[] { typeof(IEnumerable<>).MakeGenericType(innerType) }, null);

            MethodInfo toEnumerable = null;

            if (propInfo.PropertyType.IsArray)
                toEnumerable = typeof(Enumerable).GetMethod("ToArray").MakeGenericMethod(innerType);
            else
                toEnumerable = typeof(Enumerable).GetMethod("ToList").MakeGenericMethod(innerType);

            // Setting return type
            method.SetReturnType(typeof(void));

            // Parameter value
            ParameterBuilder value = method.DefineParameter(1, ParameterAttributes.None, "value");

            ILGenerator gen = method.GetILGenerator();

            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Call, baseSet);
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldstr, propInfo.Name);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Call, setRelatedEntities);
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ret);

            // finished
            return method;
        }

        protected ConstructorBuilder BuildDefaultConstructor(TypeBuilder tBuilder, Type baseType, MethodInfo setIds)
        {
            System.Reflection.MethodAttributes methodAttributes =
                  System.Reflection.MethodAttributes.Public;

            ConstructorBuilder ctorThis = tBuilder.DefineConstructor(methodAttributes, CallingConventions.Standard, new Type[0]);

            // Preparing Reflection instances
            ConstructorInfo ctor1 = tBuilder.BaseType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { }, null);

            ILGenerator gen = ctorThis.GetILGenerator();

            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Call, ctor1);
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Newobj, ciIdsBuilder);
            gen.Emit(OpCodes.Callvirt, setIds);
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ret);

            return ctorThis;
        }

        protected MethodBuilder BuildInitializeConstructor(TypeBuilder tBuilder, ConstructorBuilder defaultCtor, MethodInfo setFactory, MethodInfo setRepo)
        {
            System.Reflection.MethodAttributes methodAttributes =
              System.Reflection.MethodAttributes.Public
            | System.Reflection.MethodAttributes.HideBySig;

            MethodBuilder method = tBuilder.DefineMethod(".ctor", methodAttributes);

            method.SetReturnType(typeof(void));

            method.SetParameters
                (typeof(BESSy.Relational.IPocoRelationalDatabase<IdType, EntityType>),
                typeof(BESSy.Relational.IProxyFactory<IdType, EntityType>));

            ParameterBuilder repository = method.DefineParameter(1, ParameterAttributes.None, "repository");
            ParameterBuilder factory = method.DefineParameter(2, ParameterAttributes.None, "factory");

            ILGenerator gen = method.GetILGenerator();

            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Call, defaultCtor);
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Call, setRepo);
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_2);
            gen.Emit(OpCodes.Call, setFactory);
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ret);

            return method;
        }

        private MethodBuilder BuildShallowCopy(TypeBuilder tBuilder, List<PropertyInfo> propOverrides, List<MethodBuilder> sets, Type instanceType, MethodBuilder getFactory, MethodBuilder setOldId)
        {
            var shallow = tBuilder.DefineMethod("Bessy_Proxy_Shallow_Copy_From",
                System.Reflection.MethodAttributes.Public |
                System.Reflection.MethodAttributes.Virtual |
                System.Reflection.MethodAttributes.Final |
                System.Reflection.MethodAttributes.HideBySig |
                System.Reflection.MethodAttributes.NewSlot,
                typeof(void), new Type[] { entityType });

            // Parameter entity
            ParameterBuilder entity = shallow.DefineParameter(1, ParameterAttributes.None, "entity");

            var fields = instanceType.GetFields
                (System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic)
                .Where(f => f.Name != IdToken && !f.IsInitOnly)
                .ToList();

            var pFields = fields.Where(f => !f.IsPublic && (f.FieldType.IsValueType || f.FieldType == typeof(string) || f.FieldType.IsArray || f.FieldType.IsClass));

            ILGenerator generator = shallow.GetILGenerator();

            // Preparing locals
            LocalBuilder instance = generator.DeclareLocal(instanceType);
            LocalBuilder strArray = generator.DeclareLocal(typeof(String[]));
            LocalBuilder flag = generator.DeclareLocal(typeof(Boolean));
            LocalBuilder infoArray = generator.DeclareLocal(typeof(CSharpArgumentInfo[]));
            LocalBuilder str = generator.DeclareLocal(typeof(IdType));

            Label gtfo = generator.DefineLabel();
            Label ok = generator.DefineLabel();

            generator.Emit(OpCodes.Nop);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Isinst, instanceType);
            generator.Emit(OpCodes.Stloc_0);
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldnull);
            generator.Emit(OpCodes.Ceq);
            generator.Emit(OpCodes.Ldc_I4_0);
            generator.Emit(OpCodes.Ceq);
            generator.Emit(OpCodes.Stloc_3);
            generator.Emit(OpCodes.Ldloc_3);
            generator.Emit(OpCodes.Brtrue_S, ok);
            generator.Emit(OpCodes.Br, gtfo);
            generator.MarkLabel(ok);

            if (pFields != null && pFields.Count() > 0)
            {
                generator.Emit(OpCodes.Ldc_I4, pFields.Count());
                generator.Emit(OpCodes.Newarr, typeof(String));
                generator.Emit(OpCodes.Stloc_3);
                generator.Emit(OpCodes.Ldloc_3);


                int count = 0;
                foreach (var field in pFields)
                {
                    generator.Emit(OpCodes.Ldc_I4,  count);
                    generator.Emit(OpCodes.Ldstr, field.Name);
                    generator.Emit(OpCodes.Stelem_Ref);
                    generator.Emit(OpCodes.Ldloc_3);

                    count++;
                }

                generator.Emit(OpCodes.Stloc_1);
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Call, getType);
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Callvirt, getType);
                generator.Emit(OpCodes.Ldloc_1);
                generator.Emit(OpCodes.Call, copyFields);

                generator.Emit(OpCodes.Nop);
            }

            foreach (var field in fields.Where(f => f.IsPublic))
                CopyField(tBuilder, instanceType, generator, field);

            var properties = instanceType.GetProperties(BindingFlags.Instance | BindingFlags.Public).ToList();

            foreach (var prop in properties.Where(p => !propOverrides.Any(o => o.Name == p.Name)))
                CopyProperty(generator, prop, prop.GetSetMethod());

            generator.Emit(OpCodes.Nop);
            generator.Emit(OpCodes.Nop);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldstr, tBuilder.BaseType.FullName);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Call, getFactory);
            generator.Emit(OpCodes.Callvirt, getIdFromFactory);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Callvirt, getIdInvoke);
            generator.Emit(OpCodes.Stloc_S, 4);
            generator.Emit(OpCodes.Ldloca_S, 4);
            generator.Emit(OpCodes.Call, idToString);
            generator.Emit(OpCodes.Call, stringConcat);
            generator.Emit(OpCodes.Call, setOldId);

            generator.Emit(OpCodes.Nop);
            generator.MarkLabel(gtfo);
            generator.Emit(OpCodes.Ret);

            return shallow;
        }

        protected MethodBuilder BuildDeepCopy(TypeBuilder tBuilder, IEnumerable<PropertyInfo> propOverrides, List<MethodBuilder> sets, Type instanceType, MethodBuilder getFactory, MethodBuilder setOldId)
        {
            var deep = tBuilder.DefineMethod("Bessy_Proxy_Deep_Copy_From",
                System.Reflection.MethodAttributes.Public |
                System.Reflection.MethodAttributes.Virtual |
                System.Reflection.MethodAttributes.Final |
                System.Reflection.MethodAttributes.HideBySig |
                System.Reflection.MethodAttributes.NewSlot,
                typeof(void), new Type[] { entityType });

            // Parameter entity
            ParameterBuilder entity = deep.DefineParameter(1, ParameterAttributes.None, "entity");

            ILGenerator generator = deep.GetILGenerator();

            // Preparing locals
            LocalBuilder instance = generator.DeclareLocal(instanceType);
            LocalBuilder local1 = generator.DeclareLocal(typeof(Boolean));

            Label gtfo = generator.DefineLabel();
            Label ok = generator.DefineLabel();

            generator.Emit(OpCodes.Nop);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Isinst, instanceType);
            generator.Emit(OpCodes.Stloc_0);
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldnull);
            generator.Emit(OpCodes.Ceq);
            generator.Emit(OpCodes.Ldc_I4_0);
            generator.Emit(OpCodes.Ceq);
            generator.Emit(OpCodes.Stloc_1);
            generator.Emit(OpCodes.Ldloc_1);
            generator.Emit(OpCodes.Brtrue_S, ok);
            generator.Emit(OpCodes.Br, gtfo);
            generator.MarkLabel(ok);

            foreach (var prop in propOverrides)
            {
                var setter = sets.FirstOrDefault(s => s.Name.Substring(4, s.Name.Length - 4) == prop.Name);

                if (setter == null)
                {
                    Trace.TraceError(string.Format("Overriden property could not find corresponding set method for property: {0}", prop.Name));
                    continue;
                }

                CopyProperty(generator, prop, setter);
            }

            generator.Emit(OpCodes.Nop);
            generator.MarkLabel(gtfo);
            generator.Emit(OpCodes.Ret);

            return deep;
        }

        protected MethodBuilder BuildJCopyFrom(TypeBuilder tBuilder, IEnumerable<PropertyInfo> propOverrides, Type instanceType, MethodBuilder setIds, MethodBuilder getRepo)
        {
            var jCopy = tBuilder.DefineMethod("Bessy_Proxy_JCopy_From",
                System.Reflection.MethodAttributes.Public |
                System.Reflection.MethodAttributes.Virtual |
                System.Reflection.MethodAttributes.Final |
                System.Reflection.MethodAttributes.HideBySig |
                System.Reflection.MethodAttributes.NewSlot,
                typeof(void), new Type[] { typeof(JObject) });

            // Parameter entity
            ParameterBuilder entity = jCopy.DefineParameter(1, ParameterAttributes.None, "obj");

            ILGenerator generator = jCopy.GetILGenerator();

            var fields = instanceType.GetFields
                (System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic)
                .Where(f => f.Name != IdToken && !f.IsInitOnly)
                .ToList();

            var pFields = fields.Where(f => !f.IsPublic && (f.FieldType.IsValueType || f.FieldType == typeof(string) || f.FieldType.IsArray || f.FieldType.IsClass)).ToList();

            LocalBuilder fieldNames = generator.DeclareLocal(typeof(String[]));
            LocalBuilder token = generator.DeclareLocal(typeof(JToken));
            LocalBuilder flag = generator.DeclareLocal(typeof(Boolean));
            LocalBuilder tokenNames = generator.DeclareLocal(typeof(String[]));
            LocalBuilder tmpArray = generator.DeclareLocal(typeof(String[]));

            // Preparing labels
            Label nullCheckOk = generator.DefineLabel();
            Label gtfo = generator.DefineLabel();

            generator.Emit(OpCodes.Nop);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldnull);
            generator.Emit(OpCodes.Ceq);
            generator.Emit(OpCodes.Ldc_I4_0);
            generator.Emit(OpCodes.Ceq);
            generator.Emit(OpCodes.Stloc_2);
            generator.Emit(OpCodes.Ldloc_2);
            generator.Emit(OpCodes.Brtrue, nullCheckOk);
            generator.Emit(OpCodes.Br, gtfo);
            generator.MarkLabel(nullCheckOk);

            if (pFields != null && pFields.Count > 0)
            {
                generator.Emit(OpCodes.Nop);

                generator.Emit(OpCodes.Ldc_I4, pFields.Count);
                generator.Emit(OpCodes.Newarr, typeof(System.String));
                generator.Emit(OpCodes.Stloc, tmpArray);
                generator.Emit(OpCodes.Ldloc, tmpArray);

                for (var i = 0; i < pFields.Count; i++)
                {
                    generator.Emit(OpCodes.Ldc_I4, i);
                    generator.Emit(OpCodes.Ldstr, pFields[i].Name);
                    generator.Emit(OpCodes.Stelem_Ref);
                    generator.Emit(OpCodes.Ldloc, tmpArray);
                }

                generator.Emit(OpCodes.Stloc_0);

                generator.Emit(OpCodes.Nop);

                generator.Emit(OpCodes.Ldc_I4, pFields.Count);
                generator.Emit(OpCodes.Newarr, typeof(string));
                generator.Emit(OpCodes.Stloc, tmpArray);
                generator.Emit(OpCodes.Ldloc, tmpArray);

                for (var i = 0; i < pFields.Count; i++)
                {
                    var fieldName = pFields[i].Name;
                    var jprop = pFields[i].GetCustomAttributes(true)
                        .FirstOrDefault(a => a is JsonPropertyAttribute) as JsonPropertyAttribute;

                    if (jprop != null && !string.IsNullOrWhiteSpace(jprop.PropertyName))
                        fieldName = jprop.PropertyName;

                    generator.Emit(OpCodes.Ldc_I4, i);
                    generator.Emit(OpCodes.Ldstr, fieldName);
                    generator.Emit(OpCodes.Stelem_Ref);
                    generator.Emit(OpCodes.Ldloc, tmpArray);
                }

                generator.Emit(OpCodes.Stloc_3);

                generator.Emit(OpCodes.Nop);
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldarg_1);
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Call, getType);
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Ldloc_3);
                generator.Emit(OpCodes.Call, copyJFields);

                generator.Emit(OpCodes.Nop);
            }

            foreach (var field in fields.Where(f => f.IsPublic))
                CopyJToken(tBuilder, generator, field, getRepo);

            var properties = instanceType.GetProperties(BindingFlags.Instance | BindingFlags.Public).ToList();

            foreach (var prop in properties.Where(p => !propOverrides.Any(o => o.Name == p.Name)))
                CopyJToken(generator, prop, prop.GetSetMethod(), getRepo);

            CopyJToken(generator, "$relationshipIds", typeof(Dictionary<string, IdType[]>), setIds, getRepo);

            generator.MarkLabel(gtfo);
            generator.Emit(OpCodes.Nop);
            generator.Emit(OpCodes.Ret);

            return jCopy;
        }

        private void CopyProperty(ILGenerator generator, PropertyInfo prop, MethodInfo setter)
        {
            var getter = prop.GetGetMethod();

            if (getter == null || setter == null)
                return;

            if (prop.PropertyType.IsArray)
            {
                var innerType = prop.PropertyType.GetElementType();

                MethodInfo toArray = typeof(Enumerable).GetMethod("ToArray").MakeGenericMethod(innerType);

                Label gtfo = generator.DefineLabel();

                generator.Emit(OpCodes.Ldarg_1);
                generator.Emit(OpCodes.Callvirt, getter);
                generator.Emit(OpCodes.Ldnull);
                generator.Emit(OpCodes.Ceq);
                generator.Emit(OpCodes.Stloc_0);
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Brtrue_S, gtfo);
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldarg_1);
                generator.Emit(OpCodes.Callvirt, getter);
                generator.Emit(OpCodes.Call, toArray);
                generator.Emit(OpCodes.Callvirt, setter);
                generator.Emit(OpCodes.Nop);
                generator.MarkLabel(gtfo);
            }
            else if (prop.PropertyType.GetInterface("IEnumerable") != null
                && prop.PropertyType.GetGenericArguments().Length == 1)
            {
                var innerType = prop.PropertyType.GetGenericArguments().First();

                MethodInfo toList = typeof(Enumerable).GetMethod("ToList").MakeGenericMethod(innerType);

                Label gtfo = generator.DefineLabel();

                generator.Emit(OpCodes.Ldarg_1);
                generator.Emit(OpCodes.Callvirt, getter);
                generator.Emit(OpCodes.Ldnull);
                generator.Emit(OpCodes.Ceq);
                generator.Emit(OpCodes.Stloc_0);
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Brtrue_S, gtfo);
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldarg_1);
                generator.Emit(OpCodes.Callvirt, getter);
                generator.Emit(OpCodes.Call, toList);
                generator.Emit(OpCodes.Callvirt, setter);
                generator.Emit(OpCodes.Nop);
                generator.MarkLabel(gtfo);
            }
            else
            {
                generator.Emit(OpCodes.Nop);
                generator.Emit(OpCodes.Nop);
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldarg_1);
                generator.Emit(OpCodes.Callvirt, getter);
                generator.Emit(OpCodes.Callvirt, setter);
            }
        }

        private void CopyField(Type proxyType, Type instanceType, ILGenerator gen, FieldInfo field)
        {
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Ldfld, field);
            gen.Emit(OpCodes.Stfld, field);
        }

        private void CopyJToken(Type proxyType, ILGenerator generator, FieldInfo field, MethodInfo getRepo)
        {
            var fieldName = field.Name;

            var att = field.GetCustomAttributes(true);

            var jprop = att.FirstOrDefault(a => a is JsonPropertyAttribute) as JsonPropertyAttribute;

            if (jprop != null && !string.IsNullOrWhiteSpace(jprop.PropertyName))
                fieldName = jprop.PropertyName;

            var exitLabel = generator.DefineLabel();

            var toJObject = typeof(JToken).GetMethod("ToObject",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new Type[] { typeof(JsonSerializer) }, null).MakeGenericMethod(field.FieldType);

            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldstr, fieldName);
            generator.Emit(OpCodes.Ldloca_S, 1);
            generator.Emit(OpCodes.Callvirt, jTokenTryGetValue);
            generator.Emit(OpCodes.Ldc_I4_0);
            generator.Emit(OpCodes.Ceq);
            generator.Emit(OpCodes.Stloc_2);
            generator.Emit(OpCodes.Ldloc_2);
            generator.Emit(OpCodes.Brtrue_S, exitLabel);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldloc_1);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Call, getRepo);
            generator.Emit(OpCodes.Callvirt, getFormatter);
            generator.Emit(OpCodes.Callvirt, getSerializer);
            generator.Emit(OpCodes.Callvirt, toJObject);
            generator.Emit(OpCodes.Stfld, field);


            generator.Emit(OpCodes.Nop);
            generator.MarkLabel(exitLabel);
        }

        private void CopyJToken(ILGenerator generator, PropertyInfo prop, MethodInfo setter, MethodInfo getRepo)
        {
            var getter = prop.GetGetMethod();

            if (getter == null || setter == null)
                return;

            var fieldName = prop.Name;

            var att = prop.GetCustomAttributes(true);

            var jprop = att.FirstOrDefault(a => a is JsonPropertyAttribute) as JsonPropertyAttribute;

            if (jprop != null && !string.IsNullOrWhiteSpace(jprop.PropertyName))
                fieldName = jprop.PropertyName;

            var exitLabel = generator.DefineLabel();

            var toJObject = typeof(JToken).GetMethod("ToObject",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new Type[] { typeof(JsonSerializer) }, null).MakeGenericMethod(prop.PropertyType);

            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldstr, fieldName);
            generator.Emit(OpCodes.Ldloca_S, 1);
            generator.Emit(OpCodes.Callvirt, jTokenTryGetValue);
            generator.Emit(OpCodes.Ldc_I4_0);
            generator.Emit(OpCodes.Ceq);
            generator.Emit(OpCodes.Stloc_2);
            generator.Emit(OpCodes.Ldloc_2);
            generator.Emit(OpCodes.Brtrue_S, exitLabel);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldloc_1);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Call, getRepo);
            generator.Emit(OpCodes.Callvirt, getFormatter);
            generator.Emit(OpCodes.Callvirt, getSerializer);
            generator.Emit(OpCodes.Callvirt, toJObject);
            generator.Emit(OpCodes.Call, setter);

            generator.Emit(OpCodes.Nop);
            generator.MarkLabel(exitLabel);
        }

        private void CopyJToken(ILGenerator generator, string tokenName, Type type, MethodInfo setter, MethodInfo getRepo)
        {
            if (setter == null)
                return;

            var exitLabel = generator.DefineLabel();

            var toJObject = typeof(JToken).GetMethod("ToObject",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new Type[] { typeof(JsonSerializer) }, null).MakeGenericMethod(type);

            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldstr, tokenName);
            generator.Emit(OpCodes.Ldloca_S, 1);
            generator.Emit(OpCodes.Callvirt, jTokenTryGetValue);
            generator.Emit(OpCodes.Ldc_I4_0);
            generator.Emit(OpCodes.Ceq);
            generator.Emit(OpCodes.Stloc_2);
            generator.Emit(OpCodes.Ldloc_2);
            generator.Emit(OpCodes.Brtrue_S, exitLabel);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldloc_1);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Call, getRepo);
            generator.Emit(OpCodes.Callvirt, getFormatter);
            generator.Emit(OpCodes.Callvirt, getSerializer);
            generator.Emit(OpCodes.Callvirt, toJObject);
            generator.Emit(OpCodes.Call, setter);

            generator.Emit(OpCodes.Nop);
            generator.MarkLabel(exitLabel);
        }

        #endregion

        public void Dispose()
        {

        }
    }
}
