using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace ItemModKit.Adapters.Duckov
{
    internal static class DuckovReflectionCache
    {
        private static readonly ConcurrentDictionary<(Type, string, BindingFlags), PropertyInfo> s_props = new();
        private static readonly ConcurrentDictionary<(Type, string, BindingFlags), MethodInfo> s_methods = new();
        private static readonly ConcurrentDictionary<(Type, string, BindingFlags, string), MethodInfo> s_methodsBySig = new();
        private static readonly ConcurrentDictionary<(Type, string, BindingFlags), FieldInfo> s_fields = new();
        private static readonly ConcurrentDictionary<Type, string[]> s_enumNames = new();
        // compiled accessors
        private static readonly ConcurrentDictionary<(Type, string, BindingFlags), Func<object, object>> s_getters = new();
        private static readonly ConcurrentDictionary<(Type, string, BindingFlags), Action<object, object>> s_setters = new();

        public static PropertyInfo GetProp(Type t, string name, BindingFlags flags)
        {
            if (t == null || string.IsNullOrEmpty(name)) return null;
            return s_props.GetOrAdd((t, name, flags), key => SafeGetProperty(key.Item1, key.Item2, key.Item3));
        }
        public static MethodInfo GetMethod(Type t, string name, BindingFlags flags)
        {
            if (t == null || string.IsNullOrEmpty(name)) return null;
            return s_methods.GetOrAdd((t, name, flags), key => SafeGetMethod(key.Item1, key.Item2, key.Item3));
        }
        public static MethodInfo GetMethod(Type t, string name, BindingFlags flags, Type[] paramTypes)
        {
            if (t == null || string.IsNullOrEmpty(name)) return null;
            var sig = ParamSig(paramTypes);
            return s_methodsBySig.GetOrAdd((t, name, flags, sig), key => SafeGetMethod(key.Item1, key.Item2, key.Item3, paramTypes));
        }
        public static FieldInfo GetField(Type t, string name, BindingFlags flags)
        {
            if (t == null || string.IsNullOrEmpty(name)) return null;
            return s_fields.GetOrAdd((t, name, flags), key => SafeGetField(key.Item1, key.Item2, key.Item3));
        }

        public static Func<object, object> GetGetter(Type t, string name, BindingFlags flags)
        {
            if (t == null || string.IsNullOrEmpty(name)) return null;
            return s_getters.GetOrAdd((t, name, flags), key => BuildGetter(key.Item1, key.Item2, key.Item3));
        }
        public static Action<object, object> GetSetter(Type t, string name, BindingFlags flags)
        {
            if (t == null || string.IsNullOrEmpty(name)) return null;
            return s_setters.GetOrAdd((t, name, flags), key => BuildSetter(key.Item1, key.Item2, key.Item3));
        }

        public static string[] GetEnumNames(Type enumType)
        {
            try { if (enumType == null || !enumType.IsEnum) return Array.Empty<string>(); return s_enumNames.GetOrAdd(enumType, t => Enum.GetNames(t)); } catch { return Array.Empty<string>(); }
        }

        private static PropertyInfo SafeGetProperty(Type t, string name, BindingFlags flags)
        {
            try { return t.GetProperty(name, flags); } catch { return null; }
        }
        private static MethodInfo SafeGetMethod(Type t, string name, BindingFlags flags)
        {
            try { return t.GetMethod(name, flags); }
            catch { try { return t.GetMethods(flags).FirstOrDefault(m => m.Name == name); } catch { return null; } }
        }
        private static MethodInfo SafeGetMethod(Type t, string name, BindingFlags flags, Type[] paramTypes)
        {
            try { return t.GetMethod(name, flags, binder: null, types: paramTypes ?? Type.EmptyTypes, modifiers: null); }
            catch
            {
                try
                {
                    var methods = t.GetMethods(flags).Where(m => m.Name == name);
                    if (paramTypes == null) return methods.FirstOrDefault();
                    foreach (var m in methods)
                    {
                        var ps = m.GetParameters();
                        if (ps.Length != paramTypes.Length) continue;
                        bool match = true;
                        for (int i = 0; i < ps.Length; i++)
                        {
                            var want = paramTypes[i] ?? typeof(object);
                            if (!ps[i].ParameterType.IsAssignableFrom(want)) { match = false; break; }
                        }
                        if (match) return m;
                    }
                    return null;
                }
                catch { return null; }
            }
        }
        private static FieldInfo SafeGetField(Type t, string name, BindingFlags flags)
        {
            try { return t.GetField(name, flags); } catch { return null; }
        }

        private static string ParamSig(Type[] types)
        {
            if (types == null || types.Length == 0) return string.Empty;
            try { return string.Join("|", types.Select(tp => tp?.FullName ?? "null")); } catch { return string.Empty; }
        }

        private static Func<object, object> BuildGetter(Type t, string name, BindingFlags flags)
        {
            try
            {
                var p = GetProp(t, name, flags);
                if (p == null) return null;
                var objParam = Expression.Parameter(typeof(object), "obj");
                var inst = Expression.Convert(objParam, p.DeclaringType);
                var access = Expression.Property(inst, p);
                var box = Expression.Convert(access, typeof(object));
                return Expression.Lambda<Func<object, object>>(box, objParam).Compile();
            }
            catch { return null; }
        }
        private static Action<object, object> BuildSetter(Type t, string name, BindingFlags flags)
        {
            try
            {
                var p = GetProp(t, name, flags);
                if (p == null || !p.CanWrite) return null;
                var objParam = Expression.Parameter(typeof(object), "obj");
                var valParam = Expression.Parameter(typeof(object), "val");
                var inst = Expression.Convert(objParam, p.DeclaringType);
                var val = Expression.Convert(valParam, p.PropertyType);
                var body = Expression.Assign(Expression.Property(inst, p), val);
                return Expression.Lambda<Action<object, object>>(body, objParam, valParam).Compile();
            }
            catch { return null; }
        }
    }
}
