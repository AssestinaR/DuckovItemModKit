using System;
using System.Linq;
using System.Reflection;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 物品工厂：负责按 TypeId 实例化/生成、从预制体克隆、注册动态条目、复制与删除物品。
    /// 自动适配 Diablo2Totem 的唯一图腾与 Fallback 预制体解析。
    /// + 2024-Builder: 集成 ItemBuilder 新增的失败时自动生成 stub（模板）物品功能
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

        // ItemBuilder reflection (optional, new game version)
        private static Type s_ItemBuilderType; // Duckov.ItemBuilders.ItemBuilder
        private static MethodInfo s_BuilderNew; // static ItemBuilder New()
        private static MethodInfo s_BuilderTypeId; // ItemBuilder TypeID(int)
        private static MethodInfo s_BuilderDisableStacking; // ItemBuilder DisableStacking()
        private static MethodInfo s_BuilderInstantiate; // ItemBuilder Instantiate()
        private static bool s_BuilderScanned;

        // Marker variable keys for stub items
        private const string VarMissingType = "IMK_MissingType";
        private const string VarOriginalTypeId = "IMK_OriginalTypeId";
        private const string VarBuilderInit = "IMK_BuilderInit";

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
                    // be tolerant to various signatures, prefer any method named AddDynamicEntry
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
                    // out parameter type varies; only match first arg to int
                    foreach (var m in s_FallbackTypeResolver.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        if (m.Name != "TryGetOrCreateFallbackPrefab") continue;
                        var ps = m.GetParameters();
                        if (ps.Length >= 1 && ps[0].ParameterType == typeof(int)) { s_TryGetOrCreateFallbackPrefab = m; break; }
                    }
                }
            }
            EnsureBuilderTypes();
        }

        private static void EnsureBuilderTypes()
        {
            if (s_BuilderScanned) return;
            s_BuilderScanned = true;
            try
            {
                s_ItemBuilderType = DuckovTypeUtils.FindType("Duckov.ItemBuilders.ItemBuilder") ?? DuckovTypeUtils.FindType("ItemBuilder");
                if (s_ItemBuilderType == null) return;
                s_BuilderNew = DuckovReflectionCache.GetMethod(s_ItemBuilderType, "New", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                // Instance fluent methods
                s_BuilderTypeId = DuckovReflectionCache.GetMethod(s_ItemBuilderType, "TypeID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(int) });
                s_BuilderDisableStacking = DuckovReflectionCache.GetMethod(s_ItemBuilderType, "DisableStacking", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes);
                s_BuilderInstantiate = DuckovReflectionCache.GetMethod(s_ItemBuilderType, "Instantiate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes);
            }
            catch { s_ItemBuilderType = null; }
        }

        private static bool BuilderAvailable => s_ItemBuilderType != null && s_BuilderNew != null && s_BuilderInstantiate != null;

        /// <summary>
        /// 尝试创建一个 ItemBuilder 的 Stub 对象（模板物品）
        /// 【在无法解析 TypeId 或实例化 prefab 时自动调用】
        /// </summary>
        private object TryCreateStubWithBuilder(int typeId)
        {
            if (!BuilderAvailable || typeId <= 0) return null;
            try
            {
                var builder = s_BuilderNew.Invoke(null, null);
                if (builder == null) return null;
                try { s_BuilderTypeId?.Invoke(builder, new object[] { typeId }); } catch { }
                try { s_BuilderDisableStacking?.Invoke(builder, null); } catch { }
                var item = s_BuilderInstantiate.Invoke(builder, null);
                if (item == null) return null;
                // Mark stub metadata
                try
                {
                    var adapter = IMKDuckov.Item; // use global facade
                    adapter.SetName(item, "MissingType_" + typeId);
                    adapter.SetDisplayNameRaw(item, "MissingType_" + typeId);
                    adapter.SetTypeId(item, typeId);
                    adapter.SetVariable(item, VarMissingType, true, true);
                    adapter.SetVariable(item, VarOriginalTypeId, typeId, true);
                    adapter.SetVariable(item, VarBuilderInit, true, true);
                }
                catch { }
                try { IMKDuckov.MarkDirty(item, DirtyKind.Core | DirtyKind.Variables); } catch { }
                return item;
            }
            catch (Exception ex) { Log.Warn("ItemBuilder stub create failed: " + ex.Message); return null; }
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
                object obj = null;
                bool instantiateFailed = false;
                try { obj = invoker.Invoke(null, new object[] { typeId }); }
                catch (TargetInvocationException)
                {
                    instantiateFailed = true;
                    // If instantiate failed due to missing prefab, try to register a fallback, then retry once
                    if (s_TryGetOrCreateFallbackPrefab != null)
                    {
                        try
                        {
                            var ps = s_TryGetOrCreateFallbackPrefab.GetParameters();
                            var args = ps.Length == 1 ? new object[] { typeId } : new object[] { typeId, null };
                            var ok = Convert.ToBoolean(s_TryGetOrCreateFallbackPrefab.Invoke(null, args));
                            if (ok)
                            {
                                try { obj = invoker.Invoke(null, new object[] { typeId }); } catch { }
                            }
                        }
                        catch { }
                    }
                }
                if (obj == null && (instantiateFailed || BuilderAvailable))
                {
                    var stub = TryCreateStubWithBuilder(typeId);
                    if (stub != null) return RichResult<object>.Success(stub);
                }
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
                        var ps = s_TryGetOrCreateFallbackPrefab.GetParameters();
                        var args = ps.Length == 1 ? new object[] { typeId } : new object[] { typeId, null };
                        var ok = Convert.ToBoolean(s_TryGetOrCreateFallbackPrefab.Invoke(null, args));
                        if (ok && args.Length >= 2) prefab = args[1];
                        if (!ok) hasPrefab = false; else hasPrefab = true;
                    }
                    catch { }
                }

                // 3) 实例化
                var inst = TryInstantiateByTypeId(typeId);
                if (inst.Ok) return inst;

                // 4) 尝试直接返回 Builder Stub 以应对未知 TypeId
                if (BuilderAvailable)
                {
                    var stub = TryCreateStubWithBuilder(typeId);
                    if (stub != null) return RichResult<object>.Success(stub);
                }

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
