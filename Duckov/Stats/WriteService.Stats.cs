using System;
using System.Collections.Concurrent;
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
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly Type[] s_stringArg = { typeof(string) };
        private static readonly Type[] s_floatArg = { typeof(float) };
        private static readonly Type[] s_doubleArg = { typeof(double) };

        private static readonly ConcurrentDictionary<Type, StatsHostAccessPlan> s_statsHostPlans = new ConcurrentDictionary<Type, StatsHostAccessPlan>();
        private static readonly ConcurrentDictionary<Type, StatsCollectionAccessPlan> s_statsCollectionPlans = new ConcurrentDictionary<Type, StatsCollectionAccessPlan>();
        private static readonly ConcurrentDictionary<Type, StatValueWritePlan> s_statValueWritePlans = new ConcurrentDictionary<Type, StatValueWritePlan>();

        private sealed class StatsHostAccessPlan
        {
            public Func<object, object> StatsGetter;
            public MethodInfo CreateStatsComponent;
            public FieldInfo StatsField;
            public Action<object, object> StatsSetter;
        }

        private sealed class StatsCollectionAccessPlan
        {
            public MethodInfo GetByIndexer;
            public MethodInfo GetByKey;
            public MethodInfo AddUntyped;
            public MethodInfo Clear;
            public MethodInfo SetDirty;
            public FieldInfo BackingListField;
        }

        private sealed class StatValueWritePlan
        {
            public Action<object, object> BaseValueSetter;
            public MethodInfo[] ValueMethods;
            public Action<object, object>[] PropertySetters;
            public FieldInfo[] WritableFields;
        }

        private static StatsHostAccessPlan GetStatsHostPlan(Type ownerType)
        {
            return s_statsHostPlans.GetOrAdd(ownerType, static owner => new StatsHostAccessPlan
            {
                StatsGetter = DuckovReflectionCache.GetGetter(owner, "Stats", InstanceFlags),
                CreateStatsComponent = DuckovReflectionCache.GetMethod(owner, "CreateStatsComponent", InstanceFlags, Type.EmptyTypes)
                    ?? DuckovReflectionCache.GetMethod(owner, "CreateStatsComponent", InstanceFlags),
                StatsField = DuckovReflectionCache.GetField(owner, "stats", InstanceFlags),
                StatsSetter = DuckovReflectionCache.GetSetter(owner, "Stats", InstanceFlags),
            });
        }

        private static StatsCollectionAccessPlan GetStatsCollectionPlan(Type statsType)
        {
            return s_statsCollectionPlans.GetOrAdd(statsType, static stats => new StatsCollectionAccessPlan
            {
                GetByIndexer = DuckovReflectionCache.GetMethod(stats, "get_Item", InstanceFlags, s_stringArg),
                GetByKey = DuckovReflectionCache.GetMethod(stats, "GetStat", InstanceFlags, s_stringArg),
                AddUntyped = DuckovReflectionCache.GetMethod(stats, "Add", InstanceFlags),
                Clear = DuckovReflectionCache.GetMethod(stats, "Clear", InstanceFlags),
                SetDirty = DuckovReflectionCache.GetMethod(stats, "SetDirty", InstanceFlags),
                BackingListField = DuckovReflectionCache.GetField(stats, "list", InstanceFlags),
            });
        }

        private static StatValueWritePlan GetStatValueWritePlan(Type statType)
        {
            return s_statValueWritePlans.GetOrAdd(statType, static stat =>
            {
                var methods = new System.Collections.Generic.List<MethodInfo>();
                foreach (var methodName in new[] { "SetBaseValue", "SetValue", "Set" })
                {
                    var method = DuckovReflectionCache.GetMethod(stat, methodName, InstanceFlags, s_floatArg)
                        ?? DuckovReflectionCache.GetMethod(stat, methodName, InstanceFlags, s_doubleArg)
                        ?? DuckovReflectionCache.GetMethod(stat, methodName, InstanceFlags);
                    if (method != null && !methods.Contains(method)) methods.Add(method);
                }

                var setters = new System.Collections.Generic.List<Action<object, object>>();
                foreach (var propertyName in new[] { "BaseValue", "CurrentValue", "Amount", "Value" })
                {
                    var setter = DuckovReflectionCache.GetSetter(stat, propertyName, InstanceFlags);
                    if (setter != null && !setters.Contains(setter)) setters.Add(setter);
                }

                var fields = new System.Collections.Generic.List<FieldInfo>();
                foreach (var fieldName in new[] { "BaseValue", "m_BaseValue", "_baseValue", "Value", "m_Value", "_value" })
                {
                    var field = DuckovReflectionCache.GetField(stat, fieldName, InstanceFlags);
                    if (field != null && !field.IsInitOnly && !fields.Contains(field)) fields.Add(field);
                }

                return new StatValueWritePlan
                {
                    BaseValueSetter = DuckovReflectionCache.GetSetter(stat, "BaseValue", InstanceFlags),
                    ValueMethods = methods.ToArray(),
                    PropertySetters = setters.ToArray(),
                    WritableFields = fields.ToArray(),
                };
            });
        }

        private static object GetStatsHost(object ownerItem)
        {
            if (ownerItem == null) return null;
            var plan = GetStatsHostPlan(ownerItem.GetType());
            return plan.StatsGetter?.Invoke(ownerItem);
        }

        private static object FindStatByKey(object stats, string statKey)
        {
            if (stats == null || string.IsNullOrEmpty(statKey)) return null;

            var plan = GetStatsCollectionPlan(stats.GetType());
            if (plan.GetByIndexer != null)
            {
                var stat = plan.GetByIndexer.Invoke(stats, new object[] { statKey });
                if (stat != null) return stat;
            }

            if (plan.GetByKey != null)
            {
                return plan.GetByKey.Invoke(stats, new object[] { statKey });
            }

            return null;
        }

        private static bool TryWriteStatValueViaPlan(object stat, float value)
        {
            if (stat == null) return false;

            var plan = GetStatValueWritePlan(stat.GetType());
            if (plan.BaseValueSetter != null)
            {
                plan.BaseValueSetter(stat, value);
                return true;
            }

            foreach (var method in plan.ValueMethods)
            {
                if (method == null) continue;
                var parameters = method.GetParameters();
                if (parameters.Length != 1) continue;

                object arg = value;
                try { arg = Convert.ChangeType(value, parameters[0].ParameterType, CultureInfo.InvariantCulture); } catch { }

                try
                {
                    method.Invoke(stat, new[] { arg });
                    return true;
                }
                catch { }
            }

            foreach (var setter in plan.PropertySetters)
            {
                if (setter == null) continue;
                setter(stat, value);
                return true;
            }

            foreach (var field in plan.WritableFields)
            {
                if (field == null) continue;
                try
                {
                    object arg = value;
                    try { arg = Convert.ChangeType(value, field.FieldType, CultureInfo.InvariantCulture); } catch { }
                    field.SetValue(stat, arg);
                    return true;
                }
                catch { }
            }

            return false;
        }

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
                var stats = GetStatsHost(ownerItem);
                if (stats == null) return RichResult.Fail(ErrorCode.NotSupported, "no Stats on owner");
                var stat = FindStatByKey(stats, statKey);
                if (stat == null) return RichResult.Fail(ErrorCode.NotFound, "stat not found");

                if (TryWriteStatValueViaPlan(stat, value))
                {
                    _item.ReapplyModifiers(ownerItem);
                    IMKDuckov.MarkDirty(ownerItem, DirtyKind.Stats);
                    return RichResult.Success();
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
                var stats = GetStatsHost(ownerItem);
                if (stats == null) return RichResult.Fail(ErrorCode.NotSupported, "no Stats on owner");
                var stat = FindStatByKey(stats, statKey);
                if (stat != null)
                {
                    if (initialValue.HasValue)
                    {
                        TrySetStatValue(ownerItem, statKey, initialValue.Value);
                    }
                    IMKDuckov.MarkDirty(ownerItem, DirtyKind.Stats);
                    return RichResult.Success();
                }

                var statsPlan = GetStatsCollectionPlan(stats.GetType());
                var add = statsPlan.AddUntyped;
                if (add == null || add.GetParameters().Length != 1)
                {
                    var fallbackStatType = DuckovTypeUtils.FindType("ItemStatsSystem.Stats.Stat") ?? DuckovTypeUtils.FindType("Stat");
                    if (fallbackStatType == null) return RichResult.Fail(ErrorCode.NotSupported, "Stat type missing");
                    var ns = Activator.CreateInstance(fallbackStatType);
                    TryAssignStatKey(ns, statKey);
                    if (initialValue.HasValue) { TryAssignStatValue(ns, initialValue.Value); }
                    var init0 = DuckovReflectionCache.GetMethod(fallbackStatType, "Initialize", InstanceFlags, new[] { stats.GetType() });
                    init0?.Invoke(ns, new[] { stats });
                    TryAssignStatKey(ns, statKey);
                    var add0 = DuckovReflectionCache.GetMethod(stats.GetType(), "Add", InstanceFlags, new[] { fallbackStatType });
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
                var init = DuckovReflectionCache.GetMethod(statType, "Initialize", InstanceFlags, new[] { stats.GetType() })
                           ?? DuckovReflectionCache.GetMethod(statType, "Initialize", InstanceFlags, Type.EmptyTypes)
                           ?? DuckovReflectionCache.GetMethod(statType, "Initialize", InstanceFlags);
                init?.Invoke(newStat, init != null && init.GetParameters().Length == 0 ? null : new[] { stats });
                TryAssignStatKey(newStat, statKey);
                var addTyped = DuckovReflectionCache.GetMethod(stats.GetType(), "Add", InstanceFlags, new[] { statType }) ?? add;
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
                TryWriteStatValueViaPlan(stat, value);
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
                var stats = GetStatsHost(ownerItem);
                if (stats == null) return RichResult.Fail(ErrorCode.NotSupported, "no Stats on owner");
                var plan = GetStatsCollectionPlan(stats.GetType());
                if (plan.GetByIndexer == null && plan.GetByKey == null) return RichResult.Fail(ErrorCode.NotSupported, "GetStat not found");
                var stat = FindStatByKey(stats, statKey);
                if (stat == null) return RichResult.Fail(ErrorCode.NotFound, "stat not found");
                var remove = DuckovReflectionCache.GetMethod(stats.GetType(), "Remove", InstanceFlags, new[] { stat.GetType() });
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
                var stats = GetStatsHost(ownerItem);
                if (stats == null) return RichResult.Fail(ErrorCode.NotSupported, "no Stats on owner");

                var plan = GetStatsCollectionPlan(stats.GetType());
                var list = plan.BackingListField?.GetValue(stats) as System.Collections.IList;
                if (list == null) return RichResult.Fail(ErrorCode.NotSupported, "Stats backing list not found");
                if (fromIndex < 0 || fromIndex >= list.Count) return RichResult.Fail(ErrorCode.InvalidArgument, "fromIndex out of range");
                if (toIndex < 0 || toIndex >= list.Count) return RichResult.Fail(ErrorCode.InvalidArgument, "toIndex out of range");
                if (fromIndex == toIndex) return RichResult.Success();

                var entry = list[fromIndex];
                list.RemoveAt(fromIndex);
                list.Insert(toIndex, entry);

                plan.SetDirty?.Invoke(stats, null);
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
                var hostPlan = GetStatsHostPlan(ownerType);
                var stats = hostPlan.StatsGetter?.Invoke(ownerItem);
                if (stats != null) return RichResult.Success();

                var create = hostPlan.CreateStatsComponent;
                if (create == null) return RichResult.Fail(ErrorCode.NotSupported, "CreateStatsComponent not found");
                create.Invoke(ownerItem, null);

                stats = hostPlan.StatsGetter?.Invoke(ownerItem);
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
                var hostPlan = GetStatsHostPlan(ownerType);
                var stats = hostPlan.StatsGetter?.Invoke(ownerItem);
                if (stats == null) return RichResult.Success();

                var plan = GetStatsCollectionPlan(stats.GetType());
                plan.Clear?.Invoke(stats, null);

                if (hostPlan.StatsField != null)
                {
                    hostPlan.StatsField.SetValue(ownerItem, null);
                }
                else
                {
                    hostPlan.StatsSetter?.Invoke(ownerItem, null);
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