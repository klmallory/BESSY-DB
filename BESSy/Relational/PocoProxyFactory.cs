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
        bool Bessy_OnCascade_Delete { get; set; }
        IPocoRelationalDatabase<IdType, EntityType> Bessy_Proxy_Repository { get; set; }
        IDictionary<string, IdType[]> Bessy_Proxy_RelationshipIds { get; set; }
        IProxyFactory<IdType, EntityType> Bessy_Proxy_Factory { get; set; }
        IdType Bessy_Proxy_OldId { get; set; }
        string Bessy_Proxy_Simple_Type_Name { get; set; }

        void Bessy_Proxy_Shallow_Copy_From(EntityType entity);
        void Bessy_Proxy_Deep_Copy_From(EntityType entity);
    }

    public interface IProxyFactory<IdType, EntityType>
    {
        string IdToken { get; set; }
        Func<EntityType, IdType> IdGet { get; set; }
        Action<EntityType, IdType> IdSet { get; set; }
        Type GetProxyTypeFor(Type type);
        T GetInstanceFor<T>(IPocoRelationalDatabase<IdType, EntityType> repository, T instance) where T : EntityType;
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

        MethodInfo getType = typeof(Object).GetMethod("GetType", 
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new Type[] { }, null);

        MethodInfo copyFields = typeof(BESSy.Relational.PocoProxyHandler<IdType, EntityType>).GetMethod("CopyFields", 
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, 
            null, new Type[]{ typeof(Object), typeof(Object), typeof(Type), typeof(Type), typeof(String[]) },  null);


        readonly MethodInfo getIdFromFactory = null;

        readonly MethodInfo getIdInvoke = typeof(System.Func<EntityType, IdType>).GetMethod("Invoke");

        bool useTransientAssembly = true;
        string assemblyName = "Bessy.Proxy";

        public PocoProxyFactory()
        {
            getIdFromFactory = this.GetType().GetMethod("get_IdGet");
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

        public T GetInstanceFor<T>(IPocoRelationalDatabase<IdType, EntityType> repository, T instance) where T : EntityType
        {
            return GetProxyFor<T>(repository, instance);
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

            var fs = inType.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);

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
                    _typeCache.Add(type.AssemblyQualifiedName, proxyType);
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

            MethodBuilder getSimpleTypeName = null;
            MethodBuilder setSimpleTypeName = null;

            MethodBuilder getIds = null;
            MethodBuilder setIds = null;

            MethodBuilder getRepo = null;
            MethodBuilder setRepo = null;

            var cascadeProp = BuildProperty(typeBuilder, "Bessy_OnCascade_Delete", typeof(bool));
            var repoProp = BuildProperty(typeBuilder, "Bessy_Proxy_Repository", typeof(IPocoRelationalDatabase<IdType, EntityType>), out getRepo, out setRepo);
            var factoryProp = BuildProperty(typeBuilder, "Bessy_Proxy_Factory", typeof(IProxyFactory<IdType, EntityType>), out getFactory, out setFactory);
            var idProp = BuildProperty(typeBuilder, "Bessy_Proxy_RelationshipIds", typeof(IDictionary<string, IdType[]>), out getIds, out setIds);
            var oldIdProp = BuildProperty(typeBuilder, "Bessy_Proxy_OldId", idType, out getOldId, out setOldId);
            var simpleNameProp = BuildProperty(typeBuilder, "Bessy_Proxy_Simple_Type_Name", typeof(string), out getSimpleTypeName, out setSimpleTypeName);

            repoProp.SetCustomAttribute(cabJsonIgnore);
            factoryProp.SetCustomAttribute(cabJsonIgnore);
            idProp.SetCustomAttribute(cabJsonPropertyIds);
            oldIdProp.SetCustomAttribute(cabJsonPropertyOldId);
            simpleNameProp.SetCustomAttribute(cabJsonPropertySimpleTypeName);

            var gets = new List<MethodBuilder>();
            var sets = new List<MethodBuilder>();

            foreach (var p in propOverrides.Where(p => p.PropertyType.GetInterface("IEnumerable") == null))
                BuildProperty(typeBuilder, factoryProp, p, gets, sets);

            foreach (var p in propOverrides.Where(p => p.PropertyType.GetInterface("IEnumerable") != null))
                BuildEnumerableProperty(typeBuilder, factoryProp, p, gets, sets);

            var defaultCtor = BuildDefaultConstructor(typeBuilder, entityType, setIds);
            var initCtor = BuildInitializeConstructor(typeBuilder, defaultCtor, setFactory, setRepo);

            var shallowCopyMethod = BuildShallowCopy(typeBuilder, propOverrides, sets, originalType, getFactory, setOldId, setSimpleTypeName);
            var deepCopyMethod = BuildDeepCopy(typeBuilder, propOverrides, sets, originalType, getFactory, setOldId, setSimpleTypeName);

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
            System.Reflection.MethodAttributes methodAttributes =
                  System.Reflection.MethodAttributes.Public
                | System.Reflection.MethodAttributes.Virtual
                | System.Reflection.MethodAttributes.Final
                | System.Reflection.MethodAttributes.HideBySig
                | System.Reflection.MethodAttributes.NewSlot;

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
            System.Reflection.MethodAttributes methodAttributes =
                  System.Reflection.MethodAttributes.Public
                | System.Reflection.MethodAttributes.Virtual
                | System.Reflection.MethodAttributes.Final
                | System.Reflection.MethodAttributes.HideBySig
                | System.Reflection.MethodAttributes.NewSlot;

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
            // Declaring method builder
            // Method attributes
            System.Reflection.MethodAttributes methodAttributes =
                  System.Reflection.MethodAttributes.Public
                | System.Reflection.MethodAttributes.Virtual
                | System.Reflection.MethodAttributes.Final
                | System.Reflection.MethodAttributes.HideBySig
                | System.Reflection.MethodAttributes.NewSlot;

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
            // Declaring method builder
            // Method attributes
            System.Reflection.MethodAttributes methodAttributes =
                  System.Reflection.MethodAttributes.Public
                | System.Reflection.MethodAttributes.Virtual
                | System.Reflection.MethodAttributes.Final
                | System.Reflection.MethodAttributes.HideBySig
                | System.Reflection.MethodAttributes.NewSlot;

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
            // Declaring method builder
            // Method attributes
            System.Reflection.MethodAttributes methodAttributes =
                  System.Reflection.MethodAttributes.Public
                | System.Reflection.MethodAttributes.Virtual
                | System.Reflection.MethodAttributes.Final
                | System.Reflection.MethodAttributes.HideBySig
                | System.Reflection.MethodAttributes.NewSlot;

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
            // Declaring method builder
            // Method attributes
            System.Reflection.MethodAttributes methodAttributes =
                  System.Reflection.MethodAttributes.Public
                | System.Reflection.MethodAttributes.Virtual
                | System.Reflection.MethodAttributes.Final
                | System.Reflection.MethodAttributes.HideBySig
                | System.Reflection.MethodAttributes.NewSlot;

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

        private MethodBuilder BuildShallowCopy(TypeBuilder tBuilder, List<PropertyInfo> propOverrides, List<MethodBuilder> sets, Type instanceType, MethodBuilder getFactory, MethodBuilder setOldId, MethodBuilder setSimpleTypeName)
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
            //var siteContainers = new Dictionary<string, FieldBuilder[]>();

            //var shallowSiteContainer = tBuilder.DefineNestedType(
            //    tBuilder.Name + "+<Bessy_Proxy_Shallow_Copy_From>o__SiteContainer0",
            //    TypeAttributes.NestedPrivate | TypeAttributes.BeforeFieldInit | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.AnsiClass | TypeAttributes.AutoClass,
            //    tBuilder);

            //shallowSiteContainer.SetParent(typeof(System.Object));

            //foreach (var p in pFields)
            //{
            //    var site1 = shallowSiteContainer.DefineField(
            //       "<>p__" + p.Name,
            //       typeof(System.Runtime.CompilerServices.CallSite<Func<CallSite, Object, Object, Object>>),
            //         FieldAttributes.Public | FieldAttributes.Static);

            //    var site2 = shallowSiteContainer.DefineField(
            //        "<>p__" + p.Name,
            //        typeof(System.Runtime.CompilerServices.CallSite<Func<CallSite, Object, Object>>),
            //          FieldAttributes.Public | FieldAttributes.Static);

            //    siteContainers.Add(p.Name, new FieldBuilder[] { site1, site2 });
            //}

            //shallowSiteContainer.CreateType();

            //.Where(f => !f.FieldType.GetCustomAttributes(true).ToList().Any(a => a is JsonIgnoreAttribute));

            ILGenerator generator = shallow.GetILGenerator();

            // Preparing locals
            LocalBuilder instance = generator.DeclareLocal(instanceType);
            LocalBuilder strArray = generator.DeclareLocal(typeof(String[]));
            LocalBuilder flag = generator.DeclareLocal(typeof(Boolean));
            LocalBuilder infoArray = generator.DeclareLocal(typeof(CSharpArgumentInfo[]));

            //LocalBuilder cSharp = generator.DeclareLocal(typeof(CSharpArgumentInfo[]));

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

                //generator.Emit(OpCodes.Nop);
                //generator.Emit(OpCodes.Nop);
                //generator.Emit(OpCodes.Ldloc_0);
                //generator.Emit(OpCodes.Call, exposedObjectFrom);
                //generator.Emit(OpCodes.Stloc_1);
                //generator.Emit(OpCodes.Ldarg_0);
                //generator.Emit(OpCodes.Call, exposedObjectFrom);
                //generator.Emit(OpCodes.Stloc_2);

                //foreach (var field in pFields)
                //    CopyPrivateField(tBuilder, siteContainers[field.Name], instanceType, generator, field);

                //generator.Emit(OpCodes.Nop);
                generator.Emit(OpCodes.Nop);
            }

            foreach (var field in fields.Where(f => f.IsPublic))
                CopyField(tBuilder, instanceType, generator, field);

            var properties = instanceType.GetProperties(BindingFlags.Instance | BindingFlags.Public).ToList();

            foreach (var prop in properties.Where(p => !propOverrides.Any(o => o.Name == p.Name)))
                CopyProperty(generator, prop, prop.GetSetMethod());

            generator.Emit(OpCodes.Nop);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Call, getFactory);
            generator.Emit(OpCodes.Call, getIdFromFactory);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Call, getIdInvoke);
            generator.Emit(OpCodes.Call, setOldId);

            generator.Emit(OpCodes.Nop);
            generator.Emit(OpCodes.Nop);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldstr, tBuilder.BaseType.FullName);
            generator.Emit(OpCodes.Call, setSimpleTypeName);

            generator.Emit(OpCodes.Nop);
            generator.MarkLabel(gtfo);
            generator.Emit(OpCodes.Ret);

            return shallow;
        }

        protected MethodBuilder BuildDeepCopy(TypeBuilder tBuilder, IEnumerable<PropertyInfo> propOverrides, List<MethodBuilder> sets, Type instanceType, MethodBuilder getFactory, MethodBuilder setOldId, MethodBuilder setSimpleTypeName)
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
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Call, getFactory);
            generator.Emit(OpCodes.Call, getIdFromFactory);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Call, getIdInvoke);
            generator.Emit(OpCodes.Call, setOldId);

            generator.Emit(OpCodes.Nop);
            generator.Emit(OpCodes.Nop);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldstr, tBuilder.BaseType.FullName);
            generator.Emit(OpCodes.Call, setSimpleTypeName);

            generator.Emit(OpCodes.Nop);
            generator.MarkLabel(gtfo);
            generator.Emit(OpCodes.Ret);

            return deep;
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

        private void CopyPrivateField(TypeBuilder proxyType, Type instanceType, ILGenerator gen, FieldInfo field)
        {
            //Label label286 = gen.DefineLabel();
            //Label label363 = gen.DefineLabel();

            //gen.Emit(OpCodes.Ldsfld, siteContainers[0]);
            //gen.Emit(OpCodes.Brtrue_S, label286);
            //gen.Emit(OpCodes.Ldc_I4_0);
            //gen.Emit(OpCodes.Ldstr, field.Name);
            //gen.Emit(OpCodes.Ldtoken, proxyType);
            //gen.Emit(OpCodes.Call, getTypeFromHandle);
            //gen.Emit(OpCodes.Ldc_I4_2);
            //gen.Emit(OpCodes.Newarr, typeof(CSharpArgumentInfo));
            //gen.Emit(OpCodes.Stloc_S, 4);
            //gen.Emit(OpCodes.Ldloc_S, 4);
            //gen.Emit(OpCodes.Ldc_I4_0);
            //gen.Emit(OpCodes.Ldc_I4_0);
            //gen.Emit(OpCodes.Ldnull);
            //gen.Emit(OpCodes.Call, create);
            //gen.Emit(OpCodes.Stelem_Ref);
            //gen.Emit(OpCodes.Ldloc_S, 4);
            //gen.Emit(OpCodes.Ldc_I4_1);
            //gen.Emit(OpCodes.Ldc_I4_0);
            //gen.Emit(OpCodes.Ldnull);
            //gen.Emit(OpCodes.Call, create);
            //gen.Emit(OpCodes.Stelem_Ref);
            //gen.Emit(OpCodes.Ldloc_S, 4);
            //gen.Emit(OpCodes.Call, binderSetMember);
            //gen.Emit(OpCodes.Call, callSiteCreate4);
            //gen.Emit(OpCodes.Stsfld, siteContainers[0]);
            //gen.Emit(OpCodes.Br_S, label286);
            //gen.MarkLabel(label286);
            //gen.Emit(OpCodes.Ldsfld, siteContainers[0]);
            //gen.Emit(OpCodes.Ldfld, callSiteTarget4);
            //gen.Emit(OpCodes.Ldsfld, siteContainers[0]);
            //gen.Emit(OpCodes.Ldloc_2);
            //gen.Emit(OpCodes.Ldsfld, siteContainers[1]);
            //gen.Emit(OpCodes.Brtrue_S, label363);
            //gen.Emit(OpCodes.Ldc_I4_0);
            //gen.Emit(OpCodes.Ldstr, field.Name);
            //gen.Emit(OpCodes.Ldtoken, proxyType);
            //gen.Emit(OpCodes.Call, getTypeFromHandle);
            //gen.Emit(OpCodes.Ldc_I4_1);
            //gen.Emit(OpCodes.Newarr, typeof(CSharpArgumentInfo));
            //gen.Emit(OpCodes.Stloc_S, 4);
            //gen.Emit(OpCodes.Ldloc_S, 4);
            //gen.Emit(OpCodes.Ldc_I4_0);
            //gen.Emit(OpCodes.Ldc_I4_0);
            //gen.Emit(OpCodes.Ldnull);
            //gen.Emit(OpCodes.Call, create);
            //gen.Emit(OpCodes.Stelem_Ref);
            //gen.Emit(OpCodes.Ldloc_S, 4);
            //gen.Emit(OpCodes.Call, binderGetMember);
            //gen.Emit(OpCodes.Call, callSiteCreate3);
            //gen.Emit(OpCodes.Stsfld, siteContainers[1]);
            //gen.Emit(OpCodes.Br_S, label363);
            //gen.MarkLabel(label363);
            //gen.Emit(OpCodes.Ldsfld, siteContainers[1]);
            //gen.Emit(OpCodes.Ldfld, callSiteTarget3);
            //gen.Emit(OpCodes.Ldsfld, siteContainers[1]);
            //gen.Emit(OpCodes.Ldloc_1);
            //gen.Emit(OpCodes.Callvirt, callsiteInvoke3);
            //gen.Emit(OpCodes.Callvirt, callsiteInvoke4);
            //gen.Emit(OpCodes.Pop);
        }

        #endregion

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
