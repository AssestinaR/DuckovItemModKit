using System;
using System.Collections.Concurrent;
using System.Reflection;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 写入服务（修饰器/宿主流程）：
    /// 负责 Modifier 宿主的初始化、移除与启停控制。
    /// </summary>
    internal sealed partial class WriteService : IWriteService
    {
        private static readonly ConcurrentDictionary<Type, ModifierHostAccessPlan> s_modifierHostPlans = new ConcurrentDictionary<Type, ModifierHostAccessPlan>();
        private static readonly ConcurrentDictionary<Type, ModifierCollectionAccessPlan> s_modifierCollectionPlans = new ConcurrentDictionary<Type, ModifierCollectionAccessPlan>();

        private sealed class ModifierHostAccessPlan
        {
            public Func<object, object> HostGetter;
            public MethodInfo CreateHost;
            public FieldInfo HostField;
            public Action<object, object> HostSetter;
        }

        private sealed class ModifierCollectionAccessPlan
        {
            public MethodInfo Clear;
            public MethodInfo Reapply;
        }

        private static ModifierHostAccessPlan GetModifierHostPlan(Type itemType)
        {
            return s_modifierHostPlans.GetOrAdd(itemType, static type => new ModifierHostAccessPlan
            {
                HostGetter = DuckovReflectionCache.GetGetter(type, "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                CreateHost = DuckovReflectionCache.GetMethod(type, "CreateModifiersComponent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)
                    ?? DuckovReflectionCache.GetMethod(type, "CreateModifiersComponent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                HostField = DuckovReflectionCache.GetField(type, "modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                HostSetter = DuckovReflectionCache.GetSetter(type, "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
            });
        }

        private static ModifierCollectionAccessPlan GetModifierCollectionPlan(Type hostType)
        {
            return s_modifierCollectionPlans.GetOrAdd(hostType, static type => new ModifierCollectionAccessPlan
            {
                Clear = DuckovReflectionCache.GetMethod(type, "Clear", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? DuckovReflectionCache.GetMethod(type, "ClearModifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                Reapply = DuckovReflectionCache.GetMethod(type, "ReapplyModifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
            });
        }

        /// <summary>
        /// 确保目标物品具备可写的 Modifier 宿主。
        /// 当宿主缺失时，会尝试调用运行时的 CreateModifiersComponent。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryEnsureModifierHost(object item)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");

                var host = GetModifierHost(item);
                if (host != null)
                {
                    TrySetModifierHostMaster(host, item);
                    return RichResult.Success();
                }

                var plan = GetModifierHostPlan(item.GetType());
                var create = plan.CreateHost;
                if (create == null) return RichResult.Fail(ErrorCode.NotSupported, "CreateModifiersComponent not found");

                create.Invoke(item, null);
                host = GetModifierHost(item);
                if (host == null) return RichResult.Fail(ErrorCode.OperationFailed, "modifier host creation failed");

                TrySetModifierHostMaster(host, item);
                TryInvokeModifierReapply(host);
                MarkDirtyFromWriteScope(item, DirtyKind.Modifiers);
                return RichResult.Success();
            }
            catch (Exception ex)
            {
                Log.Error("TryEnsureModifierHost failed", ex);
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>
        /// 移除整个 Modifier 宿主。
        /// 会先清空现有描述，再解除宿主引用并销毁宿主组件。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryRemoveModifierHost(object item)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var host = GetModifierHost(item);
                if (host == null) return RichResult.Success();

                TryClearModifierHost(host);

                var plan = GetModifierHostPlan(item.GetType());
                if (plan.HostField != null)
                {
                    plan.HostField.SetValue(item, null);
                }
                else
                {
                    plan.HostSetter?.Invoke(item, null);
                }

                if (host is UnityEngine.Object unityObject)
                {
                    try { UnityEngine.Object.DestroyImmediate(unityObject); }
                    catch { try { UnityEngine.Object.Destroy(unityObject); } catch { } }
                }

                MarkDirtyFromWriteScope(item, DirtyKind.Modifiers);
                return RichResult.Success();
            }
            catch (Exception ex)
            {
                Log.Error("TryRemoveModifierHost failed", ex);
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>
        /// 启用或禁用 Modifier 宿主。
        /// 该入口对应原版 ModifierDescriptionCollection.ModifierEnable 软开关。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <param name="enabled">是否启用。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TrySetModifierHostEnabled(object item, bool enabled)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var host = GetModifierHost(item);
                if (host == null) return RichResult.Fail(ErrorCode.NotSupported, "Modifiers collection not found");

                if (!DuckovTypeUtils.TrySetMember(host, new[] { "ModifierEnable", "modifierEnable", "_modifierEnableCache" }, enabled))
                {
                    return RichResult.Fail(ErrorCode.NotSupported, "ModifierEnable setter not found");
                }

                TryInvokeModifierReapply(host);
                TryReapplyModifiers(item);
                MarkDirtyFromWriteScope(item, DirtyKind.Modifiers);
                return RichResult.Success();
            }
            catch (Exception ex)
            {
                Log.Error("TrySetModifierHostEnabled failed", ex);
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
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
                if (item == null) return null;
                return GetModifierHostPlan(item.GetType()).HostGetter?.Invoke(item);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 尝试把宿主的 Master/master 回指到当前物品。
        /// 某些原版重算路径依赖这个宿主到物品的回链。
        /// </summary>
        /// <param name="host">Modifier 宿主。</param>
        /// <param name="item">所属物品。</param>
        private static void TrySetModifierHostMaster(object host, object item)
        {
            try { DuckovTypeUtils.TrySetMember(host, new[] { "Master", "master" }, item); } catch { }
        }

        /// <summary>
        /// 清空宿主中的全部 modifier / description 条目。
        /// </summary>
        /// <param name="host">Modifier 宿主。</param>
        private static void TryClearModifierHost(object host)
        {
            try
            {
                GetModifierCollectionPlan(host.GetType()).Clear?.Invoke(host, null);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 触发宿主侧的 ReapplyModifiers。
        /// 这是 modifier 宿主自身的重算入口，不等同于物品侧 Stats 重算。
        /// </summary>
        /// <param name="host">Modifier 宿主。</param>
        private static void TryInvokeModifierReapply(object host)
        {
            try
            {
                GetModifierCollectionPlan(host.GetType()).Reapply?.Invoke(host, null);
            }
            catch
            {
            }
        }
    }
}