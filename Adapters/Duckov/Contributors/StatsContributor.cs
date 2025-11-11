using System;
using System.Collections.Generic;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov.Contributors
{
    internal sealed class StatsContributor : IItemStateContributor, IItemStateApplier
    {
        public string Key => "stats";
        public DirtyKind KindMask => DirtyKind.Stats;
        public object TryCapture(object item, ItemSnapshot baseSnapshot)
        {
            try
            {
                if (item == null) return null;
                var statsProp = item.GetType().GetProperty("Stats", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var statsObj = statsProp?.GetValue(item, null);
                if (statsObj == null) return null;
                var list = new List<object>();
                var enumerator = statsObj as System.Collections.IEnumerable;
                if (enumerator == null) return null;
                int count = 0;
                foreach (var s in enumerator)
                {
                    if (s == null) continue;
                    string key = null; float value = 0f;
                    try { var kp = s.GetType().GetProperty("Key", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance); key = kp?.GetValue(s, null) as string; } catch { }
                    try { var vp = s.GetType().GetProperty("Value", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance); var vo = vp?.GetValue(s, null); if (vo is float f) value = f; else if (vo != null) value = (float)Convert.ChangeType(vo, typeof(float)); } catch { }
                    if (string.IsNullOrEmpty(key)) continue;
                    list.Add(new { k = key, v = value });
                    count++; if (count >= 128) break; // safety cap
                }
                if (list.Count == 0) return null;
                return list;
            }
            catch { return null; }
        }

        public void TryApply(object item, ItemMeta meta, Newtonsoft.Json.Linq.JToken fragment)
        {
            try
            {
                if (item == null || fragment == null) return;
                var arr = fragment as Newtonsoft.Json.Linq.JArray; if (arr == null) return;
                var statsProp = item.GetType().GetProperty("Stats", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var statsObj = statsProp?.GetValue(item, null);
                if (statsObj == null) return;
                foreach (var t in arr)
                {
                    try
                    {
                        var key = t["k"].ToString(); var value = (float)t["v"];
                        // ensure stat exists
                        var getStat = statsObj.GetType().GetMethod("GetStat", new[]{ typeof(string) });
                        object stat = getStat?.Invoke(statsObj, new object[]{ key });
                        if (stat == null)
                        {
                            var add = statsObj.GetType().GetMethod("Add", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                            if (add != null)
                            {
                                var paramType = add.GetParameters()[0].ParameterType;
                                var newStat = Activator.CreateInstance(paramType);
                                var keyProp = paramType.GetProperty("Key", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                                keyProp?.SetValue(newStat, key, null);
                                add.Invoke(statsObj, new[]{ newStat });
                                stat = getStat?.Invoke(statsObj, new object[]{ key });
                            }
                        }
                        if (stat != null)
                        {
                            var valProp = stat.GetType().GetProperty("Value", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                            if (valProp != null && valProp.CanWrite)
                            {
                                valProp.SetValue(stat, value, null);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
