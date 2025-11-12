using System;
using System.Linq;
using System.Reflection;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 物品工厂：负责按 TypeId 实例化/生成、从预制体克隆、注册动态条目、复制与删除物品。
    /// 自动适配 Diablo2Totem 的唯一图腾与 Fallback 预制体解析。
    /// </summary>
    internal sealed class DuckovItemFactory : IItemFactory
    {
        private readonly IItemAdapter _item;
        public DuckovItemFactory(IItemAdapter item) { _item = item; }

        // 反射缓存
        private static Type s_ItemAssetsCollection;
        private static MethodInfo s_InstantiateSync;
        private static MethodInfo s_Instantiate;
        private static MethodInfo s_GetPrefab;
        private static MethodInfo s_AddDynamicEntry;
        private static Type s_ItemType;

        private static Type s_UniqueTotemService;
        private static MethodInfo s_TryGenerateByTypeId;

        private static Type s_FallbackTypeResolver;
        private static MethodInfo s_TryGetOrCreateFallbackPrefab;

        private static void EnsureCoreTypes()
        {
            if (s_ItemAssetsCollection == null)
            {
                s_ItemAssetsCollection = DuckovTypeUtils.FindType("ItemStatsSystem.ItemAssetsCollection") ?? DuckovTypeUtils.FindType("ItemAssetsCollection");
                s_ItemType = DuckovTypeUtils.FindType("ItemStatsSystem.Item") ?? DuckovTypeUtils.FindType("Item");
                if (s_ItemAssetsCollection != null)
                {
                    s_InstantiateSync = DuckovReflectionCache.GetMethod(s_ItemAssetsCollection, "InstantiateSync", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, new[] { typeof(int) });
                    s_Instantiate = DuckovReflectionCache.GetMethod(s_ItemAssetsCollection, "Instantiate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, new[] { typeof(int) });
                    s_GetPrefab = DuckovReflectionCache.GetMethod(s_ItemAssetsCollection, "GetPrefab", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, new[] { typeof(int) });
                    s_AddDynamicEntry = DuckovReflectionCache.GetMethod(s_ItemAssetsCollection, "AddDynamicEntry", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                }
            }
            if (s_UniqueTotemService == null)
            {
                s_UniqueTotemService = DuckovTypeUtils.FindType("Diablo2Totem.Services.UniqueTotemService") ?? DuckovTypeUtils.FindType("DisplayItemValue.Services.UniqueTotemService");
                if (s_UniqueTotemService != null)
                {
                    s_TryGenerateByTypeId = DuckovReflectionCache.GetMethod(s_UniqueTotemService, "TryGenerateByTypeId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, new[] { typeof(int) });
                }
            }
            if (s_FallbackTypeResolver == null)
            {
                s_FallbackTypeResolver = DuckovTypeUtils.FindType("Diablo2Totem.Services.FallbackTypeResolverService") ?? DuckovTypeUtils.FindType("DisplayItemValue.Services.FallbackTypeResolverService");
                if (s_FallbackTypeResolver != null)
                {
                    s_TryGetOrCreateFallbackPrefab = DuckovReflectionCache.GetMethod(s_FallbackTypeResolver, "TryGetOrCreateFallbackPrefab", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, new[] { typeof(int), s_ItemType ?? typeof(object) });
                    if (s_TryGetOrCreateFallbackPrefab == null)
                    {
                        // 兜底：任意两个参数的重载
                        s_TryGetOrCreateFallbackPrefab = DuckovReflectionCache.GetMethod(s_FallbackTypeResolver, "TryGetOrCreateFallbackPrefab", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    }
                }
            }
        }

        /// <summary>通过 TypeId 实例化物品（优先同步 InstantiateSync）。</summary>
        public RichResult<object> TryInstantiateByTypeId(int typeId)
        {
            try
            {
                EnsureCoreTypes();
                if (s_ItemAssetsCollection == null) return RichResult<object>.Fail(ErrorCode.DependencyMissing, "ItemAssetsCollection not found");
                var invoker = s_InstantiateSync ?? s_Instantiate;
                if (invoker == null) return RichResult<object>.Fail(ErrorCode.NotSupported, "Instantiate method not found");
                var obj = invoker.Invoke(null, new object[] { typeId });
                if (obj == null) return RichResult<object>.Fail(ErrorCode.OperationFailed, "Instantiate returned null");
                return RichResult<object>.Success(obj);
            }
            catch (Exception ex) { Log.Error("TryInstantiateByTypeId failed", ex); return RichResult<object>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        /// <summary>从预制体克隆一个实例。</summary>
        public RichResult<object> TryInstantiateFromPrefab(object prefab)
        {
            try
            {
                if (prefab == null) return RichResult<object>.Fail(ErrorCode.InvalidArgument, "prefab is null");
                var uobj = prefab as UnityEngine.Object;
                if (uobj == null) return RichResult<object>.Fail(ErrorCode.InvalidArgument, "prefab not UnityEngine.Object");
                var clone = UnityEngine.Object.Instantiate(uobj);
                return clone != null ? RichResult<object>.Success(clone) : RichResult<object>.Fail(ErrorCode.OperationFailed, "Instantiate returned null");
            }
            catch (Exception ex) { Log.Error("TryInstantiateFromPrefab failed", ex); return RichResult<object>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        /// <summary>
        /// 尝试按 TypeId 生成：
        /// 1) 先走 Unique 路径（若可用）
        /// 2) 若无预制体，尝试通过 Fallback 解析并注册动态条目
        /// 3) 最终调用实例化
        /// </summary>
        public RichResult<object> TryGenerateByTypeId(int typeId)
        {
            try
            {
                EnsureCoreTypes();
                // 1) Unique
                if (s_TryGenerateByTypeId != null)
                {
                    try
                    {
                        var res = s_TryGenerateByTypeId.Invoke(null, new object[] { typeId });
                        if (res != null) return RichResult<object>.Success(res);
                    }
                    catch { }
                }

                // 2) prefab 确认/创建
                object prefab = null;
                bool hasPrefab = false;
                try { var pf = s_GetPrefab?.Invoke(null, new object[] { typeId }); hasPrefab = pf != null; if (hasPrefab) prefab = pf; } catch { }
                if (!hasPrefab && s_TryGetOrCreateFallbackPrefab != null)
                {
                    try
                    {
                        var args = (s_TryGetOrCreateFallbackPrefab.GetParameters().Length >= 2)
                            ? new object[] { typeId, null }
                            : new object[] { typeId };
                        var ok = Convert.ToBoolean(s_TryGetOrCreateFallbackPrefab.Invoke(null, args));
                        if (ok && args.Length >= 2) prefab = args[1];
                        if (ok && prefab != null && s_AddDynamicEntry != null)
                        {
                            try { s_AddDynamicEntry.Invoke(null, new[] { prefab }); } catch { }
                        }
                    }
                    catch { }
                }

                // 3) 实例化
                var inst = TryInstantiateByTypeId(typeId);
                if (inst.Ok) return inst;

                return RichResult<object>.Fail(inst.Code, inst.Error ?? "generate failed");
            }
            catch (Exception ex) { Log.Error("TryGenerateByTypeId failed", ex); return RichResult<object>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        /// <summary>向 ItemAssetsCollection 注册一个动态条目（prefab）。</summary>
        public RichResult TryRegisterDynamicEntry(object prefab)
        {
            try
            {
                EnsureCoreTypes();
                if (prefab == null) return RichResult.Fail(ErrorCode.InvalidArgument, "prefab is null");
                if (s_AddDynamicEntry == null) return RichResult.Fail(ErrorCode.NotSupported, "AddDynamicEntry not found");
                s_AddDynamicEntry.Invoke(null, new[] { prefab });
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryRegisterDynamicEntry failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        /// <summary>克隆一个物品（复制 GameObject 并取同类型组件）。</summary>
        public RichResult<object> TryCloneItem(object item)
        {
            try
            {
                if (item == null) return RichResult<object>.Fail(ErrorCode.InvalidArgument, "item is null");
                var go = (item as UnityEngine.Component)?.gameObject;
                if (go == null) return RichResult<object>.Fail(ErrorCode.InvalidArgument, "item has no gameObject");
                var cloneGo = UnityEngine.Object.Instantiate(go);
                var comp = cloneGo.GetComponent(item.GetType());
                return comp != null ? RichResult<object>.Success(comp) : RichResult<object>.Fail(ErrorCode.OperationFailed, "clone component not found");
            }
            catch (Exception ex) { Log.Error("TryCloneItem failed", ex); return RichResult<object>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        /// <summary>
        /// 删除一个物品：销毁 GameObject 前先强制刷新持久化，避免最后修改丢失。
        /// </summary>
        public RichResult TryDeleteItem(object item)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                // Flush persistence for this item before destruction to avoid losing last modifications
                try { IMKDuckov.FlushDirty(item, force: true); } catch { }
                var comp = item as UnityEngine.Component;
                if (comp == null) return RichResult.Fail(ErrorCode.InvalidArgument, "not a component");
                UnityEngine.Object.DestroyImmediate(comp.gameObject);
                return RichResult.Success();
            }
            catch (Exception ex)
            {
                try { var comp = item as UnityEngine.Component; if (comp != null) UnityEngine.Object.Destroy(comp.gameObject); } catch { }
                Log.Error("TryDeleteItem failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }
    }
}
