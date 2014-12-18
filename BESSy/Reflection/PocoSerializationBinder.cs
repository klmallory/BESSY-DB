using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using BESSy.Json;
using BESSy.Parallelization;

namespace BESSy.Reflection
{
    [SecuritySafeCritical]
    public class PocoSerializationBinder : BESSy.Json.Serialization.DefaultSerializationBinder
    {
        static object _syncRoot = new object();

        static Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();

        public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            if (serializedType.Assembly.IsDynamic)
            {
                assemblyName = serializedType.BaseType.Assembly.FullName;
                typeName = serializedType.BaseType.FullName;
            }
            else
            {
                assemblyName = serializedType.Assembly.FullName;
                typeName = serializedType.FullName;
            }
            var key = assemblyName + typeName;

            lock (_syncRoot)
                if (_typeCache.Count > TaskGrouping.ArrayLimit)
                    _typeCache.Clear();

            lock (_syncRoot)
            {
                if (_typeCache.ContainsKey(key))
                    _typeCache[key] = serializedType;
                else
                    _typeCache.Add(key, serializedType);
            }
        }

        public override Type BindToType(string assemblyName, string typeName)
        {
            var key = assemblyName + typeName;

            lock (_syncRoot)
            {
                if (_typeCache.ContainsKey(key))
                    return _typeCache[key];
                else if (_typeCache.ContainsKey(typeName))
                    return _typeCache[typeName];
            }

            lock (_syncRoot)
                if (_typeCache.Count > TaskGrouping.ArrayLimit)
                    _typeCache.Clear();

            Type type = null;

            if (assemblyName == null)
            {
                type = Type.GetType(typeName);

                lock (_syncRoot)
                    _typeCache.Add(key, type);

                return type;
            }

            var assembly = Assembly.LoadWithPartialName(assemblyName);

            if (assembly == null)
            {
                Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

                for (var i = 0; i < loadedAssemblies.Length; i++)
                    if (loadedAssemblies[i].FullName.StartsWith(assemblyName + ","))
                    { assembly = loadedAssemblies[i]; break; }

                if (assembly == null)
                    foreach (Assembly a in loadedAssemblies)
                        if (a.FullName == assemblyName)
                        { assembly = a; break; }

                if (assembly == null)
                    throw new JsonSerializationException(string.Format("Could not load assembly '{0}'.", assemblyName));
            }

            type = assembly.GetType(typeName);

            if (type == null)
                throw new JsonSerializationException(string.Format("Could not find type '{0}' in assembly '{1}'.", typeName, assembly.FullName));

            lock (_syncRoot)
                _typeCache.Add(key, type);

            return type;
        }
    }
}
