using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Reflection;

namespace BESSy.Reflection
{
    public class DynamicMemberManager : DynamicObject
    {
        Dictionary<string, Dictionary<int, List<MethodInfo>>> _instanceMethods;
        Dictionary<string, Dictionary<int, List<MethodInfo>>> _genInstanceMethods;

        object _instance;
        Type _instanceType;


        DynamicMemberManager(object obj)
        {
            _instance = obj;
            _instanceType = obj.GetType();

            _instanceMethods =
                _instanceType
                    .GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => !m.IsGenericMethod)
                    .GroupBy(m => m.Name)
                    .ToDictionary(
                        p => p.Key,
                        p => p.GroupBy(r => r.GetParameters().Length).ToDictionary(r => r.Key, r => r.ToList()));

            _genInstanceMethods =
                _instanceType
                    .GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.IsGenericMethod)
                    .GroupBy(m => m.Name)
                    .ToDictionary(
                        p => p.Key,
                        p => p.GroupBy(r => r.GetParameters().Length).ToDictionary(r => r.Key, r => r.ToList()));
        }

        public object Instance { get { return _instance; } }

        public static dynamic GetManager(object obj)
        {
            return new DynamicMemberManager(obj);
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            try
            {
                var propertyInfo = _instance.GetType().GetProperty(
                    binder.Name,
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                if (propertyInfo != null)
                {
                    result = propertyInfo.GetValue(_instance, null);
                    return true;
                }

                var fieldInfo = _instance.GetType().GetField(
                    binder.Name,
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                var type = _instanceType;

                while (fieldInfo == null && type.BaseType != null)
                {
                    fieldInfo = type.BaseType.GetField(
                    binder.Name,
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                    type = type.BaseType;
                }

                if (fieldInfo != null)
                {
                    result = fieldInfo.GetValue(_instance);
                    return true;
                }
            }
            catch (Exception ex) { Trace.TraceError("Unable to set member: {0}, {1}", binder.Name, ex.Message); }

            result = null;
            return false;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            try
            {
                var propertyInfo = _instanceType.GetProperty(
                    binder.Name,
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                if (propertyInfo != null)
                {
                    propertyInfo.SetValue(_instance, value, null);
                    return true;
                }

                var fieldInfo = _instanceType.GetField(
                    binder.Name,
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                var type = _instanceType;

                while (fieldInfo == null && type.BaseType != null)
                {
                    fieldInfo = type.BaseType.GetField(
                    binder.Name,
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                    type = type.BaseType;
                }

                if (fieldInfo != null)
                {
                    fieldInfo.SetValue(_instance, value);
                    return true;
                }

            }
            catch (Exception ex) { Trace.TraceError("Unable to set member: {0}, {1}", binder.Name, ex.Message); }

            return false;
        }
    }
}
