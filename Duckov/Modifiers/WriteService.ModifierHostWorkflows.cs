using System;
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

                var create = DuckovReflectionCache.GetMethod(item.GetType(), "CreateModifiersComponent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)
                             ?? DuckovReflectionCache.GetMethod(item.GetType(), "CreateModifiersComponent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
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

                var field = DuckovReflectionCache.GetField(item.GetType(), "modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(item, null);
                }
                else
                {
                    var setter = DuckovReflectionCache.GetSetter(item.GetType(), "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    setter?.Invoke(item, null);
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
                return DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item);
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
                var clear = DuckovReflectionCache.GetMethod(host.GetType(), "Clear", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                            ?? DuckovReflectionCache.GetMethod(host.GetType(), "ClearModifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                clear?.Invoke(host, null);
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
                var reapply = DuckovReflectionCache.GetMethod(host.GetType(), "ReapplyModifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                reapply?.Invoke(host, null);
            }
            catch
            {
            }
        }
    }
}