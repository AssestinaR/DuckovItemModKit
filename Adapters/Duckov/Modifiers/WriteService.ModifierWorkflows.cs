using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 写入服务（修饰器/工作流）：
    /// 负责原始 modifier 增删重算，以及 modifier description 的增删改清理流程。
    /// </summary>
    internal sealed partial class WriteService : IWriteService
    {
        /// <summary>
        /// 为目标 stat 添加一个运行时 modifier。
        /// 优先尝试原生 AddModifier 签名，失败时再回退到 modifier 对象构造路径。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <param name="statKey">要作用的 stat 键。</param>
        /// <param name="value">modifier 数值。</param>
        /// <param name="isPercent">是否按百分比语义写入。</param>
        /// <param name="type">期望的 modifier 类型名。</param>
        /// <param name="source">可选的 modifier 来源对象。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryAddModifier(object item, string statKey, float value, bool isPercent = false, string type = null, object source = null)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                if (string.IsNullOrEmpty(statKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "statKey is null");

                EnsureModifierModeProbed(item);
                if (s_descriptionsOnly)
                {
                    return TryAddModifierDescription(item, statKey, type ?? "Add", value, true, 0, null);
                }

                if (Verbose) DebugLog("TryAddModifier begin key=" + statKey + " value=" + value + " type=" + type + " percent=" + isPercent);
                TryEnsureStat(item, statKey, value);

                var sig = ResolveAddModifierSignatureCached(item.GetType());
                var applied = false;
                if (sig.addNum != null)
                {
                    var args = sig.addNum.GetParameters().Length == 3
                        ? new object[] { statKey, value, isPercent }
                        : new object[] { statKey, value };
                    try
                    {
                        var okObj = sig.addNum.Invoke(item, args);
                        applied = !(okObj is bool) || (okObj is bool b && b);
                    }
                    catch
                    {
                        applied = false;
                    }
                }

                if (!applied && sig.addObj != null && sig.modifierType != null)
                {
                    object enumVal = null;
                    if (sig.enumType != null)
                    {
                        try
                        {
                            var names = DuckovReflectionCache.GetEnumNames(sig.enumType);
                            var mapped = MapModifierEnumName(type, names);
                            enumVal = Enum.Parse(sig.enumType, mapped);
                        }
                        catch
                        {
                            enumVal = sig.enumType.IsEnum ? Enum.GetValues(sig.enumType).GetValue(0) : null;
                        }
                    }

                    var mod = CreateModifierInstance(sig.modifierType, sig.enumType, value, enumVal, source);
                    if (mod != null)
                    {
                        try
                        {
                            var okObj = sig.addObj.Invoke(item, new object[] { statKey, mod });
                            applied = !(okObj is bool) || (okObj is bool b && b);
                        }
                        catch
                        {
                            applied = false;
                        }

                        if (!applied && Verbose)
                        {
                            try
                            {
                                var statsObj = DuckovReflectionCache.GetGetter(item.GetType(), "Stats", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item);
                                object stat = null;
                                var indexer = DuckovReflectionCache.GetMethod(statsObj?.GetType(), "get_Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(string) });
                                if (indexer != null) stat = indexer.Invoke(statsObj, new object[] { statKey });
                                if (stat == null)
                                {
                                    var getStat = DuckovReflectionCache.GetMethod(statsObj?.GetType(), "GetStat", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(string) });
                                    if (getStat != null) stat = getStat.Invoke(statsObj, new object[] { statKey });
                                }

                                if (stat != null)
                                {
                                    var statAdd = DuckovReflectionCache.GetMethod(stat.GetType(), "AddModifier", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { sig.modifierType })
                                                 ?? DuckovReflectionCache.GetMethod(stat.GetType(), "AddModifier", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (statAdd != null)
                                    {
                                        statAdd.Invoke(stat, new object[] { mod });
                                        applied = true;
                                    }
                                }
                            }
                            catch
                            {
                                applied = false;
                            }
                        }
                    }
                }

                if (!applied) return RichResult.Fail(ErrorCode.OperationFailed, "AddModifier failed");
                TryReapplyModifiers(item);
                MarkDirtyFromWriteScope(item, DirtyKind.Modifiers);
                return RichResult.Success();
            }
            catch (Exception ex)
            {
                Log.Error("TryAddModifier failed", ex);
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>
        /// 按 source 移除物品上的全部 modifier。
        /// 优先调用宿主原生 RemoveAllModifiersFrom，失败时再回退到集合遍历删除。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <param name="source">待清理的 source 对象。</param>
        /// <returns>成功返回删除数量；失败时返回对应错误码与错误信息。</returns>
        public RichResult<int> TryRemoveAllModifiersFromSource(object item, object source)
        {
            try
            {
                if (item == null) return RichResult<int>.Fail(ErrorCode.InvalidArgument, "item is null");

                var rem = DuckovReflectionCache.GetMethod(item.GetType(), "RemoveAllModifiersFrom", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (rem != null)
                {
                    try
                    {
                        var r = rem.Invoke(item, new object[] { source });
                        var count = r is int i ? i : 0;
                        TryReapplyModifiers(item);
                        MarkDirtyFromWriteScope(item, DirtyKind.Modifiers);
                        return RichResult<int>.Success(count);
                    }
                    catch (Exception ex)
                    {
                        return RichResult<int>.Fail(ErrorCode.OperationFailed, ex.Message);
                    }
                }

                var removed = 0;
                var modsCol = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item);
                if (modsCol == null) return RichResult<int>.Fail(ErrorCode.NotSupported, "Modifiers collection not found");

                var list = modsCol as System.Collections.IEnumerable;
                var toRemove = new List<object>();
                if (list != null)
                {
                    foreach (var m in list)
                    {
                        if (m == null) continue;
                        object src = null;
                        try { src = DuckovTypeUtils.GetMaybe(m, new[] { "Source", "source" }); } catch { }
                        if (ReferenceEquals(src, source)) toRemove.Add(m);
                    }
                }

                var remove = DuckovReflectionCache.GetMethod(modsCol.GetType(), "Remove", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                           ?? DuckovReflectionCache.GetMethod(modsCol.GetType(), "RemoveModifier", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var m in toRemove)
                {
                    try
                    {
                        remove?.Invoke(modsCol, new[] { m });
                        removed++;
                    }
                    catch
                    {
                    }
                }

                TryReapplyModifiers(item);
                MarkDirtyFromWriteScope(item, DirtyKind.Modifiers);
                return RichResult<int>.Success(removed);
            }
            catch (Exception ex)
            {
                Log.Error("TryRemoveAllModifiersFromSource failed", ex);
                return RichResult<int>.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>
        /// 触发目标物品上的 modifier 重算。
        /// 当物品当前没有 Stats 宿主时，直接按 no-op 成功返回。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryReapplyModifiers(object item)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var statsIsNull = false;
                try
                {
                    var p = item.GetType().GetProperty("Stats", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var stats = p?.GetValue(item, null);
                    statsIsNull = stats == null;
                }
                catch
                {
                }

                if (statsIsNull) return RichResult.Success();
                _item.ReapplyModifiers(item);
                return RichResult.Success();
            }
            catch (Exception ex)
            {
                Log.Error("TryReapplyModifiers failed", ex);
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>
        /// 新增或更新一个 modifier description。
        /// 若同 key 描述已存在，则按 upsert 语义更新其值、类型、显示与排序信息。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <param name="key">描述绑定的 stat 键。</param>
        /// <param name="type">描述类型名。</param>
        /// <param name="value">描述数值。</param>
        /// <param name="display">是否显示在 UI 中。</param>
        /// <param name="order">排序值。</param>
        /// <param name="target">可选目标对象语义。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryAddModifierDescription(object item, string key, string type, float value, bool? display = null, int? order = null, string target = null)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                if (string.IsNullOrEmpty(key)) return RichResult.Fail(ErrorCode.InvalidArgument, "key is empty");

                DebugLog("TryAddModifierDescription begin key=" + key + " value=" + value + " type=" + type);
                var ensureHost = TryEnsureModifierHost(item);
                if (!ensureHost.Ok) return ensureHost;
                var modsCol = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item);
                if (modsCol == null) return RichResult.Fail(ErrorCode.NotSupported, "Modifiers collection not found");

                var existing = (modsCol as System.Collections.IEnumerable)?.Cast<object>()
                    .FirstOrDefault(d => string.Equals(Convert.ToString(DuckovTypeUtils.GetMaybe(d, new[] { "Key", "key" })), key, StringComparison.Ordinal));
                if (existing != null)
                {
                    InternalSetDescField(item, key, "Value", value);
                    if (!string.IsNullOrEmpty(type)) TrySetModifierDescriptionType(item, key, type);
                    if (display.HasValue) InternalSetDescField(item, key, "Display", display.Value);
                    if (order.HasValue) InternalSetDescField(item, key, "Order", order.Value);
                    MarkDirtyFromWriteScope(item, DirtyKind.Modifiers);
                    return RichResult.Success();
                }

                var descType = modsCol.GetType().GetGenericArguments().FirstOrDefault() ?? DuckovTypeUtils.FindType("ItemStatsSystem.ModifierDescription");
                if (descType == null) return RichResult.Fail(ErrorCode.NotSupported, "ModifierDescription type missing");

                var inst = Activator.CreateInstance(descType);
                var keyOk = TrySetByNames(descType, inst, new[] { "Key", "key", "Name" }, key);
                var valOk = TrySetByNames(descType, inst, new[] { "Value", "value", "Amount" }, value);
                var dispOk = display.HasValue
                    ? TrySetByNames(descType, inst, new[] { "Display", "display" }, display.Value)
                    : TrySetByNames(descType, inst, new[] { "Display", "display" }, true);
                if (order.HasValue) TrySetByNames(descType, inst, new[] { "Order", "order", "Index" }, order.Value);
                if (!string.IsNullOrEmpty(target)) TrySetByNames(descType, inst, new[] { "Target", "target" }, target);
                if (!string.IsNullOrEmpty(type)) TrySetEnumByAny(descType, inst, type);
                if (!keyOk || !valOk || !dispOk) DebugDumpMembers(descType);

                var addDesc = DuckovReflectionCache.GetMethod(modsCol.GetType(), "Add", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { descType })
                              ?? DuckovReflectionCache.GetMethod(modsCol.GetType(), "Add", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                addDesc?.Invoke(modsCol, new[] { inst });

                var reapply = DuckovReflectionCache.GetMethod(modsCol.GetType(), "ReapplyModifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                reapply?.Invoke(modsCol, null);
                TryReapplyModifiers(item);
                MarkDirtyFromWriteScope(item, DirtyKind.Modifiers);
                return RichResult.Success();
            }
            catch (Exception ex)
            {
                Log.Error("TryAddModifierDescription failed", ex);
                DebugLog("TryAddModifierDescription exception: " + ex.Message);
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>
        /// 删除指定 key 的 modifier description。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <param name="key">待删除的描述键。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryRemoveModifierDescription(object item, string key)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var modsCol = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item);
                if (modsCol == null) return RichResult.Fail(ErrorCode.NotSupported, "Modifiers collection not found");

                object targetDesc = null;
                foreach (var d in (modsCol as System.Collections.IEnumerable) ?? Array.Empty<object>())
                {
                    if (d == null) continue;
                    var k = Convert.ToString(DuckovTypeUtils.GetMaybe(d, new[] { "Key", "key" }));
                    if (string.Equals(k, key, StringComparison.Ordinal))
                    {
                        targetDesc = d;
                        break;
                    }
                }

                if (targetDesc == null) return RichResult.Fail(ErrorCode.NotFound, "description not found");
                var remove = DuckovReflectionCache.GetMethod(modsCol.GetType(), "Remove", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { targetDesc.GetType() })
                              ?? DuckovReflectionCache.GetMethod(modsCol.GetType(), "Remove", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                remove?.Invoke(modsCol, new[] { targetDesc });

                var reapply = DuckovReflectionCache.GetMethod(modsCol.GetType(), "ReapplyModifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                reapply?.Invoke(modsCol, null);
                TryReapplyModifiers(item);
                MarkDirtyFromWriteScope(item, DirtyKind.Modifiers);
                return RichResult.Success();
            }
            catch (Exception ex)
            {
                Log.Error("TryRemoveModifierDescription failed", ex);
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>
        /// 更新指定 modifier description 的数值字段。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <param name="key">目标描述键。</param>
        /// <param name="value">新的数值。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TrySetModifierDescriptionValue(object item, string key, float value)
        {
            DebugLog("SetDescValue key=" + key + " value=" + value);
            return InternalSetDescField(item, key, "Value", value);
        }

        /// <summary>
        /// 更新指定 modifier description 的类型字段。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <param name="key">目标描述键。</param>
        /// <param name="type">新的类型名。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TrySetModifierDescriptionType(object item, string key, string type)
        {
            DebugLog("SetDescType key=" + key + " type=" + type);
            return TrySetModifierDescriptionTypeInternal(item, key, type);
        }

        /// <summary>
        /// 更新指定 modifier description 的排序值。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <param name="key">目标描述键。</param>
        /// <param name="order">新的排序值。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TrySetModifierDescriptionOrder(object item, string key, int order)
        {
            DebugLog("SetDescOrder key=" + key + " order=" + order);
            return InternalSetDescField(item, key, "Order", order);
        }

        /// <summary>
        /// 更新指定 modifier description 的显示标记。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <param name="key">目标描述键。</param>
        /// <param name="display">新的显示状态。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TrySetModifierDescriptionDisplay(object item, string key, bool display)
        {
            DebugLog("SetDescDisplay key=" + key + " display=" + display);
            return InternalSetDescField(item, key, "Display", display);
        }

        /// <summary>
        /// 更新指定 modifier description 的目标语义。
        /// 支持 Self、Parent、Character 三种原版目标枚举名称。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <param name="key">目标描述键。</param>
        /// <param name="target">新的目标名。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TrySetModifierDescriptionTarget(object item, string key, string target)
        {
            DebugLog("SetDescTarget key=" + key + " target=" + target);
            return InternalSetDescField(item, key, "Target", target);
        }

        /// <summary>
        /// 更新指定 modifier description 是否允许在背包中生效。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <param name="key">目标描述键。</param>
        /// <param name="enabled">新的背包生效开关。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TrySetModifierDescriptionEnableInInventory(object item, string key, bool enabled)
        {
            DebugLog("SetDescEnableInInventory key=" + key + " enabled=" + enabled);
            return InternalSetDescField(item, key, "EnableInInventory", enabled);
        }

        /// <summary>
        /// 清空物品上的全部 modifier descriptions。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryClearModifierDescriptions(object item)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var modsCol = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item);
                if (modsCol == null) return RichResult.Fail(ErrorCode.NotSupported, "Modifiers collection not found");

                var clear = DuckovReflectionCache.GetMethod(modsCol.GetType(), "Clear", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                            ?? DuckovReflectionCache.GetMethod(modsCol.GetType(), "ClearModifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                clear?.Invoke(modsCol, null);
                TryReapplyModifiers(item);
                MarkDirtyFromWriteScope(item, DirtyKind.Modifiers);
                return RichResult.Success();
            }
            catch (Exception ex)
            {
                Log.Error("TryClearModifierDescriptions failed", ex);
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>
        /// 清理异常 modifier descriptions。
        /// 当前策略会删除空 key 条目，并对重复 key 仅保留最后一项。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TrySanitizeModifierDescriptions(object item)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var modsColObj = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item);
                var modsCol = modsColObj as System.Collections.IEnumerable;
                if (modsCol == null) return RichResult.Fail(ErrorCode.NotSupported, "Modifiers collection not found");

                var remove = DuckovReflectionCache.GetMethod(modsColObj.GetType(), "Remove", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                             ?? DuckovReflectionCache.GetMethod(modsColObj.GetType(), "RemoveModifier", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var groups = modsCol.Cast<object>()
                    .Where(d => d != null)
                    .GroupBy(d => Convert.ToString(DuckovTypeUtils.GetMaybe(d, new[] { "Key", "key" })) ?? string.Empty);
                foreach (var g in groups)
                {
                    var list = g.ToList();
                    if (string.IsNullOrEmpty(g.Key))
                    {
                        foreach (var d in list)
                        {
                            try { remove?.Invoke(modsColObj, new[] { d }); } catch { }
                        }

                        continue;
                    }

                    for (var i = 0; i < list.Count - 1; i++)
                    {
                        try { remove?.Invoke(modsColObj, new[] { list[i] }); } catch { }
                    }
                }

                TryReapplyModifiers(item);
                MarkDirtyFromWriteScope(item, DirtyKind.Modifiers);
                return RichResult.Success();
            }
            catch (Exception ex)
            {
                Log.Error("TrySanitizeModifierDescriptions failed", ex);
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }
    }
}