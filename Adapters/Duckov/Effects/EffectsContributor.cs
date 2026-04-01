using System;
using System.Collections.Generic;
using ItemModKit.Core;
using Newtonsoft.Json.Linq;

namespace ItemModKit.Adapters.Duckov.Contributors
{
    internal sealed class EffectsContributor : IItemStateContributor, IItemStateApplier
    {
        private sealed class PersistedEffectComponent
        {
            public string k { get; set; }
            public string t { get; set; }
            public Dictionary<string, object> p { get; set; }
        }

        private sealed class PersistedEffectEntry
        {
            public string t { get; set; }
            public string n { get; set; }
            public bool en { get; set; }
            public bool? d { get; set; }
            public string desc { get; set; }
            public List<PersistedEffectComponent> c { get; set; }
        }

        public string Key => "effects";
        public DirtyKind KindMask => DirtyKind.Effects;

        public object TryCapture(object item, ItemSnapshot baseSnapshot)
        {
            try
            {
                if (item == null) return null;
                var effectsProp = item.GetType().GetProperty("Effects", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var listObj = effectsProp?.GetValue(item, null) as System.Collections.IEnumerable;
                if (listObj == null) return null;
                var result = new List<PersistedEffectEntry>(); int count = 0;
                foreach (var e in listObj)
                {
                    if (e == null) continue;
                    var et = e.GetType();
                    string type = et.FullName;
                    bool enabled = false; try { var beh = e as UnityEngine.Behaviour; if (beh) enabled = beh.enabled; } catch { }
                    string name = null; try { var go = DuckovTypeUtils.GetMaybe(e, new[] { "gameObject" }) as UnityEngine.GameObject; if (go != null) name = go.name; } catch { }
                    bool? display = null; try { var value = DuckovTypeUtils.GetMaybe(e, new[] { "display", "Display" }); if (value != null) display = Convert.ToBoolean(value); } catch { }
                    string description = null; try { var value = DuckovTypeUtils.GetMaybe(e, new[] { "description", "Description" }); if (value != null) description = Convert.ToString(value); } catch { }
                    var components = new List<PersistedEffectComponent>();
                    CaptureComponents(components, et, e, "triggers", "Trigger");
                    CaptureComponents(components, et, e, "filters", "Filter");
                    CaptureComponents(components, et, e, "actions", "Action");
                    result.Add(new PersistedEffectEntry { t = type, n = name, en = enabled, d = display, desc = description, c = components });
                    count++; if (count >= 64) break;
                }
                return result;
            }
            catch { return null; }
        }

        private static void CaptureComponents(List<PersistedEffectComponent> result, Type effectType, object effectInstance, string fieldName, string kind)
        {
            try
            {
                var f = effectType.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var col = f?.GetValue(effectInstance) as System.Collections.IEnumerable; if (col == null) return;
                int count = 0;
                foreach (var c in col)
                {
                    if (c == null) continue;
                    result.Add(new PersistedEffectComponent { k = kind, t = c.GetType().FullName, p = DuckovEffectSchemaSupport.CapturePrimitiveMembers(c) });
                    count++;
                    if (count >= 16) break;
                }
            }
            catch { }
        }

        public void TryApply(object item, ItemMeta meta, Newtonsoft.Json.Linq.JToken fragment)
        {
            try
            {
                if (item == null || fragment == null) return;
                var arr = fragment as Newtonsoft.Json.Linq.JArray; if (arr == null) return;
                var effectsProp = item.GetType().GetProperty("Effects", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var listObj = effectsProp?.GetValue(item, null) as System.Collections.IList;
                if (listObj == null) return;
                ClearCurrentEffects(listObj);
                foreach (var t in arr)
                {
                    try
                    {
                        var typeName = t["t"].ToString();
                        var enabled = t["en"] != null && (bool)t["en"];
                        var et = DuckovTypeUtils.FindType(typeName);
                        if (et == null) continue;
                        var go = DuckovTypeUtils.GetMaybe(item, new[] { "gameObject" }) as UnityEngine.GameObject;
                        if (go == null) continue;
                        var child = new UnityEngine.GameObject(et.Name);
                        child.hideFlags = UnityEngine.HideFlags.HideInInspector;
                        child.transform.SetParent(go.transform, false);
                        var effect = child.AddComponent(et);
                        var name = t["n"]?.ToString();
                        if (!string.IsNullOrEmpty(name)) child.name = name;
                        // add to list and set item
                        listObj.Add(effect);
                        var setItem = et.GetMethod("SetItem", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        setItem?.Invoke(effect, new[] { item });
                        var beh = effect as UnityEngine.Behaviour; if (beh) beh.enabled = enabled;
                        TryApplyEffectProperties(effect, t);
                        TryRecreateComponents(effect, et, t["c"] as JArray);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void ClearCurrentEffects(System.Collections.IList listObj)
        {
            try
            {
                for (var index = listObj.Count - 1; index >= 0; index--)
                {
                    var effect = listObj[index] as UnityEngine.Component;
                    listObj.RemoveAt(index);
                    if (effect == null)
                    {
                        continue;
                    }

                    try { UnityEngine.Object.DestroyImmediate(effect.gameObject); }
                    catch { try { UnityEngine.Object.Destroy(effect.gameObject); } catch { } }
                }
            }
            catch { }
        }

        private static void TryApplyEffectProperties(object effect, JToken token)
        {
            try
            {
                if (token == null) return;
                TryAssignMember(effect, "display", token["d"]);
                TryAssignMember(effect, "description", token["desc"]);
            }
            catch { }
        }

        private static void TryRecreateComponents(object effect, Type et, JArray list)
        {
            try
            {
                var go = DuckovTypeUtils.GetMaybe(effect, new[] { "gameObject" }) as UnityEngine.GameObject;
                if (go == null) return;
                if (list == null) return;
                foreach (var entry in list)
                {
                    try
                    {
                        var kind = entry["k"]?.ToString();
                        var typeName = entry["t"]?.ToString();
                        if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(typeName)) continue;
                        var ct = DuckovTypeUtils.FindType(typeName);
                        if (ct == null) continue;
                        var fieldName = kind == "Trigger" ? "triggers" : kind == "Filter" ? "filters" : "actions";
                        var field = et.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field == null) continue;
                        var col = field.GetValue(effect) as System.Collections.IList;
                        if (col == null) continue;
                        var comp = go.AddComponent(ct);
                        col.Add(comp);
                        // set Master if exists
                        try { ct.GetProperty("Master", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(comp, effect, null); } catch { }
                        DuckovEffectSchemaSupport.TryAssignMembers(comp, entry["p"] as JObject);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void TryAssignMember(object target, string memberName, JToken token)
        {
            try
            {
                if (target == null || token == null || string.IsNullOrEmpty(memberName)) return;
                DuckovEffectSchemaSupport.TryAssignMember(target, memberName, token);
            }
            catch { }
        }
    }
}
