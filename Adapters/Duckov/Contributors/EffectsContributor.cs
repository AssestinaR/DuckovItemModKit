using System;
using System.Collections.Generic;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov.Contributors
{
    internal sealed class EffectsContributor : IItemStateContributor, IItemStateApplier
    {
        public string Key => "effects";
        public DirtyKind KindMask => DirtyKind.Effects;

        public object TryCapture(object item, ItemSnapshot baseSnapshot)
        {
            try
            {
                if (item == null) return null;
                var effectsProp = item.GetType().GetProperty("Effects", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var listObj = effectsProp?.GetValue(item, null) as System.Collections.IEnumerable;
                if (listObj == null) return null;
                var result = new List<object>(); int count = 0;
                foreach (var e in listObj)
                {
                    if (e == null) continue;
                    var et = e.GetType();
                    string type = et.FullName;
                    bool enabled = false; try { var beh = e as UnityEngine.Behaviour; if (beh) enabled = beh.enabled; } catch { }
                    // capture component type names (triggers/filters/actions) if fields exist
                    string[] triggers = TryListTypeNames(et, e, "triggers");
                    string[] filters  = TryListTypeNames(et, e, "filters");
                    string[] actions  = TryListTypeNames(et, e, "actions");
                    result.Add(new { t = type, en = enabled, tr = triggers, fl = filters, ac = actions });
                    count++; if (count >= 64) break;
                }
                return result.Count == 0 ? null : (object)result;
            }
            catch { return null; }
        }

        private static string[] TryListTypeNames(Type effectType, object effectInstance, string fieldName)
        {
            try
            {
                var f = effectType.GetField(fieldName, System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                var col = f?.GetValue(effectInstance) as System.Collections.IEnumerable; if (col == null) return null;
                var list = new List<string>();
                foreach (var c in col) { if (c == null) continue; list.Add(c.GetType().FullName); if (list.Count >= 16) break; }
                return list.Count == 0 ? null : list.ToArray();
            }
            catch { return null; }
        }

        public void TryApply(object item, ItemMeta meta, Newtonsoft.Json.Linq.JToken fragment)
        {
            try
            {
                if (item == null || fragment == null) return;
                var arr = fragment as Newtonsoft.Json.Linq.JArray; if (arr == null) return;
                var effectsProp = item.GetType().GetProperty("Effects", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var listObj = effectsProp?.GetValue(item, null) as System.Collections.IList;
                if (listObj == null) return;
                foreach (var t in arr)
                {
                    try
                    {
                        var typeName = t["t"].ToString();
                        var enabled = t["en"] != null && (bool)t["en"];
                        var et = DuckovTypeUtils.FindType(typeName);
                        if (et == null) continue;
                        var go = DuckovTypeUtils.GetMaybe(item, new[]{"gameObject"}) as UnityEngine.GameObject;
                        if (go == null) continue;
                        var child = new UnityEngine.GameObject(et.Name);
                        child.hideFlags = UnityEngine.HideFlags.HideInInspector;
                        child.transform.SetParent(go.transform, false);
                        var effect = child.AddComponent(et);
                        // add to list and set item
                        listObj.Add(effect);
                        var setItem = et.GetMethod("SetItem", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                        setItem?.Invoke(effect, new[]{ item });
                        var beh = effect as UnityEngine.Behaviour; if (beh) beh.enabled = enabled;
                        // components
                        TryRecreateComponents(effect, et, t["tr"], "triggers");
                        TryRecreateComponents(effect, et, t["fl"], "filters");
                        TryRecreateComponents(effect, et, t["ac"], "actions");
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void TryRecreateComponents(object effect, Type et, Newtonsoft.Json.Linq.JToken token, string fieldName)
        {
            try
            {
                var list = token as Newtonsoft.Json.Linq.JArray; if (list == null) return;
                var field = et.GetField(fieldName, System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                if (field == null) return;
                var go = DuckovTypeUtils.GetMaybe(effect, new[]{"gameObject"}) as UnityEngine.GameObject;
                if (go == null) return;
                var col = field.GetValue(effect) as System.Collections.IList;
                if (col == null) return;
                foreach (var tn in list)
                {
                    try
                    {
                        var typeName = tn.ToString();
                        var ct = DuckovTypeUtils.FindType(typeName);
                        if (ct == null) continue;
                        var comp = go.AddComponent(ct);
                        col.Add(comp);
                        // set Master if exists
                        try { ct.GetProperty("Master", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)?.SetValue(comp, effect, null); } catch { }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
