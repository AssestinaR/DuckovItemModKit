using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ItemModKit.Core;
using ItemStatsSystem;
using static ItemModKit.Adapters.Duckov.DuckovTypeUtils;

namespace ItemModKit.Adapters.Duckov
{
 internal sealed class DuckovItemAdapter : IItemAdapter
 {
 public string GetName(object item) { try { return (item as Item)?.DisplayName ?? GetProp<string>(item, "DisplayName"); } catch { return null; } }
 public void SetName(object item, string name) { try { SetProp(item, "DisplayName", name); } catch { } try { SetProp(item, "Name", name); } catch { } }
 public string GetDisplayNameRaw(object item) { try { return GetProp<string>(item, "DisplayNameRaw"); } catch { return null; } }
 public void SetDisplayNameRaw(object item, string raw) { try { SetProp(item, "DisplayNameRaw", raw); } catch { } try { SetProp(item, "RawName", raw); } catch { } }
 public int GetTypeId(object item) { try { return GetProp<int>(item, "TypeID"); } catch { return 0; } }
 public void SetTypeId(object item, int typeId) { try { SetProp(item, "TypeID", typeId); } catch { } }
 public int GetQuality(object item) { try { return GetProp<int>(item, "Quality"); } catch { return 0; } }
 public void SetQuality(object item, int quality) { try { SetProp(item, "Quality", quality); } catch { } }
 public int GetDisplayQuality(object item) { try { return GetProp<int>(item, "DisplayQuality"); } catch { return 0; } }
 public void SetDisplayQuality(object item, int dq) { try { SetProp(item, "DisplayQuality", dq); } catch { } }
 public int GetValue(object item) { try { return GetProp<int>(item, "Value"); } catch { return 0; } }
 public void SetValue(object item, int value) { try { SetProp(item, "Value", value); } catch { } }

