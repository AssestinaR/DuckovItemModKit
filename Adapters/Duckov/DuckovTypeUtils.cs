using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace ItemModKit.Adapters.Duckov
{
 internal static class DuckovTypeUtils
 {
 private static readonly ConcurrentDictionary<string, Type> s_typeCache = new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);
 internal static Type FindType(string name)
 {
 try
 {
 if (string.IsNullOrEmpty(name)) return null;
 if (s_typeCache.TryGetValue(name, out var t) && t != null) return t;
 foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
 {
 try { t = a.GetType(name, false); if (t != null) { s_typeCache[name] = t; return t; } } catch { }
 }
 }
 catch { }
 return null;
 }
 internal static T GetProp<T>(object obj, string name)
 {
 if (obj == null) return default(T);
 try
 {
 var getter = DuckovReflectionCache.GetGetter(obj.GetType(), name, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (getter == null) return default(T);
 var v = getter(obj);
 if (v == null) return default(T);
 try { if (v is T tv) return tv; return (T)Convert.ChangeType(v, typeof(T)); } catch { return default(T); }
 }
 catch { return default(T); }
 }
 internal static void SetProp(object obj, string name, object val)
 {
 try
 {
 if (obj == null) return;
 var setter = DuckovReflectionCache.GetSetter(obj.GetType(), name, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (setter != null) { setter(obj, val); return; }
 // fallback (rare)
 var p = DuckovReflectionCache.GetProp(obj.GetType(), name, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 p?.SetValue(obj, val, null);
 }
 catch { }
 }
 internal static object GetMaybe(object obj, string[] names)
 {
 if (obj == null || names == null) return null;
 foreach (var n in names)
 {
 try
 {
 // property first (cached delegate)
 var getter = DuckovReflectionCache.GetGetter(obj.GetType(), n, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (getter != null)
 {
 var v = getter(obj);
 if (v != null) return v;
 }
 // then field via cached FieldInfo
 var f = DuckovReflectionCache.GetField(obj.GetType(), n, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (f != null)
 {
 var v = f.GetValue(obj);
 if (v != null) return v;
 }
 }
 catch { }
 }
 return null;
 }
 internal static float ConvertToFloat(object v)
 {
 try { if (v == null) return 0f; if (v is float f) return f; if (v is double d) return (float)d; if (v is int i) return i; float x; if (float.TryParse(v.ToString(), out x)) return x; } catch { }
 return 0f;
 }
 internal static bool ConvertToBool(object v)
 {
 try { if (v == null) return false; if (v is bool b) return b; var s = v.ToString(); if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)) return true; if (string.Equals(s, "1")) return true; } catch { }
 return false;
 }

 // Stable Unity instance id when available; otherwise fallback to GetHashCode
 internal static int GetStableId(object obj)
 {
 if (obj == null) return 0;
 try
 {
 var t = obj.GetType();
 var m = DuckovReflectionCache.GetMethod(t, "GetInstanceID", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (m != null && m.GetParameters().Length == 0)
 {
 var v = m.Invoke(obj, null);
 if (v is int id) return id;
 try { return Convert.ToInt32(v); } catch { }
 }
 }
 catch { }
 try { return obj.GetHashCode(); } catch { return 0; }
 }
 }
}
