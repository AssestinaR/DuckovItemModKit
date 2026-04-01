using System;
using System.Collections.Generic;
using ItemModKit.Core;
using ItemModKit.Core.Locator;
using ItemModKit.Adapters.Duckov.Locator;
using CoreQuery = ItemModKit.Core.IItemQuery;
using LocatorQuery = ItemModKit.Core.Locator.IItemQuery;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// IMK 的 Duckov 统一门面/入口(Facade)。
    /// Stage 1 API Freeze: Public static members listed here are considered stable entry points. See docs/API_FREEZE.md.
    /// Do not change signatures in minor patches.
    /// </summary>
    public static class IMKDuckov
    {
        /// <summary>
        /// 静态构造：注册可选的贡献者/应用器 (Contributor + Applier)，用于扩展写入指令。
        /// </summary>
        static IMKDuckov()
        {
            try
            {
                var stats = new Contributors.StatsContributor();
                ItemStateExtensions.RegisterContributor(stats);
                ItemStateExtensions.RegisterApplier(stats);
                var effects = new Contributors.EffectsContributor();
                ItemStateExtensions.RegisterContributor(effects);
                ItemStateExtensions.RegisterApplier(effects);
                // 初始化事件桥：订阅全部现有 Item 实例变化
                DuckovEventBridge.Initialize();
            }
            catch { }
        }

        // ---- Core Adapters / Services ----
        /// <summary>物品适配器（读写核心字段、变量、修饰、插槽等）。</summary>
        public static readonly IItemAdapter Item = new DuckovItemAdapter();
        /// <summary>背包适配器。</summary>
        public static readonly IInventoryAdapter Inventory = new DuckovInventoryAdapter();
        /// <summary>槽位适配器。</summary>
        public static readonly ISlotAdapter Slot = new DuckovSlotAdapter();
        /// <summary>物品查询服务（背包/槽位）。</summary>
        public static readonly CoreQuery Query = new DuckovItemQuery();
        /// <summary>持久化适配器。</summary>
        public static readonly IItemPersistence Persistence = new DuckovPersistenceAdapter(Item);
        /// <summary>重生/替换服务。</summary>
        public static readonly IRebirthService Rebirth = new DuckovRebirthService(Item, Inventory, Slot, Persistence);
        /// <summary>当前 UI 选中项。</summary>
        public static readonly IUISelection UISelection = new DuckovUISelection();
        /// <summary>物品事件源。</summary>
        public static readonly DuckovItemEventSource ItemEvents = new DuckovItemEventSource(Query, Item);
        /// <summary>世界掉落事件源。</summary>
        public static readonly DuckovWorldDropEventSource WorldDrops = new DuckovWorldDropEventSource(Item, Inventory);
        /// <summary>只读聚合读取服务。</summary>
        public static readonly IReadService Read = new ReadService(Item);
        /// <summary>写入服务（事务式修改）。</summary>
        public static readonly IWriteService Write = new WriteService(Item);
        /// <summary>物品工厂。</summary>
        public static readonly IItemFactory Factory = new DuckovItemFactory(Item);
        /// <summary>物品移动服务。</summary>
        public static readonly IItemMover Mover = new DuckovItemMover(Item, Inventory);
        /// <summary>克隆流水线。</summary>
        public static readonly IClonePipeline Clone = new DuckovClonePipeline();
        /// <summary>变量合并服务。</summary>
        public static readonly IVariableMergeService VariableMerge = new DuckovVariableMergeService();
        /// <summary>UI 刷新服务。</summary>
        public static readonly IUIRefreshService UIRefresh = new DuckovUIRefreshService();
        /// <summary>背包解析服务。</summary>
        public static readonly IInventoryResolver InventoryResolver = new DuckovInventoryResolver();
        /// <summary>背包放置服务。</summary>
        public static readonly IInventoryPlacementService InventoryPlacement = new DuckovInventoryPlacementService();
        /// <summary>持久化调度器。</summary>
        public static readonly IPersistenceScheduler PersistenceScheduler = new PersistenceScheduler(Item, Persistence, (obj) => ItemSnapshot.Capture(Item, obj));

        // ---- Version & Capabilities ----
        /// <summary>IMK 版本号。</summary>
        public static System.Version Version => IMKVersion.Version;
        /// <summary>当前功能集。</summary>
        public static IMKCapabilities Capabilities => IMKVersion.Capabilities;
        /// <summary>确保满足最低版本要求。</summary>
        public static bool Require(System.Version min, out string error) => IMKVersion.Require(min, out error);

        // ---- Ownership Helpers ----
        /// <summary>获取持有者 OwnerId。</summary>
        public static string GetOwnerId(object item) => DuckovPersistenceAdapter.GetOwnerId(item, Item);
        /// <summary>判断是否被指定 OwnerId 拥有。</summary>
        public static bool IsOwnedBy(object item, string ownerId) => DuckovPersistenceAdapter.IsOwnedBy(item, ownerId, Item);
        /// <summary>替换内部日志记录器。</summary>
        public static void UseLogger(ILogger logger) => Log.Use(logger);

        // ---- Mutex Helpers ----
        /// <summary>尝试加锁（成功返回 true）。</summary>
        public static bool TryLock(object item, string ownerId) => DuckovMutex.TryLock(item, ownerId);
        /// <summary>释放锁。</summary>
        public static void Unlock(object item, string ownerId) => DuckovMutex.Unlock(item, ownerId);

        // ---- Migration Helper ----
        /// <summary>执行一次迁移：补全老版本缺失字段/常量。</summary>
        public static bool EnsureMigrated(object item) => DuckovMigration.EnsureMigrated(Item, Persistence, item);

        // ---- External Event Mode & Publishing ----
        /// <summary>进入“外部事件”模式，暂停自动轮询，改为手动发布。</summary>
        public static void BeginExternalEvents() => ItemEvents.BeginExternalMode();
        /// <summary>退出“外部事件”模式。</summary>
        public static void EndExternalEvents() => ItemEvents.EndExternalMode();
        /// <summary>发布新增事件。</summary>
        public static void PublishItemAdded(object item, ItemEventContext ctx = null) => ItemEvents.PublishAdded(item, ctx);
        /// <summary>发布移除事件。</summary>
        public static void PublishItemRemoved(object item, ItemEventContext ctx = null) => ItemEvents.PublishRemoved(item, ctx);
        /// <summary>发布变更事件。</summary>
        public static void PublishItemChanged(object item, ItemEventContext ctx = null) => ItemEvents.PublishChanged(item, ctx);
        /// <summary>发布移动事件。</summary>
        public static void PublishItemMoved(object item, int fromIndex, int toIndex, ItemEventContext ctx = null) => ItemEvents.PublishMoved(item, fromIndex, toIndex, ctx);
        /// <summary>发布合并事件。</summary>
        public static void PublishItemMerged(object item, ItemEventContext ctx = null) => ItemEvents.PublishMerged(item, ctx);
        /// <summary>发布拆分事件。</summary>
        public static void PublishItemSplit(object item, ItemEventContext ctx = null) => ItemEvents.PublishSplit(item, ctx);

        // ---- Dirty Markers ----
        private static int s_writeScope;
        /// <summary>
        /// 授权 WriteService 内部标记脏（使用 using 范围控制）。
        /// 显式模式(ExplicitOnly)下，仅在该范围内的 MarkDirty 才生效。
        /// </summary>
        internal static System.IDisposable AllowDirtyFromWriteService()
        {
            s_writeScope++; return new Scope(() => s_writeScope--);
        }
        private sealed class Scope : System.IDisposable { private readonly System.Action _d; public Scope(System.Action d) { _d = d; } public void Dispose() { try { _d?.Invoke(); } catch { } } }
        /// <summary>
        /// 标记某个物品为“脏”，需要持久化。immediate=true 可提示优先刷新。
        /// 在 ExplicitOnly 模式外部调用将被忽略。
        /// </summary>
        public static void MarkDirty(object item, DirtyKind kind, bool immediate = false)
        {
            if (PersistenceSettings.Current.ExplicitOnly && s_writeScope == 0)
            {
                return; // 显式模式，未授权时忽略
            }
            PersistenceScheduler.EnqueueDirty(item, kind, immediate);
        }
        /// <summary>刷新指定物品的脏写入（可选强制）。</summary>
        public static void FlushDirty(object item, bool force = false) => PersistenceScheduler.Flush(item, force);
        /// <summary>
        /// 刷新所有脏队列；reason="manual-deferred" 时为延迟/分片刷新。
        /// </summary>
        public static void FlushAllDirty(string reason = "manual")
        {
            try
            {
                if (reason == "manual-deferred")
                {
                    try { PersistenceScheduler.GetType().GetMethod("RequestFlushAllDeferred", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.Invoke(PersistenceScheduler, null); } catch { }
                    Core.Log.Info("[IMK.Flush] scheduled deferred flush (will spread across ticks)");
                    return;
                }
                try { PerfFlushMetrics.Reset(); } catch { }
                var t0 = UnityEngine.Time.realtimeSinceStartup;
                PersistenceScheduler.FlushAll(reason);
                var ms = (UnityEngine.Time.realtimeSinceStartup - t0) * 1000.0;
                try
                {
                    var snap = PerfFlushMetrics.Snapshot();
                    Core.Log.Info($"[IMK.Flush] reason={reason} took={ms:0.0}ms items={snap.Items} avg={snap.AvgItemMs:0.00}ms max={snap.MaxItemMs:0.00}ms bytes={snap.JsonBytes}");
                }
                catch { Core.Log.Info($"[IMK.Flush] reason={reason} took={ms:0.0}ms (metrics N/A)"); }
            }
            catch { }
        }
        public static IOwnershipService Ownership { get; } = new ItemModKit.Adapters.Duckov.Locator.DuckovOwnershipService();
        public static ILogicalIdMap LogicalIds { get; } = new ItemModKit.Adapters.Duckov.Locator.DuckovLogicalIdMap();
        public static IUISelectionV2 UISelectionV2 { get; } = new DuckovUISelectionV2Adapter();
        public static LocatorQuery QueryV2 { get; } = new ItemModKit.Adapters.Duckov.Locator.DuckovItemQueryV2();
        private static readonly System.Collections.Generic.Dictionary<object, IItemHandle> s_handleMap = new System.Collections.Generic.Dictionary<object, IItemHandle>();
        internal static void RegisterHandle(IItemHandle h)
        {
            if (h == null) return;
            try
            {
                var raw = h.TryGetRaw(); if (raw != null) s_handleMap[raw] = h;
                (QueryV2 as ItemModKit.Adapters.Duckov.Locator.DuckovItemQueryV2)?.AddHandle(h);
                LogicalIds.Bind(null, h);
            }
            catch { }
        }
        internal static void UnregisterHandle(IItemHandle h)
        {
            if (h == null) return;
            try
            {
                var raw = h.TryGetRaw(); if (raw != null) s_handleMap.Remove(raw);
                (QueryV2 as ItemModKit.Adapters.Duckov.Locator.DuckovItemQueryV2)?.RemoveHandle(h);
            }
            catch { }
        }
        internal static void IncrementalRefresh(object raw)
        {
            try
            {
                if (raw == null) return;
                if (s_handleMap.TryGetValue(raw, out var handle))
                {
                    // refresh underlying cached metadata if needed
                    handle.RefreshMetadata();
                    (QueryV2 as ItemModKit.Adapters.Duckov.Locator.DuckovItemQueryV2)?.UpdateHandle(handle);
                }
            }
            catch { }
        }
        public static (long ticks, int results, int source, int predicates) QueryDiagnostics()
        {
            try
            {
                var q = QueryV2 as ItemModKit.Adapters.Duckov.Locator.DuckovItemQueryV2;
                if (q == null) return (0,0,0,0);
                return (q.LastQueryTicks, q.LastResultCount, q.LastSourceSize, q.LastPredicateCount);
            }
            catch { return (0,0,0,0); }
        }
        internal static void ResetQueryIndex()
        {
            try { (QueryV2 as ItemModKit.Adapters.Duckov.Locator.DuckovItemQueryV2)?.Clear(); } catch { }
        }
        public static void ReindexAll(Func<IEnumerable<object>> enumerator)
        {
            try
            {
                ResetQueryIndex();
                var items = enumerator?.Invoke();
                if (items != null)
                {
                    foreach (var raw in items)
                    {
                        try
                        {
                            var h = Adapters.Duckov.Locator.DuckovHandleFactory.CreateItemHandle(raw);
                            RegisterHandle(h);
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }
        private static IEnumerable<object> DefaultEnumerateAll()
        {
            // Enumerate player inventory + storage + lootboxes via Ownership + existing query fallback
            var set = new System.Collections.Generic.HashSet<object>();
            try
            {
                // From legacy query (backpack/storage/all inventories)
                foreach (var it in Query.EnumerateAllInventories()) if (it != null) set.Add(it);
            }
            catch { }
            // Potential extension: world drops, etc.
            return set;
        }
        public static void ReindexAll()
        {
            ReindexAll(DefaultEnumerateAll);
        }
        public static IItemHandle TryGetCurrentSelectedHandle()
        {
            try
            {
                if (UISelectionV2.TryGetCurrent(out var h) && h != null) { RegisterHandle(h); return h; }
            }
            catch { }
            return null;
        }
        public static void RefreshHandleMetadata(IItemHandle handle)
        {
            if (handle == null) return;
            try { handle.RefreshMetadata(); } catch { }
        }
        public static IItemHandle TryGetHandle(object raw)
        {
            if (raw == null) return null; s_handleMap.TryGetValue(raw, out var h); return h;
        }
        public static IItemHandle TryGetHandleByInstanceId(int iid)
        {
            try
            {
                foreach (var kv in s_handleMap)
                {
                    var h = kv.Value; if (h?.InstanceId == iid) return h;
                }
            }
            catch { }
            return null;
        }
        public static (double avgMs, double maxMs, int queries) QueryPerfStats()
        {
            try
            {
                var q = QueryV2 as ItemModKit.Adapters.Duckov.Locator.DuckovItemQueryV2; return q != null ? q.SnapshotPerf() : (0,0,0);
            }
            catch { return (0,0,0); }
        }
        /// <summary>
        /// 手动刷新世界掉落扫描（若需要强制遍历）。常规情况下事件桥会提前登记，不必频繁调用。
        /// </summary>
        public static void ForceWorldDropRescan()
        {
            try { WorldDrops?.RegisterExternalWorldItem(null); } catch { }
        }
    }
}
