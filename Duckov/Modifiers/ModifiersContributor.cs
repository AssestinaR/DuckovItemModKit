using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ItemModKit.Core;
using Newtonsoft.Json.Linq;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// Modifier 状态贡献者：
    /// 负责扩展块中的 modifier 宿主存在性、启用状态与 description 列表捕获及回放。
    /// </summary>
    internal sealed class ModifiersContributor : IItemStateContributor, IItemStateApplier
    {
        private const int CurrentSchemaVersion = 1;

        /// <summary>扩展块键名：modifiers。</summary>
        public string Key => "modifiers";

        /// <summary>脏标记掩码：仅响应 Modifier 相关写入。</summary>
        public DirtyKind KindMask => DirtyKind.Modifiers;

        /// <summary>
        /// 捕获当前物品上的 modifier 扩展状态。
        /// 包括宿主是否存在、宿主启用状态，以及最多 128 条 description 的最小回放信息。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <param name="baseSnapshot">基础快照；当前仅用于对齐接口签名。</param>
        /// <returns>成功返回可序列化对象；失败时返回 null。</returns>
        public object TryCapture(object item, ItemSnapshot baseSnapshot)
        {
            try
            {
                if (item == null) return null;
                var host = GetModifierHost(item);
                if (host == null)
                {
                    return new { sv = CurrentSchemaVersion, h = false, enabled = true, e = Array.Empty<object>() };
                }

                var entries = new List<object>();
                if (host is System.Collections.IEnumerable enumerable)
                {
                    var count = 0;
                    foreach (var entry in enumerable)
                    {
                        if (entry == null) continue;
                        var key = Convert.ToString(DuckovTypeUtils.GetMaybe(entry, new[] { "Key", "key" }));
                        if (string.IsNullOrWhiteSpace(key)) continue;

                        entries.Add(new
                        {
                            k = key,
                            t = Convert.ToString(DuckovTypeUtils.GetMaybe(entry, new[] { "Type", "type" })),
                            v = DuckovTypeUtils.ConvertToFloat(DuckovTypeUtils.GetMaybe(entry, new[] { "Value", "value" })),
                            o = ToInt(DuckovTypeUtils.GetMaybe(entry, new[] { "Order", "order" })),
                            d = ToBool(DuckovTypeUtils.GetMaybe(entry, new[] { "Display", "display" }), true),
                            target = Convert.ToString(DuckovTypeUtils.GetMaybe(entry, new[] { "Target", "target" })),
                            inv = ToBool(DuckovTypeUtils.GetMaybe(entry, new[] { "EnableInInventory", "enableInInventory" }), false)
                        });
                        count++;
                        if (count >= 128) break;
                    }
                }

                return new
                {
                    sv = CurrentSchemaVersion,
                    h = true,
                    enabled = ToBool(DuckovTypeUtils.GetMaybe(host, new[] { "ModifierEnable", "modifierEnable", "_modifierEnableCache" }), true),
                    e = entries
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 将 modifiers 扩展块回放到目标物品。
        /// 会按快照要求补宿主、清宿主、重建 description 列表并恢复宿主启用状态。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <param name="meta">宿主元信息；当前仅用于对齐接口签名。</param>
        /// <param name="fragment">modifiers 扩展块片段。</param>
        public void TryApply(object item, ItemMeta meta, JToken fragment)
        {
            try
            {
                if (item == null || fragment == null) return;
                var obj = fragment as JObject;
                if (obj == null) return;

                var hostRequired = obj.Value<bool?>("h") ?? false;
                if (!hostRequired)
                {
                    TryRemoveModifierHost(item);
                    return;
                }

                var host = GetModifierHost(item) ?? TryCreateModifierHost(item);
                if (host == null) return;

                TrySetModifierHostMaster(host, item);
                TryClearModifierHost(host);

                var entries = obj["e"] as JArray;
                if (entries != null)
                {
                    foreach (var token in entries.OfType<JObject>())
                    {
                        try
                        {
                            var key = token.Value<string>("k");
                            if (string.IsNullOrWhiteSpace(key)) continue;
                            var inst = CreateModifierDescriptionInstance(host, key, token);
                            if (inst == null) continue;
                            AddDescription(host, inst);
                        }
                        catch
                        {
                        }
                    }
                }

                DuckovTypeUtils.TrySetMember(host, new[] { "ModifierEnable", "modifierEnable", "_modifierEnableCache" }, obj.Value<bool?>("enabled") ?? true);
                TryInvokeModifierReapply(host);
                try { IMKDuckov.Item.ReapplyModifiers(item); } catch { }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 获取物品上的 Modifier 宿主引用。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <returns>存在时返回宿主对象；否则返回 null。</returns>
        private static object GetModifierHost(object item)
        {
            try
            {
                return DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 在目标物品上尝试创建 Modifier 宿主。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <returns>成功返回新宿主；失败返回 null。</returns>
        private static object TryCreateModifierHost(object item)
        {
            try
            {
                var create = DuckovReflectionCache.GetMethod(item.GetType(), "CreateModifiersComponent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)
                             ?? DuckovReflectionCache.GetMethod(item.GetType(), "CreateModifiersComponent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                create?.Invoke(item, null);
                return GetModifierHost(item);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 移除整个 Modifier 宿主。
        /// 在回放扩展块要求“无宿主”时使用。
        /// </summary>
        /// <param name="item">目标物品。</param>
        private static void TryRemoveModifierHost(object item)
        {
            try
            {
                var host = GetModifierHost(item);
                if (host == null) return;
                TryClearModifierHost(host);

                var field = DuckovReflectionCache.GetField(item.GetType(), "modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) field.SetValue(item, null);
                else DuckovReflectionCache.GetSetter(item.GetType(), "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item, null);

                if (host is UnityEngine.Object unityObject)
                {
                    try { UnityEngine.Object.Destroy(unityObject); } catch { }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 维护宿主到所属物品的 Master/master 回链。
        /// </summary>
        /// <param name="host">Modifier 宿主。</param>
        /// <param name="item">所属物品。</param>
        private static void TrySetModifierHostMaster(object host, object item)
        {
            try { DuckovTypeUtils.TrySetMember(host, new[] { "Master", "master" }, item); } catch { }
        }

        /// <summary>
        /// 清空宿主中的全部已有 description。
        /// 以便回放扩展块中的目标状态。
        /// </summary>
        /// <param name="host">Modifier 宿主。</param>
        private static void TryClearModifierHost(object host)
        {
            try
            {
                var clear = DuckovReflectionCache.GetMethod(host.GetType(), "Clear", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                            ?? DuckovReflectionCache.GetMethod(host.GetType(), "ClearModifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                clear?.Invoke(host, null);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 依据扩展块中的单条描述记录创建运行时 ModifierDescription 实例。
        /// </summary>
        /// <param name="host">Modifier 宿主。</param>
        /// <param name="key">描述键。</param>
        /// <param name="token">对应的 JSON 片段。</param>
        /// <returns>成功返回新实例；失败返回 null。</returns>
        private static object CreateModifierDescriptionInstance(object host, string key, JObject token)
        {
            try
            {
                var hostType = host.GetType();
                var descType = hostType.GetGenericArguments().FirstOrDefault() ?? DuckovTypeUtils.FindType("ItemStatsSystem.ModifierDescription");
                if (descType == null) return null;
                var inst = Activator.CreateInstance(descType);

                DuckovTypeUtils.TrySetMember(inst, new[] { "Key", "key" }, key);
                DuckovTypeUtils.TrySetMember(inst, new[] { "Value", "value" }, token.Value<float?>("v") ?? 0f);
                DuckovTypeUtils.TrySetMember(inst, new[] { "Order", "order" }, token.Value<int?>("o") ?? 0);
                DuckovTypeUtils.TrySetMember(inst, new[] { "Display", "display" }, token.Value<bool?>("d") ?? true);
                DuckovTypeUtils.TrySetMember(inst, new[] { "EnableInInventory", "enableInInventory" }, token.Value<bool?>("inv") ?? false);

                var target = token.Value<string>("target");
                if (!string.IsNullOrWhiteSpace(target))
                {
                    TrySetEnumMember(descType, inst, new[] { "Target", "target" }, target);
                }

                var type = token.Value<string>("t");
                if (!string.IsNullOrWhiteSpace(type))
                {
                    TrySetEnumMember(descType, inst, new[] { "Type", "type" }, type);
                }

                return inst;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 将单条 description 加入宿主集合。
        /// </summary>
        /// <param name="host">Modifier 宿主。</param>
        /// <param name="inst">待加入的 description 实例。</param>
        private static void AddDescription(object host, object inst)
        {
            try
            {
                var add = DuckovReflectionCache.GetMethod(host.GetType(), "Add", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { inst.GetType() })
                          ?? DuckovReflectionCache.GetMethod(host.GetType(), "Add", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                add?.Invoke(host, new[] { inst });
            }
            catch
            {
            }
        }

        /// <summary>
        /// 触发宿主侧的 ReapplyModifiers。
        /// 用于让新回放的 description 列表立即生效。
        /// </summary>
        /// <param name="host">Modifier 宿主。</param>
        private static void TryInvokeModifierReapply(object host)
        {
            try
            {
                DuckovReflectionCache.GetMethod(host.GetType(), "ReapplyModifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(host, null);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 尝试把字符串枚举名写入属性或字段。
        /// 失败时回退到通用 TrySetMember 路径。
        /// </summary>
        /// <param name="type">目标运行时类型。</param>
        /// <param name="instance">目标实例。</param>
        /// <param name="names">候选成员名集合。</param>
        /// <param name="raw">待解析的枚举字符串。</param>
        /// <returns>写入成功返回 true；否则返回 false。</returns>
        private static bool TrySetEnumMember(Type type, object instance, string[] names, string raw)
        {
            foreach (var name in names)
            {
                try
                {
                    var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null && prop.PropertyType.IsEnum)
                    {
                        var value = Enum.Parse(prop.PropertyType, raw, true);
                        prop.SetValue(instance, value);
                        return true;
                    }

                    var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null && field.FieldType.IsEnum)
                    {
                        var value = Enum.Parse(field.FieldType, raw, true);
                        field.SetValue(instance, value);
                        return true;
                    }
                }
                catch
                {
                }
            }

            return DuckovTypeUtils.TrySetMember(instance, names, raw);
        }

        /// <summary>
        /// 安全转换为 int；失败时回退为 0。
        /// </summary>
        /// <param name="value">待转换的对象。</param>
        /// <returns>转换后的 int 值。</returns>
        private static int ToInt(object value)
        {
            try { return value == null ? 0 : Convert.ToInt32(value); } catch { return 0; }
        }

        /// <summary>
        /// 安全转换为 bool；失败时回退到给定默认值。
        /// </summary>
        /// <param name="value">待转换的对象。</param>
        /// <param name="fallback">转换失败时的默认值。</param>
        /// <returns>转换后的 bool 值。</returns>
        private static bool ToBool(object value, bool fallback)
        {
            try { return value == null ? fallback : Convert.ToBoolean(value); } catch { return fallback; }
        }
    }
}