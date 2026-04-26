using System;
using System.Collections.Generic;
using System.Globalization;
using ItemModKit.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// Stats 状态贡献者：
    /// 负责扩展块中的 stats 宿主存在性、条目基础值捕获以及加载回放。
    /// </summary>
    internal sealed class StatsContributor : IItemStateContributor, IItemStateApplier
    {
        private const int CurrentSchemaVersion = 1;

        public string Key => "stats";
        public DirtyKind KindMask => DirtyKind.Stats;

        /// <summary>
        /// 从目标物品捕获当前 stats 扩展状态。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <param name="baseSnapshot">当前事务基线快照。</param>
        /// <returns>返回可序列化的 stats 扩展片段；无可捕获状态时返回 null。</returns>
        public object TryCapture(object item, ItemSnapshot baseSnapshot)
        {
            try
            {
                if (item == null) return null;
                var statsObj = GetStatsHost(item);
                var list = new List<object>();
                var enumerator = statsObj as System.Collections.IEnumerable;
                if (enumerator != null)
                {
                    int count = 0;
                    foreach (var s in enumerator)
                    {
                        if (s == null) continue;
                        var key = ReadStatKey(s);
                        if (string.IsNullOrEmpty(key)) continue;
                        list.Add(new { k = key, b = ReadStatBaseValue(s) });
                        count++; if (count >= 128) break;
                    }
                }

                return new
                {
                    sv = CurrentSchemaVersion,
                    h = statsObj != null,
                    e = list
                };
            }
            catch { return null; }
        }

        /// <summary>
        /// 将扩展块中的 stats 状态回放到目标物品。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <param name="meta">当前物品元数据。</param>
        /// <param name="fragment">待应用的 stats 扩展片段。</param>
        public void TryApply(object item, ItemMeta meta, Newtonsoft.Json.Linq.JToken fragment)
        {
            try
            {
                if (item == null || fragment == null) return;
                var hostRequired = true;
                JArray arr;
                if (fragment is JArray legacyArray)
                {
                    arr = legacyArray;
                }
                else
                {
                    var obj = fragment as JObject;
                    if (obj == null) return;
                    hostRequired = obj.Value<bool?>("h") ?? obj["e"] != null;
                    arr = obj["e"] as JArray ?? new JArray();
                }

                if (!hostRequired)
                {
                    TryRemoveStatsHost(item);
                    try { IMKDuckov.Item.ReapplyModifiers(item); } catch { }
                    return;
                }

                var statsObj = GetStatsHost(item) ?? TryCreateStatsHost(item);
                if (statsObj == null) return;

                TryClearStatsHost(statsObj);
                foreach (var t in arr)
                {
                    try
                    {
                        var key = t["k"]?.ToString();
                        if (string.IsNullOrEmpty(key)) continue;
                        var value = ToSingle(t["b"] ?? t["v"]);
                        var stat = EnsureStat(statsObj, key, value);
                        if (stat != null) TryAssignStatBaseValue(stat, value);
                    }
                    catch { }
                }

                TryMarkStatsDirty(statsObj);
                try { IMKDuckov.Item.ReapplyModifiers(item); } catch { }
            }
            catch { }
        }

        /// <summary>
        /// 读取物品当前的 stats 宿主对象。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <returns>存在时返回宿主对象；否则返回 null。</returns>
        private static object GetStatsHost(object item)
        {
            try
            {
                return DuckovReflectionCache.GetGetter(item.GetType(), "Stats", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(item);
            }
            catch { return null; }
        }

        /// <summary>
        /// 尝试为物品创建缺失的 stats 宿主。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <returns>成功返回新宿主对象；失败返回 null。</returns>
        private static object TryCreateStatsHost(object item)
        {
            try
            {
                var ownerType = item.GetType();
                var create = DuckovReflectionCache.GetMethod(ownerType, "CreateStatsComponent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, Type.EmptyTypes)
                             ?? DuckovReflectionCache.GetMethod(ownerType, "CreateStatsComponent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                create?.Invoke(item, null);
                return GetStatsHost(item);
            }
            catch { return null; }
        }

        /// <summary>
        /// 移除物品上的 stats 宿主，并清空已有 stats 条目。
        /// </summary>
        /// <param name="item">目标物品。</param>
        private static void TryRemoveStatsHost(object item)
        {
            try
            {
                var ownerType = item.GetType();
                var stats = GetStatsHost(item);
                if (stats == null) return;

                TryClearStatsHost(stats);

                var statsField = DuckovReflectionCache.GetField(ownerType, "stats", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (statsField != null)
                {
                    statsField.SetValue(item, null);
                }
                else
                {
                    var setter = DuckovReflectionCache.GetSetter(ownerType, "Stats", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    setter?.Invoke(item, null);
                }

                if (stats is Component component)
                {
                    UnityEngine.Object.Destroy(component);
                }
            }
            catch { }
        }

        /// <summary>
        /// 清空 stats 宿主中的全部条目。
        /// </summary>
        /// <param name="statsObj">stats 宿主对象。</param>
        private static void TryClearStatsHost(object statsObj)
        {
            try
            {
                var clear = DuckovReflectionCache.GetMethod(statsObj.GetType(), "Clear", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (clear != null)
                {
                    clear.Invoke(statsObj, null);
                    return;
                }

                var listField = DuckovReflectionCache.GetField(statsObj.GetType(), "list", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var list = listField?.GetValue(statsObj) as System.Collections.IList;
                list?.Clear();
            }
            catch { }
        }

        /// <summary>
        /// 确保 stats 宿主中存在指定 key 的 stat。
        /// </summary>
        /// <param name="statsObj">stats 宿主对象。</param>
        /// <param name="key">目标 stat 键。</param>
        /// <param name="baseValue">缺失时要写入的基础值。</param>
        /// <returns>成功返回目标 stat；失败返回 null。</returns>
        private static object EnsureStat(object statsObj, string key, float baseValue)
        {
            var stat = TryGetStat(statsObj, key);
            if (stat != null) return stat;

            try
            {
                var add = DuckovReflectionCache.GetMethod(statsObj.GetType(), "Add", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (add == null || add.GetParameters().Length != 1)
                {
                    var fallbackStatType = DuckovTypeUtils.FindType("ItemStatsSystem.Stats.Stat") ?? DuckovTypeUtils.FindType("Stat");
                    if (fallbackStatType == null) return null;
                    var newStat = Activator.CreateInstance(fallbackStatType);
                    TryAssignStatKey(newStat, key);
                    TryAssignStatBaseValue(newStat, baseValue);
                    var init = DuckovReflectionCache.GetMethod(fallbackStatType, "Initialize", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, new[] { statsObj.GetType() });
                    init?.Invoke(newStat, new[] { statsObj });
                    TryAssignStatKey(newStat, key);
                    var addTyped = DuckovReflectionCache.GetMethod(statsObj.GetType(), "Add", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, new[] { fallbackStatType });
                    addTyped?.Invoke(statsObj, new[] { newStat });
                    TryAssignStatKey(newStat, key);
                    return TryGetStat(statsObj, key) ?? newStat;
                }

                var statType = add.GetParameters()[0].ParameterType;
                var typedStat = Activator.CreateInstance(statType);
                TryAssignStatKey(typedStat, key);
                TryAssignStatBaseValue(typedStat, baseValue);
                var initTyped = DuckovReflectionCache.GetMethod(statType, "Initialize", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, new[] { statsObj.GetType() })
                                ?? DuckovReflectionCache.GetMethod(statType, "Initialize", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, Type.EmptyTypes)
                                ?? DuckovReflectionCache.GetMethod(statType, "Initialize", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                initTyped?.Invoke(typedStat, initTyped != null && initTyped.GetParameters().Length == 0 ? null : new[] { statsObj });
                TryAssignStatKey(typedStat, key);
                var addTypedMethod = DuckovReflectionCache.GetMethod(statsObj.GetType(), "Add", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, new[] { statType }) ?? add;
                addTypedMethod.Invoke(statsObj, new[] { typedStat });
                TryAssignStatKey(typedStat, key);
                return TryGetStat(statsObj, key) ?? typedStat;
            }
            catch { return null; }
        }

        /// <summary>
        /// 为 stat 实例补写逻辑键。
        /// </summary>
        /// <param name="stat">目标 stat 实例。</param>
        /// <param name="key">期望写入的 stat 键。</param>
        /// <returns>任一路径成功返回 true；否则返回 false。</returns>
        private static bool TryAssignStatKey(object stat, string key)
        {
            try
            {
                return DuckovTypeUtils.TrySetMember(stat, new[] { "Key", "key", "Name", "name" }, key);
            }
            catch { return false; }
        }

        /// <summary>
        /// 从 stats 宿主中按 key 查找 stat。
        /// </summary>
        /// <param name="statsObj">stats 宿主对象。</param>
        /// <param name="key">目标 stat 键。</param>
        /// <returns>找到则返回 stat；否则返回 null。</returns>
        private static object TryGetStat(object statsObj, string key)
        {
            try
            {
                var indexer = DuckovReflectionCache.GetMethod(statsObj.GetType(), "get_Item", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, new[] { typeof(string) });
                var stat = indexer?.Invoke(statsObj, new object[] { key });
                if (stat != null) return stat;

                var getStat = DuckovReflectionCache.GetMethod(statsObj.GetType(), "GetStat", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, new[] { typeof(string) });
                return getStat?.Invoke(statsObj, new object[] { key });
            }
            catch { return null; }
        }

        /// <summary>
        /// 读取 stat 的逻辑键。
        /// </summary>
        /// <param name="stat">目标 stat 实例。</param>
        /// <returns>成功返回键名；失败返回 null。</returns>
        private static string ReadStatKey(object stat)
        {
            try
            {
                return Convert.ToString(DuckovTypeUtils.GetMaybe(stat, new[] { "Key", "key", "Name", "name" }))?.Trim();
            }
            catch { return null; }
        }

        /// <summary>
        /// 读取 stat 的基础值。
        /// </summary>
        /// <param name="stat">目标 stat 实例。</param>
        /// <returns>成功返回基础值；失败返回 0。</returns>
        private static float ReadStatBaseValue(object stat)
        {
            try
            {
                var baseValue = DuckovTypeUtils.GetMaybe(stat, new[] { "BaseValue", "baseValue" });
                if (baseValue != null) return ToSingle(baseValue);
                return ToSingle(DuckovTypeUtils.GetMaybe(stat, new[] { "Value", "value" }));
            }
            catch { return 0f; }
        }

        /// <summary>
        /// 为 stat 实例补写基础值。
        /// </summary>
        /// <param name="stat">目标 stat 实例。</param>
        /// <param name="value">待写入的基础值。</param>
        private static void TryAssignStatBaseValue(object stat, float value)
        {
            try
            {
                var setVal = DuckovReflectionCache.GetSetter(stat.GetType(), "BaseValue", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (setVal != null)
                {
                    setVal(stat, value);
                    return;
                }

                foreach (var mname in new[] { "SetBaseValue", "SetValue", "Set" })
                {
                    var m = DuckovReflectionCache.GetMethod(stat.GetType(), mname, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, new[] { typeof(float) })
                            ?? DuckovReflectionCache.GetMethod(stat.GetType(), mname, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, new[] { typeof(double) })
                            ?? DuckovReflectionCache.GetMethod(stat.GetType(), mname, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (m == null) continue;
                    var ps = m.GetParameters();
                    if (ps.Length != 1) continue;
                    object arg = value;
                    try { arg = Convert.ChangeType(value, ps[0].ParameterType, CultureInfo.InvariantCulture); } catch { }
                    try { m.Invoke(stat, new[] { arg }); return; } catch { }
                }

                foreach (var pname in new[] { "BaseValue", "CurrentValue", "Amount", "Value" })
                {
                    var setter = DuckovReflectionCache.GetSetter(stat.GetType(), pname, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (setter != null)
                    {
                        setter(stat, value);
                        return;
                    }
                }

                foreach (var fname in new[] { "BaseValue", "m_BaseValue", "_baseValue", "Value", "m_Value", "_value" })
                {
                    var f = DuckovReflectionCache.GetField(stat.GetType(), fname, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f == null || f.IsInitOnly) continue;
                    object arg = value;
                    try { arg = Convert.ChangeType(value, f.FieldType, CultureInfo.InvariantCulture); } catch { }
                    try { f.SetValue(stat, arg); return; } catch { }
                }
            }
            catch { }
        }

        /// <summary>
        /// 尝试调用宿主的 SetDirty，通知运行时 stats 已变更。
        /// </summary>
        /// <param name="statsObj">stats 宿主对象。</param>
        private static void TryMarkStatsDirty(object statsObj)
        {
            try
            {
                var setDirty = DuckovReflectionCache.GetMethod(statsObj.GetType(), "SetDirty", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                setDirty?.Invoke(statsObj, null);
            }
            catch { }
        }

        /// <summary>
        /// 将 JSON token 转成单精度浮点数。
        /// </summary>
        /// <param name="token">待转换的 token。</param>
        /// <returns>成功返回转换值；失败返回 0。</returns>
        private static float ToSingle(JToken token)
        {
            try
            {
                if (token == null) return 0f;
                if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer) return token.Value<float>();
                return Convert.ToSingle(token.ToString(), CultureInfo.InvariantCulture);
            }
            catch { return 0f; }
        }

        /// <summary>
        /// 将任意对象尽量转换成单精度浮点数。
        /// </summary>
        /// <param name="value">待转换的值。</param>
        /// <returns>成功返回转换值；失败返回 0。</returns>
        private static float ToSingle(object value)
        {
            try
            {
                if (value == null) return 0f;
                if (value is float f) return f;
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }
            catch { return 0f; }
        }
    }
}