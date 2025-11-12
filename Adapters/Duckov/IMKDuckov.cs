using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// IMK 针对 Duckov 引擎的统一入口/门面(Facade)。
    /// 汇聚所有常用适配器与服务：读写/查询/事件/工厂/克隆/重生/持久化/变量合并/背包定位与放置等。
    /// 使用模式：IMKDuckov.&lt;Component&gt;.&lt;Method&gt;(...)。
    /// </summary>
    public static class IMKDuckov
    {
        /// <summary>
        /// 静态构造：注册内置的属性/效果贡献者 (Contributor + Applier)，用于快照扩展写回与恢复。
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
            }
            catch { }
        }

        // ---- Core Adapters / Services ----
        /// <summary>物品读写基础适配器。</summary>
        public static readonly IItemAdapter Item = new DuckovItemAdapter();
        /// <summary>背包/容器适配器。</summary>
        public static readonly IInventoryAdapter Inventory = new DuckovInventoryAdapter();
        /// <summary>槽位适配器 (插槽/嵌入相关)。</summary>
        public static readonly ISlotAdapter Slot = new DuckovSlotAdapter();
        /// <summary>物品查询适配器（按条件枚举/定位）。</summary>
        public static readonly IItemQuery Query = new DuckovItemQuery();
        /// <summary>持久化适配器（读写嵌入元数据与变量）。</summary>
        public static readonly IItemPersistence Persistence = new DuckovPersistenceAdapter(Item);
        /// <summary>重生/复制重建服务。</summary>
        public static readonly IRebirthService Rebirth = new DuckovRebirthService(Item, Inventory, Slot, Persistence);
        /// <summary>当前 UI 选中物品适配器。</summary>
        public static readonly IUISelection UISelection = new DuckovUISelection();
        /// <summary>物品事件源（新增/删除/变更等，支持外部模式）。</summary>
        public static readonly DuckovItemEventSource ItemEvents = new DuckovItemEventSource(Query, Item);
        /// <summary>世界掉落事件源（地面物品跟踪）。</summary>
        public static readonly DuckovWorldDropEventSource WorldDrops = new DuckovWorldDropEventSource(Item, Inventory);
        /// <summary>只读访问服务（聚合常用读取逻辑）。</summary>
        public static readonly IReadService Read = new ReadService(Item);
        /// <summary>写入访问服务（批量/事务式修改）。</summary>
        public static readonly IWriteService Write = new WriteService(Item);
        /// <summary>物品工厂（实例化/克隆）。</summary>
        public static readonly IItemFactory Factory = new DuckovItemFactory(Item);
        /// <summary>物品移动服务（跨背包/槽位搬运）。</summary>
        public static readonly IItemMover Mover = new DuckovItemMover(Item, Inventory);
        /// <summary>克隆管线（深度复制子树）。</summary>
        public static readonly IClonePipeline Clone = new DuckovClonePipeline();
        /// <summary>变量合并服务（多源变量整合）。</summary>
        public static readonly IVariableMergeService VariableMerge = new DuckovVariableMergeService();
        /// <summary>UI 刷新服务（统一触发界面更新）。</summary>
        public static readonly IUIRefreshService UIRefresh = new DuckovUIRefreshService();
        /// <summary>背包解析服务（识别物品所在背包/角色）。</summary>
        public static readonly IInventoryResolver InventoryResolver = new DuckovInventoryResolver();
        /// <summary>背包放置服务（寻找可用位置并放置，支持延迟重试）。</summary>
        public static readonly IInventoryPlacementService InventoryPlacement = new DuckovInventoryPlacementService();
        /// <summary>持久化调度器（脏数据排队 + 节流写入）。</summary>
        public static readonly IPersistenceScheduler PersistenceScheduler = new PersistenceScheduler(Item, Persistence, (obj) => ItemSnapshot.Capture(Item, obj));

        // ---- Version & Capabilities ----
        /// <summary>IMK 版本号。</summary>
        public static System.Version Version => IMKVersion.Version;
        /// <summary>当前能力标记集合。</summary>
        public static IMKCapabilities Capabilities => IMKVersion.Capabilities;
        /// <summary>确保满足最低版本要求；失败返回 false 并给出错误信息。</summary>
        public static bool Require(System.Version min, out string error) => IMKVersion.Require(min, out error);

        // ---- Ownership Helpers ----
        /// <summary>读取或推断物品所属的 OwnerId。</summary>
        public static string GetOwnerId(object item) => DuckovPersistenceAdapter.GetOwnerId(item, Item);
        /// <summary>判断物品是否归属指定 OwnerId。</summary>
        public static bool IsOwnedBy(object item, string ownerId) => DuckovPersistenceAdapter.IsOwnedBy(item, ownerId, Item);
        /// <summary>替换内部日志记录器。</summary>
        public static void UseLogger(ILogger logger) => Log.Use(logger);

        // ---- Mutex Helpers ----
        /// <summary>尝试加互斥锁（成功返回 true）。</summary>
        public static bool TryLock(object item, string ownerId) => DuckovMutex.TryLock(item, ownerId);
        /// <summary>释放互斥锁。</summary>
        public static void Unlock(object item, string ownerId) => DuckovMutex.Unlock(item, ownerId);

        // ---- Migration Helper ----
        /// <summary>执行一次迁移（补全旧版本缺失字段/变量）。</summary>
        public static bool EnsureMigrated(object item) => DuckovMigration.EnsureMigrated(Item, Persistence, item);

        // ---- External Event Mode & Publishing ----
        /// <summary>开启“外部事件模式”（暂停自动扫描，仅接受显式发布）。</summary>
        public static void BeginExternalEvents() => ItemEvents.BeginExternalMode();
        /// <summary>结束“外部事件模式”。</summary>
        public static void EndExternalEvents() => ItemEvents.EndExternalMode();
        /// <summary>发布“新增”事件。</summary>
        public static void PublishItemAdded(object item, ItemEventContext ctx = null) => ItemEvents.PublishAdded(item, ctx);
        /// <summary>发布“移除”事件。</summary>
        public static void PublishItemRemoved(object item, ItemEventContext ctx = null) => ItemEvents.PublishRemoved(item, ctx);
        /// <summary>发布“属性或状态改变”事件。</summary>
        public static void PublishItemChanged(object item, ItemEventContext ctx = null) => ItemEvents.PublishChanged(item, ctx);
        /// <summary>发布“移动位置”事件（fromIndex→toIndex）。</summary>
        public static void PublishItemMoved(object item, int fromIndex, int toIndex, ItemEventContext ctx = null) => ItemEvents.PublishMoved(item, fromIndex, toIndex, ctx);
        /// <summary>发布“合并”事件。</summary>
        public static void PublishItemMerged(object item, ItemEventContext ctx = null) => ItemEvents.PublishMerged(item, ctx);
        /// <summary>发布“拆分”事件。</summary>
        public static void PublishItemSplit(object item, ItemEventContext ctx = null) => ItemEvents.PublishSplit(item, ctx);

        // ---- Dirty Markers ----
        private static int s_writeScope;
        /// <summary>
        /// 允许 WriteService 内部产生的脏标记（返回一个作用域，Dispose 后自动撤销）。
        /// 在 ExplicitOnly 模式下，只有包裹在该作用域内的 MarkDirty 才会生效。
        /// </summary>
        internal static System.IDisposable AllowDirtyFromWriteService()
        {
            s_writeScope++; return new Scope(() => s_writeScope--);
        }
        private sealed class Scope : System.IDisposable { private readonly System.Action _d; public Scope(System.Action d) { _d = d; } public void Dispose() { try { _d?.Invoke(); } catch { } } }
        /// <summary>
        /// 标记某物品为“脏”（需要持久化），可选择立即写入或延迟调度。
        /// ExplicitOnly 且不在写入作用域时的非显式脏标记会被忽略。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <param name="kind">脏类别标记。</param>
        /// <param name="immediate">是否立即写入。</param>
        public static void MarkDirty(object item, DirtyKind kind, bool immediate = false)
        {
            if (PersistenceSettings.Current.ExplicitOnly && s_writeScope == 0)
            {
                return; // 显式模式：忽略未授权的脏标记
            }
            PersistenceScheduler.EnqueueDirty(item, kind, immediate);
        }
        /// <summary>立即对单个物品执行刷新写入（可选强制）。</summary>
        /// <param name="item">目标物品。</param>
        /// <param name="force">是否强制跳过延迟。</param>
        public static void FlushDirty(object item, bool force = false) => PersistenceScheduler.Flush(item, force);
        /// <summary>
        /// 刷新所有排队的脏物品。reason="manual-deferred" 时改为请求分帧刷新。
        /// 输出性能指标（平均/最大耗时与 JSON 字节数）。
        /// </summary>
        /// <param name="reason">原因标签。</param>
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
    }
}
