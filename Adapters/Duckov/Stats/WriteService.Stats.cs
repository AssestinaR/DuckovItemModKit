using System;
using System.Globalization;
using System.Reflection;
using ItemModKit.Core;
using UnityEngine;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 写入服务（Stats）：
    /// 负责 stats 宿主生命周期、单个 stat 的增删改，以及集合顺序调整。
    /// </summary>
    internal sealed partial class WriteService : IWriteService
    {
        /// <summary>
        /// 设置指定 stat 的基础值。
        /// 优先走 BaseValue setter，失败时再回退到兼容方法、属性或字段写入路径。
        /// </summary>
        /// <param name="ownerItem">目标宿主物品。</param>
        /// <param name="statKey">目标 stat 键。</param>
        /// <param name="value">新的基础值。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TrySetStatValue(object ownerItem, string statKey, float value)
        {
            try
            {
                if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "owner null");
                if (string.IsNullOrEmpty(statKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "statKey null");
                var stats = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Stats", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (stats == null) return RichResult.Fail(ErrorCode.NotSupported, "no Stats on owner");
                object stat = null;
                var indexer = DuckovReflectionCache.GetMethod(stats.GetType(), "get_Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(string) });
                if (indexer != null) { stat = indexer.Invoke(stats, new object[] { statKey }); }
                if (stat == null)
                {
                    var getStat = DuckovReflectionCache.GetMethod(stats.GetType(), "GetStat", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(string) });
                    if (getStat != null) stat = getStat.Invoke(stats, new object[] { statKey });
                }
                if (stat == null) return RichResult.Fail(ErrorCode.NotFound, "stat not found");

                var setVal = DuckovReflectionCache.GetSetter(stat.GetType(), "BaseValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (setVal != null)
                {
                    setVal(stat, value);
                    _item.ReapplyModifiers(ownerItem);
                    IMKDuckov.MarkDirty(ownerItem, DirtyKind.Stats);
                    return RichResult.Success();
                }

                foreach (var mname in new[] { "SetBaseValue", "SetValue", "Set" })
                {
                    var m = DuckovReflectionCache.GetMethod(stat.GetType(), mname, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(float) })
                            ?? DuckovReflectionCache.GetMethod(stat.GetType(), mname, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(double) })
                            ?? DuckovReflectionCache.GetMethod(stat.GetType(), mname, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (m == null) continue;
                    var ps = m.GetParameters();
                    if (ps.Length != 1) continue;
                    object arg = value;
                    try { arg = Convert.ChangeType(value, ps[0].ParameterType, CultureInfo.InvariantCulture); } catch { }
                    try { m.Invoke(stat, new[] { arg }); _item.ReapplyModifiers(ownerItem); IMKDuckov.MarkDirty(ownerItem, DirtyKind.Stats); return RichResult.Success(); } catch { }
                }

                foreach (var pname in new[] { "BaseValue", "CurrentValue", "Amount", "Value" })
                {
                    var setter = DuckovReflectionCache.GetSetter(stat.GetType(), pname, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (setter != null) { setter(stat, value); _item.ReapplyModifiers(ownerItem); IMKDuckov.MarkDirty(ownerItem, DirtyKind.Stats); return RichResult.Success(); }
                }

                foreach (var fname in new[] { "BaseValue", "m_BaseValue", "_baseValue", "Value", "m_Value", "_value" })
                {
                    var f = DuckovReflectionCache.GetField(stat.GetType(), fname, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f == null || f.IsInitOnly) continue;
                    try
                    {
                        object arg = value;
                        try { arg = Convert.ChangeType(value, f.FieldType, CultureInfo.InvariantCulture); } catch { }
                        f.SetValue(stat, arg);
                        _item.ReapplyModifiers(ownerItem);
                        IMKDuckov.MarkDirty(ownerItem, DirtyKind.Stats);
                        return RichResult.Success();
                    }
                    catch { }
                }

                return RichResult.Fail(ErrorCode.NotSupported, "stat.BaseValue setter not found");
            }
            catch (Exception ex) { Log.Error("TrySetStatValue failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        /// <summary>
        /// 确保指定 stat 存在。
        /// 当目标 stat 缺失时，会尝试实例化并加入宿主集合；存在时可按需补写初始值。
        /// </summary>
        /// <param name="ownerItem">目标宿主物品。</param>
        /// <param name="statKey">目标 stat 键。</param>
        /// <param name="initialValue">可选的初始基础值。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryEnsureStat(object ownerItem, string statKey, float? initialValue = null)
        {
            try
            {
                if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "owner null");
                if (string.IsNullOrEmpty(statKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "statKey null");
                var stats = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Stats", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (stats == null) return RichResult.Fail(ErrorCode.NotSupported, "no Stats on owner");
                object stat = null;
                var indexer = DuckovReflectionCache.GetMethod(stats.GetType(), "get_Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(string) });
                if (indexer != null) { stat = indexer.Invoke(stats, new object[] { statKey }); }
                if (stat == null)
                {
                    var getStat = DuckovReflectionCache.GetMethod(stats.GetType(), "GetStat", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(string) });
                    if (getStat != null) stat = getStat.Invoke(stats, new object[] { statKey });
                }
                if (stat != null)
                {
                    if (initialValue.HasValue)
                    {
                        TrySetStatValue(ownerItem, statKey, initialValue.Value);
                    }
                    IMKDuckov.MarkDirty(ownerItem, DirtyKind.Stats);
                    return RichResult.Success();
                }

                var add = DuckovReflectionCache.GetMethod(stats.GetType(), "Add", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (add == null || add.GetParameters().Length != 1)
                {
                    var fallbackStatType = DuckovTypeUtils.FindType("ItemStatsSystem.Stats.Stat") ?? DuckovTypeUtils.FindType("Stat");
                    if (fallbackStatType == null) return RichResult.Fail(ErrorCode.NotSupported, "Stat type missing");
                    var ns = Activator.CreateInstance(fallbackStatType);
                    TryAssignStatKey(ns, statKey);
                    if (initialValue.HasValue) { TryAssignStatValue(ns, initialValue.Value); }
                    var init0 = DuckovReflectionCache.GetMethod(fallbackStatType, "Initialize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { stats.GetType() });
                    init0?.Invoke(ns, new[] { stats });
                    TryAssignStatKey(ns, statKey);
                    var add0 = DuckovReflectionCache.GetMethod(stats.GetType(), "Add", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { fallbackStatType });
                    if (add0 == null) return RichResult.Fail(ErrorCode.NotSupported, "StatCollection.Add not found");
                    add0.Invoke(stats, new[] { ns });
                    TryAssignStatKey(ns, statKey);
                    _item.ReapplyModifiers(ownerItem);
                    IMKDuckov.MarkDirty(ownerItem, DirtyKind.Stats);
                    return RichResult.Success();
                }

                var statType = add.GetParameters()[0].ParameterType;
                var newStat = Activator.CreateInstance(statType);
                TryAssignStatKey(newStat, statKey);
                if (initialValue.HasValue) { TryAssignStatValue(newStat, initialValue.Value); }
                var init = DuckovReflectionCache.GetMethod(statType, "Initialize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { stats.GetType() })
                           ?? DuckovReflectionCache.GetMethod(statType, "Initialize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)
                           ?? DuckovReflectionCache.GetMethod(statType, "Initialize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                init?.Invoke(newStat, init != null && init.GetParameters().Length == 0 ? null : new[] { stats });
                TryAssignStatKey(newStat, statKey);
                var addTyped = DuckovReflectionCache.GetMethod(stats.GetType(), "Add", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { statType }) ?? add;
                addTyped.Invoke(stats, new[] { newStat });
                TryAssignStatKey(newStat, statKey);
                _item.ReapplyModifiers(ownerItem);
                IMKDuckov.MarkDirty(ownerItem, DirtyKind.Stats);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryEnsureStat failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        /// <summary>
        /// 为新建 stat 尝试补写逻辑键。
        /// </summary>
        /// <param name="stat">目标 stat 实例。</param>
        /// <param name="statKey">期望写入的 stat 键。</param>
        /// <returns>任一路径写入成功则返回 true；否则返回 false。</returns>
        private static bool TryAssignStatKey(object stat, string statKey)
        {
            try
            {
                return DuckovTypeUtils.TrySetMember(stat, new[] { "Key", "key", "Name", "name" }, statKey);
            }
            catch { return false; }
        }

        /// <summary>
        /// 为新建 stat 尝试补写基础值。
        /// </summary>
        /// <param name="stat">目标 stat 实例。</param>
        /// <param name="value">期望写入的基础值。</param>
        private static void TryAssignStatValue(object stat, float value)
        {
            try
            {
                var setVal = DuckovReflectionCache.GetSetter(stat.GetType(), "BaseValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (setVal != null) { setVal(stat, value); return; }
                foreach (var mname in new[] { "SetBaseValue", "SetValue", "Set" })
                {
                    var m = DuckovReflectionCache.GetMethod(stat.GetType(), mname, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(float) })
                            ?? DuckovReflectionCache.GetMethod(stat.GetType(), mname, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(double) })
                            ?? DuckovReflectionCache.GetMethod(stat.GetType(), mname, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (m == null) continue;
                    var ps = m.GetParameters(); if (ps.Length != 1) continue;
                    object arg = value; try { arg = Convert.ChangeType(value, ps[0].ParameterType, CultureInfo.InvariantCulture); } catch { }
                    try { m.Invoke(stat, new[] { arg }); return; } catch { }
                }
                foreach (var pname in new[] { "BaseValue", "CurrentValue", "Amount", "Value" })
                {
                    var setter = DuckovReflectionCache.GetSetter(stat.GetType(), pname, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (setter != null) { setter(stat, value); return; }
                }
                foreach (var fname in new[] { "BaseValue", "m_BaseValue", "_baseValue", "Value", "m_Value", "_value" })
                {
                    var f = DuckovReflectionCache.GetField(stat.GetType(), fname, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f == null || f.IsInitOnly) continue;
                    object arg = value; try { arg = Convert.ChangeType(value, f.FieldType, CultureInfo.InvariantCulture); } catch { }
                    try { f.SetValue(stat, arg); return; } catch { }
                }
            }
            catch { }
        }

        /// <summary>
        /// 移除指定 stat。
        /// </summary>
        /// <param name="ownerItem">目标宿主物品。</param>
        /// <param name="statKey">待移除的 stat 键。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryRemoveStat(object ownerItem, string statKey)
        {
            try
            {
                if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "owner null");
                if (string.IsNullOrEmpty(statKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "statKey null");
                var stats = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Stats", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (stats == null) return RichResult.Fail(ErrorCode.NotSupported, "no Stats on owner");
                var getStat = DuckovReflectionCache.GetMethod(stats.GetType(), "GetStat", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(string) });
                if (getStat == null) return RichResult.Fail(ErrorCode.NotSupported, "GetStat not found");
                var stat = getStat.Invoke(stats, new object[] { statKey });
                if (stat == null) return RichResult.Fail(ErrorCode.NotFound, "stat not found");
                var remove = DuckovReflectionCache.GetMethod(stats.GetType(), "Remove", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { stat.GetType() });
                if (remove == null) return RichResult.Fail(ErrorCode.NotSupported, "StatCollection.Remove not found");
                remove.Invoke(stats, new[] { stat });
                _item.ReapplyModifiers(ownerItem);
                IMKDuckov.MarkDirty(ownerItem, DirtyKind.Stats);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryRemoveStat failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        /// <summary>
        /// 调整 stats 集合中的条目顺序。
        /// 此操作仅影响列表顺序语义，不改变 stat 数值本身。
        /// </summary>
        /// <param name="ownerItem">目标宿主物品。</param>
        /// <param name="fromIndex">原始索引。</param>
        /// <param name="toIndex">目标索引。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryMoveStat(object ownerItem, int fromIndex, int toIndex)
        {
            try
            {
                if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "owner null");
                var stats = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Stats", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (stats == null) return RichResult.Fail(ErrorCode.NotSupported, "no Stats on owner");

                var listField = DuckovReflectionCache.GetField(stats.GetType(), "list", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var list = listField?.GetValue(stats) as System.Collections.IList;
                if (list == null) return RichResult.Fail(ErrorCode.NotSupported, "Stats backing list not found");
                if (fromIndex < 0 || fromIndex >= list.Count) return RichResult.Fail(ErrorCode.InvalidArgument, "fromIndex out of range");
                if (toIndex < 0 || toIndex >= list.Count) return RichResult.Fail(ErrorCode.InvalidArgument, "toIndex out of range");
                if (fromIndex == toIndex) return RichResult.Success();

                var entry = list[fromIndex];
                list.RemoveAt(fromIndex);
                list.Insert(toIndex, entry);

                var setDirty = DuckovReflectionCache.GetMethod(stats.GetType(), "SetDirty", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                setDirty?.Invoke(stats, null);
                IMKDuckov.MarkDirty(ownerItem, DirtyKind.Stats);
                return RichResult.Success();
            }
            catch (Exception ex)
            {
                Log.Error("TryMoveStat failed", ex);
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>
        /// 确保目标物品具备可写的 stats 宿主组件。
        /// 当宿主缺失时，会尝试调用运行时的 CreateStatsComponent。
        /// </summary>
        /// <param name="ownerItem">目标宿主物品。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryEnsureStatsHost(object ownerItem)
        {
            try
            {
                if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "owner null");
                var ownerType = ownerItem.GetType();
                var stats = DuckovReflectionCache.GetGetter(ownerType, "Stats", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (stats != null) return RichResult.Success();

                var create = DuckovReflectionCache.GetMethod(ownerType, "CreateStatsComponent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)
                             ?? DuckovReflectionCache.GetMethod(ownerType, "CreateStatsComponent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (create == null) return RichResult.Fail(ErrorCode.NotSupported, "CreateStatsComponent not found");
                create.Invoke(ownerItem, null);

                stats = DuckovReflectionCache.GetGetter(ownerType, "Stats", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (stats == null) return RichResult.Fail(ErrorCode.OperationFailed, "stats host creation failed");

                IMKDuckov.MarkDirty(ownerItem, DirtyKind.Stats);
                return RichResult.Success();
            }
            catch (Exception ex)
            {
                Log.Error("TryEnsureStatsHost failed", ex);
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>
        /// 移除整个 stats 宿主。
        /// 会先清空现有 stats，再解除宿主引用并销毁宿主组件。
        /// </summary>
        /// <param name="ownerItem">目标宿主物品。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryRemoveStatsHost(object ownerItem)
        {
            try
            {
                if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "owner null");
                var ownerType = ownerItem.GetType();
                var stats = DuckovReflectionCache.GetGetter(ownerType, "Stats", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (stats == null) return RichResult.Success();

                var clear = DuckovReflectionCache.GetMethod(stats.GetType(), "Clear", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                clear?.Invoke(stats, null);

                var statsField = DuckovReflectionCache.GetField(ownerType, "stats", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (statsField != null)
                {
                    statsField.SetValue(ownerItem, null);
                }
                else
                {
                    var setter = DuckovReflectionCache.GetSetter(ownerType, "Stats", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    setter?.Invoke(ownerItem, null);
                }

                if (stats is Component component)
                {
                    UnityEngine.Object.Destroy(component);
                }

                IMKDuckov.MarkDirty(ownerItem, DirtyKind.Stats);
                return RichResult.Success();
            }
            catch (Exception ex)
            {
                Log.Error("TryRemoveStatsHost failed", ex);
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }
    }
}