 public VariableEntry[] GetVariables(object item)
 {
 try
 {
 var list = new List<VariableEntry>();
 var vars = item?.GetType().GetProperty("Variables", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(item, null) as IEnumerable;
 if (vars != null)
 {
 foreach (var v in vars)
 {
 if (v == null) continue;
 string key = GetProp<string>(v, "Key");
 object val = ReadCustomDataValue(v);
 list.Add(new VariableEntry{ Key = key, Value = val, Constant = false });
 }
 }
 return list.ToArray();
 }
 catch { return Array.Empty<VariableEntry>(); }
 }

 public void SetVariable(object item, string key, object value, bool constant)
 {
 try
 {
 var t = item?.GetType(); if (t == null) return;
 var vars = t.GetProperty("Variables", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(item, null);
 if (vars == null) return;
 var vt = vars.GetType();
 // Prefer Variables helpers
 var typeCode = value == null ? TypeCode.Empty : Type.GetTypeCode(value.GetType());
 MethodInfo m = null;
 switch (typeCode)
 {
 case TypeCode.Int32: m = vt.GetMethod("SetInt", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance); break;
 case TypeCode.Single: m = vt.GetMethod("SetFloat", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance); break;
 case TypeCode.String: m = vt.GetMethod("SetString", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance); break;
 default:
 m = vt.GetMethod("SetString", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 value = value?.ToString();
 break;
 }
 if (m != null)
 {
 var ps = m.GetParameters();
 try
 {
 if (ps.Length ==3) { m.Invoke(vars, new object[]{ key, value, constant }); return; }
 if (ps.Length ==2) { m.Invoke(vars, new object[]{ key, value }); return; }
 }
 catch { /* fallback below */ }
 }
 // other fallbacks: generic Set / SetOrAdd
 foreach (var name in new[]{"Set","SetOrAdd","SetAny","SetObject"})
 {
 var mm = vt.GetMethod(name, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (mm == null) continue;
 var ps = mm.GetParameters();
 try
 {
 if (ps.Length ==3) { mm.Invoke(vars, new object[]{ key, value, constant }); return; }
 if (ps.Length ==2) { mm.Invoke(vars, new object[]{ key, value }); return; }
 }
 catch { }
 }
 // fallback: Variables.Add(new CustomData(key, value))
 var listT = vt;
 var add = listT.GetMethod("Add", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 var cdT = listT.Assembly.GetTypes().FirstOrDefault(x => x.Name == "CustomData");
 if (cdT != null)
 {
 object cd = null;
 try { cd = Activator.CreateInstance(cdT, new object[]{ key, value }); }
 catch { try { cd = Activator.CreateInstance(cdT, new object[]{ key, (value?.ToString() ?? string.Empty) }); } catch { } }
 if (cd != null && add != null) { try { add.Invoke(vars, new[]{ cd }); return; } catch { } }
 }
 }
 catch { }
 }

 public object GetVariable(object item, string key)
 {
 try
 {
 var t = item?.GetType(); if (t == null) return null;
 var vars = t.GetProperty("Variables", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(item, null);
 if (vars == null) return null;
 var vt = vars.GetType();
 foreach (var name in new[]{ "GetInt", "GetFloat", "GetString", "GetBool" })
 {
 var m = vt.GetMethod(name, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (m == null) continue;
 var ps = m.GetParameters();
 try
 {
 if (ps.Length ==2)
 {
 object def = name == "GetInt" ? (object)0 : name == "GetFloat" ? (object)0f : name == "GetBool" ? (object)false : "";
 return m.Invoke(vars, new object[]{ key, def });
 }
 if (ps.Length ==1) return m.Invoke(vars, new object[]{ key });
 }
 catch { }
 }
 var enumVars = vars as IEnumerable;
 if (enumVars != null)
 {
 foreach (var v in enumVars)
 {
 if (v == null) continue;
 string k = GetProp<string>(v, "Key");
 if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) return ReadCustomDataValue(v);
 }
 }
 return null;
 }
 catch { return null; }
 }

 public bool RemoveVariable(object item, string key)
 {
 try
 {
 var t = item?.GetType(); if (t == null) return false;
 var vars = t.GetProperty("Variables", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(item, null);
 if (vars == null) return false;
 var listT = vars.GetType();
 // Try common removal patterns
 // 1) Remove(string key)
 var remKey = listT.GetMethod("Remove", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, null, new Type[]{ typeof(string) }, null)
           ?? listT.GetMethod("RemoveKey", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, null, new Type[]{ typeof(string) }, null)
           ?? listT.GetMethod("Delete", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, null, new Type[]{ typeof(string) }, null)
           ?? listT.GetMethod("DeleteKey", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, null, new Type[]{ typeof(string) }, null)
           ?? listT.GetMethod("RemoveByKey", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, null, new Type[]{ typeof(string) }, null);
 if (remKey != null) { remKey.Invoke(vars, new object[]{ key }); return true; }
 // 2) Find entry then Remove(entry)
 var getEntry = listT.GetMethod("GetEntry", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
             ?? listT.GetMethod("Get", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, null, new[]{ typeof(string) }, null)
             ?? listT.GetMethod("Find", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, null, new[]{ typeof(string) }, null);
 var remove = listT.GetMethod("Remove", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 object entry = null;
 if (getEntry != null)
 {
 try { entry = getEntry.GetParameters().Length == 1 ? getEntry.Invoke(vars, new object[]{ key }) : getEntry.Invoke(vars, null); } catch { entry = null; }
 }
 if (entry == null)
 {
 foreach (var v in (vars as IEnumerable) ?? Array.Empty<object>())
 {
 string k = GetProp<string>(v, "Key");
 if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) { entry = v; break; }
 }
 }
 if (entry != null && remove != null) { remove.Invoke(vars, new[]{ entry }); return true; }
 }
 catch { }
 return false;
 }

 public ModifierEntry[] GetModifiers(object item)
 {
 try
 {
 var list = new List<ModifierEntry>();
 var mods = item?.GetType().GetProperty("Modifiers", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(item, null) as IEnumerable;
 if (mods != null)
 {
 foreach (var m in mods)
 {
 if (m == null) continue;
 string key = GetMaybe(m, new[]{ "Key", "key" }) as string;
 float val = ConvertToFloat(GetMaybe(m, new[]{ "Value", "value" }));
 string mod = GetMaybe(m, new[]{ "Modifier", "modifier" }) as string;
 bool isPercent = ConvertToBool(GetMaybe(m, new[]{ "IsPercent", "isPercent" }));
 list.Add(new ModifierEntry{ Key = key, Value = val, Modifier = mod, IsPercent = isPercent });
 }
 }
 return list.ToArray();
 }
 catch { return Array.Empty<ModifierEntry>(); }
 }

 public void ReapplyModifiers(object item)
 {
 try
 {
 var modsCol = item?.GetType().GetProperty("Modifiers", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(item, null);
 var m = modsCol?.GetType().GetMethod("ReapplyModifiers", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 m?.Invoke(modsCol, null);
 }
 catch { }
 }

 public SlotEntry[] GetSlots(object item)
 {
 try
 {
 var list = new List<SlotEntry>();
 var slots = item?.GetType().GetProperty("Slots", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(item, null) as IEnumerable;
 if (slots != null)
 {
 foreach (var s in slots)
 {
 if (s == null) continue;
 string key = GetMaybe(s, new[]{ "Key", "Name", "Id" }) as string;
 var content = GetMaybe(s, new[]{ "Content" });
 bool occ = content != null;
 string plugType = GetMaybe(s, new[]{ "PlugType", "Type", "ExpectedType" })?.ToString();
 list.Add(new SlotEntry{ Key = key, Occupied = occ, PlugType = plugType });
 }
 }
 return list.ToArray();
 }
 catch { return Array.Empty<SlotEntry>(); }
 }

 public string[] GetTags(object item)
 {
 try
 {
 var tags = item?.GetType().GetProperty("Tags", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(item, null) as IEnumerable;
 if (tags == null) return Array.Empty<string>();
 var list = new List<string>(); foreach (var t in tags) if (t != null) list.Add(t.ToString());
 return list.ToArray();
 }
 catch { return Array.Empty<string>(); }
 }
 public void SetTags(object item, string[] tags)
 {
 try
 {
 var p = item?.GetType().GetProperty("Tags", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (p == null) return;
 var hasSetter = p.CanWrite;
 var curObj = p.GetValue(item, null);
 // If property is array and writable, replace array
 if (hasSetter && p.PropertyType.IsArray)
 {
 p.SetValue(item, tags ?? Array.Empty<string>(), null); return;
 }
 // If we got a list we can mutate
 var ilist = curObj as System.Collections.IList;
 if (ilist != null && !ilist.IsFixedSize)
 {
 ilist.Clear(); if (tags != null) foreach (var s in tags) ilist.Add(s); return;
 }
 // Try set with compatible collection if setter exists
 if (hasSetter)
 {
 if (p.PropertyType == typeof(List<string>)) { p.SetValue(item, tags == null ? new List<string>() : new List<string>(tags), null); return; }
 if (typeof(IEnumerable<string>).IsAssignableFrom(p.PropertyType)) { p.SetValue(item, tags ?? Array.Empty<string>(), null); return; }
 }
 // Last resort: look for Add/Remove/Clear methods on current object
 if (curObj != null)
 {
 var t = curObj.GetType();
 var clear = t.GetMethod("Clear", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 var add = t.GetMethod("Add", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, null, new[]{ typeof(string) }, null);
 if (clear != null && add != null) { clear.Invoke(curObj, null); if (tags != null) foreach (var s in tags) add.Invoke(curObj, new object[]{ s }); }
 }
 }
 catch { }
 }
 public VariableEntry[] GetConstants(object item)
 {
 try
 {
 var list = new List<VariableEntry>();
 var cons = item?.GetType().GetProperty("Constants", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(item, null) as IEnumerable;
 if (cons != null)
 {
 foreach (var v in cons)
 {
 if (v == null) continue;
 string key = GetProp<string>(v, "Key");
 object val = ReadCustomDataValue(v);
 list.Add(new VariableEntry{ Key = key, Value = val, Constant = true });
 }
 }
 return list.ToArray();
 }
 catch { return Array.Empty<VariableEntry>(); }
 }
 public void SetConstant(object item, string key, object value, bool createIfNotExist)
 {
 try
 {
 var cons = item?.GetType().GetProperty("Constants", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(item, null);
 if (cons == null) return;
 var ct = cons.GetType();
 var typeCode = value == null ? TypeCode.Empty : Type.GetTypeCode(value.GetType());
 MethodInfo m = null;
 switch (typeCode)
 {
 case TypeCode.Int32: m = ct.GetMethod("SetInt", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance); break;
 case TypeCode.Single: m = ct.GetMethod("SetFloat", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance); break;
 case TypeCode.String: m = ct.GetMethod("SetString", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance); break;
 default: m = ct.GetMethod("SetString", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance); value = value?.ToString(); break;
 }
 if (m != null)
 {
 var ps = m.GetParameters();
 try
 {
 if (ps.Length ==3) { m.Invoke(cons, new object[]{ key, value, createIfNotExist }); return; }
 if (ps.Length ==2) { m.Invoke(cons, new object[]{ key, value }); return; }
 }
 catch { }
 }
 var any = ct.GetMethod("Set", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (any != null)
 {
 var ps = any.GetParameters();
 try
 {
 if (ps.Length ==3) { any.Invoke(cons, new object[]{ key, value, createIfNotExist }); return; }
 if (ps.Length ==2) { any.Invoke(cons, new object[]{ key, value }); return; }
 }
 catch { }
 }
 }
 catch { }
 }

 public object GetConstant(object item, string key)
 {
 try
 {
 var t = item?.GetType(); if (t == null) return null;
 var cons = t.GetProperty("Constants", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(item, null);
 if (cons == null) return null;
 var ct = cons.GetType();
 foreach (var name in new[]{ "GetInt", "GetFloat", "GetString", "GetBool" })
 {
 var m = ct.GetMethod(name, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (m == null) continue;
 var ps = m.GetParameters();
 try
 {
 if (ps.Length ==2)
 {
 object def = name == "GetInt" ? (object)0 : name == "GetFloat" ? (object)0f : name == "GetBool" ? (object)false : "";
 return m.Invoke(cons, new object[]{ key, def });
 }
 if (ps.Length ==1) return m.Invoke(cons, new object[]{ key });
 }
 catch { }
 }
 var enumCons = cons as IEnumerable;
 if (enumCons != null)
 {
 foreach (var v in enumCons)
 {
 if (v == null) continue;
 string k = GetProp<string>(v, "Key");
 if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) return ReadCustomDataValue(v);
 }
 }
 return null;
 }
 catch { return null; }
 }

 public bool RemoveConstant(object item, string key)
 {
     try
     {
         var cons = item?.GetType().GetProperty("Constants", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(item, null);
         if (cons == null) return false; var ct = cons.GetType();
         // 1) Remove(string key) variants
         var remKey = ct.GetMethod("Remove", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, null, new Type[]{ typeof(string) }, null)
                   ?? ct.GetMethod("RemoveKey", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, null, new Type[]{ typeof(string) }, null)
                   ?? ct.GetMethod("Delete", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, null, new Type[]{ typeof(string) }, null)
                   ?? ct.GetMethod("DeleteKey", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, null, new Type[]{ typeof(string) }, null)
                   ?? ct.GetMethod("RemoveByKey", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, null, new Type[]{ typeof(string) }, null);
         if (remKey != null) { remKey.Invoke(cons, new object[]{ key }); return true; }
         // 2) Get entry then Remove(entry)
         var getEntry = ct.GetMethod("GetEntry", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
                     ?? ct.GetMethod("Get", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, null, new[]{ typeof(string) }, null)
                     ?? ct.GetMethod("Find", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, null, new[]{ typeof(string) }, null);
         var remove = ct.GetMethod("Remove", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
         object entry = null; if (getEntry != null) { try { entry = getEntry.GetParameters().Length == 1 ? getEntry.Invoke(cons, new object[]{ key }) : getEntry.Invoke(cons, null); } catch { } }
         if (entry == null)
         {
             foreach (var v in (cons as IEnumerable) ?? Array.Empty<object>())
             {
                 string k = GetProp<string>(v, "Key"); if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) { entry = v; break; }
             }
         }
         if (entry != null && remove != null) { remove.Invoke(cons, new[]{ entry }); return true; }
     }
     catch { }
     return false;
 }

 // Helper: attempt to read typed value from CustomData entry
 private static object ReadCustomDataValue(object entry)
 {
 if (entry == null) return null;
 try
 {
 // Prefer DataType if available
 var dt = GetMaybe(entry, new[]{ "DataType", "dataType", "Type" })?.ToString();
 if (!string.IsNullOrEmpty(dt))
 {
 switch (dt)
 {
 case "String":
 case "string":
 case "Text":
 {
 var m = DuckovReflectionCache.GetMethod(entry.GetType(), "GetString", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (m != null && m.GetParameters().Length == 0) { var v = m.Invoke(entry, null); if (v != null) return v; }
 break;
 }
 case "Int":
 case "Integer":
 {
 var m = DuckovReflectionCache.GetMethod(entry.GetType(), "GetInt", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (m != null && m.GetParameters().Length == 0) { var v = m.Invoke(entry, null); return v; }
 break;
 }
 case "Float":
 case "Single":
 {
 var m = DuckovReflectionCache.GetMethod(entry.GetType(), "GetFloat", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (m != null && m.GetParameters().Length == 0) { var v = m.Invoke(entry, null); return v; }
 break;
 }
 case "Bool":
 case "Boolean":
 {
 var m = DuckovReflectionCache.GetMethod(entry.GetType(), "GetBool", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (m != null && m.GetParameters().Length == 0) { var v = m.Invoke(entry, null); return v; }
 break;
 }
 }
 }
 // Fallback: try common getters blindly
 var gt = entry.GetType();
 var sM = DuckovReflectionCache.GetMethod(gt, "GetString", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (sM != null && sM.GetParameters().Length == 0)
 {
 var sv = sM.Invoke(entry, null) as string; if (!string.IsNullOrEmpty(sv)) return sv;
 }
 var iM = DuckovReflectionCache.GetMethod(gt, "GetInt", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (iM != null && iM.GetParameters().Length == 0) { var iv = iM.Invoke(entry, null); if (iv != null) return iv; }
 var fM = DuckovReflectionCache.GetMethod(gt, "GetFloat", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (fM != null && fM.GetParameters().Length == 0) { var fv = fM.Invoke(entry, null); if (fv != null) return fv; }
 var bM = DuckovReflectionCache.GetMethod(gt, "GetBool", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (bM != null && bM.GetParameters().Length == 0) { var bv = bM.Invoke(entry, null); if (bv != null) return bv; }
 // Last: raw Value property if any
 var val = GetMaybe(entry, new[]{ "Value", "value" });
 return val;
 }
 catch { return null; }
 }
 }
}
