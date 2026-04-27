using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using ItemModKit.Core;
using ItemModKit.Diagnostics;
using ItemModKit.Core.Locator;
using ItemModKit.Adapters.Duckov.Locator;
using ItemModKit.Adapters.Duckov.Buffs;
using Newtonsoft.Json.Linq;
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
        /// persistence restore 的内部执行结果。
        /// 用于把 shared restore contract 映射为对外的 PersistenceRestoreResult。
        /// </summary>
        private sealed class PersistenceRestoreExecutionResult : RestoreExecutionResultBase
        {
            /// <summary>调用方请求附加到的 targetKey；为空时表示 detached restore。</summary>
            public string TargetKey { get; set; }
        }

        /// <summary>
        /// tree export restore 的内部执行结果。
        /// 除 shared restore 字段外，还记录 import mode、fallback 和 rebuild/attach 细节。
        /// </summary>
        private sealed class TreeRestoreExecutionResult : RestoreExecutionResultBase
        {
            /// <summary>调用方请求附加到的 targetKey；为空时表示 detached restore。</summary>
            public string TargetKey { get; set; }

            /// <summary>导入模式，通常是 tree 或 minimal。</summary>
            public string ImportMode { get; set; }

            /// <summary>是否发生了 fallback。</summary>
            public bool FallbackUsed { get; set; }

            /// <summary>fallback 发生的阶段。</summary>
            public string FallbackStage { get; set; }

            /// <summary>fallback 原因。</summary>
            public string FallbackReason { get; set; }

            /// <summary>期望导入的条目数。</summary>
            public int EntriesRequested { get; set; }

            /// <summary>实际导入的条目数。</summary>
            public int EntriesImported { get; set; }

            /// <summary>是否完成完整 tree rebuild。</summary>
            public bool RebuildCompleted { get; set; }

            /// <summary>是否处于降级 rebuild 状态。</summary>
            public bool RebuildDegraded { get; set; }

            /// <summary>调用方是否请求附加到宿主。</summary>
            public bool AttachRequested { get; set; }

            /// <summary>是否在 rebuild 成功后仍然附加失败。</summary>
            public bool AttachFailedAfterRebuild { get; set; }
        }

        /// <summary>
        /// 静态构造：注册可选的贡献者/应用器 (Contributor + Applier)，用于扩展写入指令。
        /// </summary>
        static IMKDuckov()
        {
            try
            {
                var stats = new StatsContributor();
                ItemStateExtensions.RegisterContributor(stats);
                ItemStateExtensions.RegisterApplier(stats);
                var modifiers = new ModifiersContributor();
                ItemStateExtensions.RegisterContributor(modifiers);
                ItemStateExtensions.RegisterApplier(modifiers);
                var effects = new Contributors.EffectsContributor();
                ItemStateExtensions.RegisterContributor(effects);
                ItemStateExtensions.RegisterApplier(effects);
                // 初始化事件桥：订阅全部现有 Item 实例变化
                DuckovEventBridge.Initialize();
            }
            catch (Exception ex) { ReportFacadeFailureOnce("StaticInitialization", ex); }
        }

        // ---- Core Adapters / Services ----
        /// <summary>物品适配器（读写核心字段、变量、修饰、插槽等）。</summary>
        public static readonly IItemAdapter Item = new DuckovItemAdapter();
        /// <summary>背包适配器。</summary>
        public static readonly IInventoryAdapter Inventory = new DuckovInventoryAdapter();
        /// <summary>槽位适配器。</summary>
        public static readonly ISlotAdapter Slot = new DuckovSlotAdapter();
        /// <summary>
        /// Stage 1 兼容查询门面。
        /// 新代码优先使用 QueryV2；这里只保留旧的背包/槽位查询入口，便于兼容旧调用方。
        /// </summary>
        [Obsolete("新代码优先使用 IMKDuckov.QueryV2。IMKDuckov.Query 仅保留为 Stage 1 兼容查询门面。", false)]
        public static readonly CoreQuery Query = new DuckovCompatItemQueryFacade();
        /// <summary>持久化适配器。</summary>
        public static readonly IItemPersistence Persistence = new DuckovPersistenceAdapter(Item);
        /// <summary>重生/替换服务。</summary>
        public static readonly IRebirthService Rebirth = new DuckovRebirthService(Item, Inventory, Slot, Persistence);
        /// <summary>
        /// Stage 1 兼容 UI 选中项门面。
        /// 新代码优先使用 UISelectionV2 或 TryGetCurrentSelectedHandle()，以获得 handle 语义和 locator 体系支持。
        /// </summary>
        [Obsolete("新代码优先使用 IMKDuckov.UISelectionV2 或 IMKDuckov.TryGetCurrentSelectedHandle()。IMKDuckov.UISelection 仅保留为 Stage 1 兼容选中项门面。", false)]
        public static readonly IUISelection UISelection = new DuckovCompatUISelectionFacade();
        /// <summary>物品事件源。</summary>
        public static readonly DuckovItemEventSource ItemEvents = new DuckovItemEventSource(Item);
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
        /// <summary>持久化调度器，负责脏标记后的延迟或批量刷新。</summary>
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

        /// <summary>
        /// 执行“无槽位物品补槽”草案请求。
        /// 当前仍属于实现孵化期入口，适合 Probe、内部工具和受控后置 mod 使用。
        /// </summary>
        /// <param name="request">补槽请求对象，负责描述目标物品、目标槽位以及是否需要刷新 UI / 写回持久化。</param>
        /// <returns>
        /// 成功时返回包含补槽结果和 diagnostics 的 `RichResult`；
        /// 失败时返回错误码与错误信息，调用方应根据 `Ok` 判断是否真的补槽成功。
        /// </returns>
        /// <remarks>
        /// 这是面向后置作者的 facade 入口。
        /// 若只想判断“有没有成功补槽”，先看 `Ok`；
        /// 若还要知道哪些槽是新建、哪些只是复用，再看 `Value.CreatedSlotKeys` 和 `Value.ReusedSlotKeys`。
        /// </remarks>
        public static RichResult<EnsureSlotsResult> EnsureSlotsDraft(EnsureSlotsRequest request)
        {
            return DuckovSlotProvisioningDraft.EnsureSlots(request);
        }

        /// <summary>
        /// 执行 durability/use-count 补建草案请求。
        /// 当前仍属于实现孵化期入口，适合 Probe、内部工具和受控后置 mod 使用。
        /// </summary>
        /// <param name="request">资源补建请求对象，负责描述目标物品、目标资源状态以及后续是否刷新 UI / 写回持久化。</param>
        /// <returns>
        /// 成功时返回包含补建结果和 diagnostics 的 RichResult；
        /// 失败时返回错误码与错误信息，调用方应优先检查 Ok。
        /// </returns>
        /// <remarks>
        /// 对后置作者来说，最常见的读取方式是：
        /// 先看 Ok，再看 Value.RuntimeStateApplied、Value.MetadataPersisted 和 Value.Diagnostics。
        /// </remarks>
        public static RichResult<EnsureResourceProvisionResult> EnsureResourceProvisionDraft(EnsureResourceProvisionRequest request)
        {
            return DuckovResourceProvisioningDraft.EnsureProvision(request);
        }

        /// <summary>
        /// 枚举当前运行时可见的 effect / trigger / filter / action schema 草案目录。
        /// 当前属于 draft 入口，适合 Probe、内部工具和受控后置 mod 使用。
        /// </summary>
        public static RichResult<EffectSchemaCatalogDraft> EnumerateEffectSchemaDraft(bool includeAbstractTypes = false)
        {
            return DuckovEffectSchemaDraft.Enumerate(includeAbstractTypes);
        }

        /// <summary>
        /// 枚举当前运行时可见的 buff prefab 目录草案。
        /// 当前属于 draft 入口，适合 Probe、内部工具和受控后置 mod 使用。
        /// </summary>
        public static RichResult<BuffCatalogDraft> EnumerateBuffCatalogDraft()
        {
            return DuckovBuffDraftService.EnumerateCatalog();
        }

        /// <summary>
        /// 读取指定宿主上下文当前激活的 buff 列表草案。
        /// hostContext 为空时默认读取主角；传主角对象、主角角色物品或其子物品时也会解析到主角。
        /// </summary>
        public static RichResult<BuffSnapshotDraft[]> TryReadBuffsDraft(object hostContext = null)
        {
            return DuckovBuffDraftService.TryReadBuffs(hostContext);
        }

        /// <summary>
        /// 按 ID 查找当前激活的 buff 草案。
        /// </summary>
        public static RichResult<BuffSnapshotDraft> TryFindBuffDraft(int buffId, object hostContext = null)
        {
            return DuckovBuffDraftService.TryFindBuff(buffId, hostContext);
        }

        /// <summary>
        /// 按独占标签查找当前激活的 buff 草案。
        /// 标签支持传入枚举名本身，或包含命名空间/类型前缀的完整 ToString 结果。
        /// </summary>
        public static RichResult<BuffSnapshotDraft> TryFindBuffByExclusiveTagDraft(string exclusiveTag, object hostContext = null)
        {
            return DuckovBuffDraftService.TryFindBuffByExclusiveTag(exclusiveTag, hostContext);
        }

        /// <summary>
        /// 按运行时类型全名查找当前激活的 buff 草案。
        /// 允许传完整 FullName，也允许只传类型短名。
        /// </summary>
        public static RichResult<BuffSnapshotDraft> TryFindBuffByTypeDraft(string typeFullName, object hostContext = null)
        {
            return DuckovBuffDraftService.TryFindBuffByType(typeFullName, hostContext);
        }

        /// <summary>
        /// 判断指定 buff 是否处于激活状态草案。
        /// </summary>
        public static RichResult<bool> TryHasBuffDraft(int buffId, object hostContext = null)
        {
            return DuckovBuffDraftService.TryHasBuff(buffId, hostContext);
        }

        /// <summary>
        /// 向目标角色添加一个 buff 草案。
        /// hostContext 为空时默认目标是主角。
        /// </summary>
        public static RichResult TryAddBuffDraft(int buffId, object hostContext = null, int overrideWeaponId = 0)
        {
            return DuckovBuffDraftService.TryAddBuff(buffId, hostContext, overrideWeaponId);
        }

        /// <summary>
        /// 从目标角色移除一个 buff 草案；removeOneLayer=true 时走原版减层语义。
        /// hostContext 为空时默认目标是主角。
        /// </summary>
        public static RichResult TryRemoveBuffDraft(int buffId, bool removeOneLayer = false, object hostContext = null)
        {
            return DuckovBuffDraftService.TryRemoveBuff(buffId, removeOneLayer, hostContext);
        }

        /// <summary>
        /// 直接设置目标角色某个激活 buff 的层数草案。
        /// 设为 0 时会移除该 buff。
        /// </summary>
        public static RichResult TrySetBuffLayersDraft(int buffId, int layers, object hostContext = null)
        {
            return DuckovBuffDraftService.TrySetBuffLayers(buffId, layers, hostContext);
        }

        /// <summary>
        /// 对目标角色某个 buff 增加若干层草案。
        /// 当 addIfMissing=true 且目标 buff 尚未激活时，会先添加再写到目标层数。
        /// </summary>
        public static RichResult TryAddBuffLayersDraft(int buffId, int layerDelta, bool addIfMissing = true, object hostContext = null, int overrideWeaponId = 0)
        {
            return DuckovBuffDraftService.TryAddBuffLayers(buffId, layerDelta, addIfMissing, hostContext, overrideWeaponId);
        }

        /// <summary>
        /// 对目标角色某个 buff 减少若干层草案。
        /// 当减少后层数小于等于 0 时，会直接移除该 buff。
        /// </summary>
        public static RichResult TryRemoveBuffLayersDraft(int buffId, int layerDelta, object hostContext = null)
        {
            return DuckovBuffDraftService.TryRemoveBuffLayers(buffId, layerDelta, hostContext);
        }

        /// <summary>
        /// 按独占标签移除激活 buff 草案；removeOneLayer=true 时走原版减层语义。
        /// </summary>
        public static RichResult TryRemoveBuffsByExclusiveTagDraft(string exclusiveTag, bool removeOneLayer = false, object hostContext = null)
        {
            return DuckovBuffDraftService.TryRemoveBuffsByExclusiveTag(exclusiveTag, removeOneLayer, hostContext);
        }

        // ---- Persistence Restore Helper ----
        /// <summary>
        /// 根据持久化 ItemMeta 恢复一个新物品。
        /// 这是最简入口：不指定 targetKey，返回 detached root，不暴露结构化 diagnostics。
        /// </summary>
        public static RichResult<object> RestoreFromMeta(ItemMeta meta)
        {
            return RestoreFromMeta(meta, targetKey: null, refreshUI: false);
        }

        /// <summary>
        /// 根据持久化 ItemMeta 恢复一个新物品，并返回结构化详细结果。
        /// 当调用方需要 attached、targetResolved、strategy、diagnostics 等信息时，应优先使用这个重载。
        /// </summary>
        public static RichResult<PersistenceRestoreResult> RestoreFromMetaDetailed(ItemMeta meta)
        {
            return RestoreFromMetaDetailed(meta, targetKey: null, refreshUI: false);
        }

        /// <summary>
        /// 根据持久化 ItemMeta 恢复一个新物品；指定 targetKey 时尝试附加到解析到的宿主背包。
        /// 如果调用方只关心最终 rootItem 是否成功创建，可使用这个非详细结果入口。
        /// </summary>
        public static RichResult<object> RestoreFromMeta(ItemMeta meta, string targetKey, bool refreshUI = true)
        {
            var detailed = RestoreFromMetaDetailed(meta, targetKey, refreshUI);
            if (!detailed.Ok || detailed.Value == null || detailed.Value.RootItem == null)
            {
                return RichResult<object>.Fail(detailed.Code, detailed.Error);
            }

            return RichResult<object>.Success(detailed.Value.RootItem);
        }

        /// <summary>
        /// 根据持久化 ItemMeta 恢复一个新物品；指定目标键时尝试附加到解析到的宿主背包，并返回结构化详细结果。
        /// 目标键解析失败、附加失败或降级恢复信息都会进入返回对象的 Diagnostics。
        /// </summary>
        public static RichResult<PersistenceRestoreResult> RestoreFromMetaDetailed(ItemMeta meta, string targetKey, bool refreshUI = true)
        {
            if (meta == null || meta.TypeId <= 0)
            {
                return RichResult<PersistenceRestoreResult>.Fail(ErrorCode.InvalidArgument, "meta/typeId invalid");
            }

            var restore = ExecutePersistenceRestore(meta, targetKey, refreshUI);
            if (!restore.Succeeded || restore.RootItem == null)
            {
                return RichResult<PersistenceRestoreResult>.Fail(restore.ErrorCode, BuildPersistenceRestoreFailureMessage(restore));
            }

            return RichResult<PersistenceRestoreResult>.Success(CreatePersistenceRestoreResult(restore));
        }

        /// <summary>
        /// 根据 DuckovTreeDataService.TryExport(...) 产出的 tree export 恢复一个新物品。
        /// 这是最简入口：不指定 targetKey，返回 detached root，不暴露结构化 diagnostics。
        /// </summary>
        public static RichResult<object> RestoreFromTreeExport(JObject exportData)
        {
            return RestoreFromTreeExport(exportData, targetKey: null, refreshUI: false);
        }

        /// <summary>
        /// 根据 DuckovTreeDataService.TryExport(...) 产出的 tree export 恢复一个新物品，并返回结构化详细结果。
        /// 当调用方需要 importMode、fallback、entriesImported 等恢复细节时，应优先使用这个重载。
        /// </summary>
        public static RichResult<TreeRestoreResult> RestoreFromTreeExportDetailed(JObject exportData)
        {
            return RestoreFromTreeExportDetailed(exportData, targetKey: null, refreshUI: false);
        }

        /// <summary>
        /// 根据 DuckovTreeDataService.TryExport(...) 产出的 tree export 恢复一个新物品；指定 targetKey 时尝试附加到解析到的宿主背包。
        /// 如果调用方只关心最终 rootItem 是否生成成功，可使用这个非详细结果入口。
        /// </summary>
        public static RichResult<object> RestoreFromTreeExport(JObject exportData, string targetKey, bool refreshUI = true)
        {
            var detailed = RestoreFromTreeExportDetailed(exportData, targetKey, refreshUI);
            if (!detailed.Ok || detailed.Value == null || detailed.Value.RootItem == null)
            {
                return RichResult<object>.Fail(detailed.Code, detailed.Error);
            }

            return RichResult<object>.Success(detailed.Value.RootItem);
        }

        /// <summary>
        /// 根据 DuckovTreeDataService.TryExport(...) 产出的 tree export 恢复一个新物品；指定目标键时尝试附加到解析到的宿主背包，并返回结构化详细结果。
        /// 该结果会同时描述 tree rebuild 是否完整、是否发生 fallback，以及最终 attach outcome。
        /// </summary>
        public static RichResult<TreeRestoreResult> RestoreFromTreeExportDetailed(JObject exportData, string targetKey, bool refreshUI = true)
        {
            if (exportData == null)
            {
                return RichResult<TreeRestoreResult>.Fail(ErrorCode.InvalidArgument, "exportData null");
            }

            var restore = ExecuteTreeExportRestore(exportData, targetKey, refreshUI);
            if (!restore.Succeeded || restore.RootItem == null)
            {
                return RichResult<TreeRestoreResult>.Fail(restore.ErrorCode, BuildTreeRestoreFailureMessage(restore));
            }

            return RichResult<TreeRestoreResult>.Success(CreateTreeRestoreResult(restore));
        }

        /// <summary>
        /// 用指定元数据替换旧物品并返回结构化详细结果。
        /// 适合“成功导向”的调用方：失败时返回 RichResult.Fail，成功时可消费共享诊断与重生恢复/告警元数据。
        /// </summary>
        public static RichResult<RebirthRestoreResult> ReplaceRebirthDetailed(object oldItem, ItemMeta meta, bool keepLocation = true)
        {
            if (oldItem == null)
            {
                return RichResult<RebirthRestoreResult>.Fail(ErrorCode.InvalidArgument, "oldItem null");
            }

            if (Rebirth is DuckovRebirthService detailed)
            {
                return detailed.ReplaceRebirthDetailed(oldItem, meta, keepLocation);
            }

            return RichResult<RebirthRestoreResult>.Fail(ErrorCode.NotSupported, "rebirth detailed not supported by current adapter");
        }

        /// <summary>
        /// 显式执行“安全替换”语义的 rebirth，并返回结构化详细结果。
        /// 该语义保持当前 IMK 接管行为：会记录 meta，并保留 IMK_/Custom 变量。
        /// </summary>
        public static RichResult<RebirthRestoreResult> ReplaceSafeRebirthDetailed(object oldItem, ItemMeta meta, bool keepLocation = true)
        {
            if (oldItem == null)
            {
                return RichResult<RebirthRestoreResult>.Fail(ErrorCode.InvalidArgument, "oldItem null");
            }

            if (Rebirth is DuckovRebirthService detailed)
            {
                return detailed.ReplaceRebirthDetailed(oldItem, meta, keepLocation, RebirthIntent.SafeReplace);
            }

            return RichResult<RebirthRestoreResult>.Fail(ErrorCode.NotSupported, "safe rebirth detailed not supported by current adapter");
        }

        /// <summary>
        /// 显式执行“干净重生”语义的 rebirth，并返回结构化详细结果。
        /// 该语义会重建并替换旧实例，但不主动写入 IMK meta，也不复制 IMK_/Custom 变量。
        /// </summary>
        public static RichResult<RebirthRestoreResult> ReplaceCleanRebirthDetailed(object oldItem, ItemMeta meta, bool keepLocation = true)
        {
            if (oldItem == null)
            {
                return RichResult<RebirthRestoreResult>.Fail(ErrorCode.InvalidArgument, "oldItem null");
            }

            if (Rebirth is DuckovRebirthService detailed)
            {
                return detailed.ReplaceRebirthDetailed(oldItem, meta, keepLocation, RebirthIntent.CleanRebirth);
            }

            return RichResult<RebirthRestoreResult>.Fail(ErrorCode.NotSupported, "clean rebirth detailed not supported by current adapter");
        }

        /// <summary>
        /// 用指定元数据替换旧物品并返回结构化报告结果。
        /// 与详细结果入口不同，这个入口是失败安全的：即使失败，也尽量返回包含错误、诊断和告警信息的结果对象。
        /// </summary>
        public static RebirthRestoreResult ReplaceRebirthReport(object oldItem, ItemMeta meta, bool keepLocation = true)
        {
            if (oldItem == null)
            {
                return new RebirthRestoreResult
                {
                    Succeeded = false,
                    ErrorCode = ErrorCode.InvalidArgument,
                    Error = "oldItem null",
                    IntentUsed = RebirthIntent.SafeReplace,
                    StrategyUsed = "unknown",
                    Diagnostics = new Dictionary<string, object>
                    {
                        ["rebirth.intent"] = RebirthIntent.SafeReplace.ToString(),
                        ["strategy"] = "unknown",
                        ["attached"] = false,
                        ["targetResolved"] = false,
                        ["errorCode"] = ErrorCode.InvalidArgument,
                        ["error"] = "oldItem null",
                    }
                };
            }

            if (Rebirth is DuckovRebirthService detailed)
            {
                return detailed.ReplaceRebirthReport(oldItem, meta, keepLocation);
            }

            return new RebirthRestoreResult
            {
                Succeeded = false,
                ErrorCode = ErrorCode.NotSupported,
                Error = "rebirth report not supported by current adapter",
                IntentUsed = RebirthIntent.SafeReplace,
                StrategyUsed = "unknown",
                Diagnostics = new Dictionary<string, object>
                {
                    ["rebirth.intent"] = RebirthIntent.SafeReplace.ToString(),
                    ["strategy"] = "unknown",
                    ["attached"] = false,
                    ["targetResolved"] = false,
                    ["errorCode"] = ErrorCode.NotSupported,
                    ["error"] = "rebirth report not supported by current adapter",
                }
            };
        }

        /// <summary>
        /// 显式执行“安全替换”语义的 rebirth，并返回结构化报告结果。
        /// </summary>
        public static RebirthRestoreResult ReplaceSafeRebirthReport(object oldItem, ItemMeta meta, bool keepLocation = true)
        {
            if (oldItem == null)
            {
                return new RebirthRestoreResult
                {
                    Succeeded = false,
                    ErrorCode = ErrorCode.InvalidArgument,
                    Error = "oldItem null",
                    IntentUsed = RebirthIntent.SafeReplace,
                    StrategyUsed = "unknown",
                    Diagnostics = new Dictionary<string, object>
                    {
                        ["rebirth.intent"] = RebirthIntent.SafeReplace.ToString(),
                        ["strategy"] = "unknown",
                        ["attached"] = false,
                        ["targetResolved"] = false,
                        ["errorCode"] = ErrorCode.InvalidArgument,
                        ["error"] = "oldItem null",
                    }
                };
            }

            if (Rebirth is DuckovRebirthService detailed)
            {
                return detailed.ReplaceRebirthReport(oldItem, meta, keepLocation, RebirthIntent.SafeReplace);
            }

            return new RebirthRestoreResult
            {
                Succeeded = false,
                ErrorCode = ErrorCode.NotSupported,
                Error = "safe rebirth report not supported by current adapter",
                IntentUsed = RebirthIntent.SafeReplace,
                StrategyUsed = "unknown",
                Diagnostics = new Dictionary<string, object>
                {
                    ["rebirth.intent"] = RebirthIntent.SafeReplace.ToString(),
                    ["strategy"] = "unknown",
                    ["attached"] = false,
                    ["targetResolved"] = false,
                    ["errorCode"] = ErrorCode.NotSupported,
                    ["error"] = "safe rebirth report not supported by current adapter",
                }
            };
        }

        /// <summary>
        /// 显式执行“干净重生”语义的 rebirth，并返回结构化报告结果。
        /// </summary>
        public static RebirthRestoreResult ReplaceCleanRebirthReport(object oldItem, ItemMeta meta, bool keepLocation = true)
        {
            if (oldItem == null)
            {
                return new RebirthRestoreResult
                {
                    Succeeded = false,
                    ErrorCode = ErrorCode.InvalidArgument,
                    Error = "oldItem null",
                    IntentUsed = RebirthIntent.CleanRebirth,
                    StrategyUsed = "unknown",
                    Diagnostics = new Dictionary<string, object>
                    {
                        ["rebirth.intent"] = RebirthIntent.CleanRebirth.ToString(),
                        ["strategy"] = "unknown",
                        ["attached"] = false,
                        ["targetResolved"] = false,
                        ["errorCode"] = ErrorCode.InvalidArgument,
                        ["error"] = "oldItem null",
                    }
                };
            }

            if (Rebirth is DuckovRebirthService detailed)
            {
                return detailed.ReplaceRebirthReport(oldItem, meta, keepLocation, RebirthIntent.CleanRebirth);
            }

            return new RebirthRestoreResult
            {
                Succeeded = false,
                ErrorCode = ErrorCode.NotSupported,
                Error = "clean rebirth report not supported by current adapter",
                IntentUsed = RebirthIntent.CleanRebirth,
                StrategyUsed = "unknown",
                Diagnostics = new Dictionary<string, object>
                {
                    ["rebirth.intent"] = RebirthIntent.CleanRebirth.ToString(),
                    ["strategy"] = "unknown",
                    ["attached"] = false,
                    ["targetResolved"] = false,
                    ["errorCode"] = ErrorCode.NotSupported,
                    ["error"] = "clean rebirth report not supported by current adapter",
                }
            };
        }

        /// <summary>
        /// 获取最近缓存的 rebirth reports；默认返回最近 20 条，按时间倒序。
        /// 适合 Probe、离线诊断页或无 UI 的快速排障场景。
        /// </summary>
        public static RebirthRestoreResult[] GetRecentRebirthReports(int maxCount = 20) => IMKRebirthReports.SnapshotRecent(maxCount);

        /// <summary>清空最近缓存的 rebirth reports。</summary>
        public static void ClearRecentRebirthReports() => IMKRebirthReports.Clear();

        /// <summary>
        /// 将最近缓存的 rebirth reports 输出到日志；适合无调试 UI 时快速导出最近替换结果。
        /// </summary>
        public static void LogRecentRebirthReports(int maxCount = 10, bool includeDiagnostics = false) => IMKRebirthReports.LogRecent(maxCount, includeDiagnostics);

        private static PersistenceRestoreExecutionResult ExecutePersistenceRestore(ItemMeta meta, string targetKey, bool refreshUI)
        {
            RestoreDiagnostics diagnostics = null;
            var result = new PersistenceRestoreExecutionResult
            {
                Succeeded = false,
                ErrorCode = ErrorCode.OperationFailed,
                TargetKey = targetKey,
                TargetMode = string.IsNullOrEmpty(targetKey) ? RestoreTargetMode.DetachedTree : RestoreTargetMode.AttachToResolvedHost,
                FinalPhase = RestorePhase.None,
            };

            var request = new RestoreRequest
            {
                Source = meta,
                SourceKind = RestoreSourceKind.Persistence,
                TargetMode = result.TargetMode,
                Strategy = CloneStrategy.Unity,
                VariableMergeMode = VariableMergeMode.None,
                CopyTags = false,
                AllowDegraded = true,
                PublishEvents = false,
                RefreshUI = !string.IsNullOrEmpty(targetKey) && refreshUI,
                MarkDirty = false,
                DiagnosticsEnabled = true,
                CallerTag = "persistence.restore",
                ResolvedTargetKey = targetKey,
            };
            request.DiagnosticsFinalized = (diag, _) =>
            {
                diagnostics = diag;
                result.Diagnostics = diag;
            };
            request.DiagnosticsMetadata["persistence.targetKey"] = targetKey ?? string.Empty;
            request.DiagnosticsMetadata["persistence.refreshUI"] = !string.IsNullOrEmpty(targetKey) && refreshUI;
            request.DiagnosticsMetadata["persistence.typeId"] = meta.TypeId;

            var restore = DuckovTreeRestoreOrchestrator.Shared.Execute(request);
            if (!restore.Ok || restore.Value == null || restore.Value.RootItem == null)
            {
                result.ApplyFailure(restore.Code, restore.Error ?? "restore failed", diagnostics);
                return result;
            }

            result.ApplyRestoreSuccess(restore.Value, diagnostics);
            return result;
        }

        private static TreeRestoreExecutionResult ExecuteTreeExportRestore(JObject exportData, string targetKey, bool refreshUI)
        {
            RestoreDiagnostics diagnostics = null;
            var result = new TreeRestoreExecutionResult
            {
                Succeeded = false,
                ErrorCode = ErrorCode.OperationFailed,
                TargetKey = targetKey,
                TargetMode = string.IsNullOrEmpty(targetKey) ? RestoreTargetMode.DetachedTree : RestoreTargetMode.AttachToResolvedHost,
                FinalPhase = RestorePhase.None,
                ImportMode = "minimal",
            };

            var imported = DuckovTreeDataService.TryImportTree(exportData, out var importDiagnostics);
            if (!imported.Ok || imported.Value == null)
            {
                result.ApplyFailure(imported.Code, imported.Error ?? "tree import failed");
                return result;
            }

            result.ImportMode = importDiagnostics?.ImportMode ?? "minimal";
            result.FallbackUsed = importDiagnostics?.FallbackUsed ?? false;
            result.FallbackStage = importDiagnostics?.FallbackStage ?? string.Empty;
            result.FallbackReason = importDiagnostics?.FallbackReason ?? string.Empty;
            result.EntriesRequested = importDiagnostics?.EntriesRequested ?? 0;
            result.EntriesImported = importDiagnostics?.EntriesImported ?? 0;
            result.RebuildCompleted = string.Equals(result.ImportMode, "tree", StringComparison.Ordinal) && !result.FallbackUsed;
            result.RebuildDegraded = result.FallbackUsed || !result.RebuildCompleted;
            result.AttachRequested = result.TargetMode != RestoreTargetMode.DetachedTree;

            var request = new RestoreRequest
            {
                Source = exportData,
                PreparedRoot = imported.Value,
                SourceKind = RestoreSourceKind.VanillaTreeData,
                TargetMode = result.TargetMode,
                Strategy = CloneStrategy.TreeData,
                VariableMergeMode = VariableMergeMode.None,
                CopyTags = false,
                AllowDegraded = true,
                PublishEvents = false,
                RefreshUI = !string.IsNullOrEmpty(targetKey) && refreshUI,
                MarkDirty = false,
                DiagnosticsEnabled = true,
                CallerTag = "treedata.restore",
                ResolvedTargetKey = targetKey,
            };
            request.DiagnosticsFinalized = (diag, _) =>
            {
                diagnostics = diag;
                result.Diagnostics = diag;
            };
            request.DiagnosticsMetadata["tree.importMode"] = result.ImportMode;
            request.DiagnosticsMetadata["tree.fallbackUsed"] = result.FallbackUsed;
            request.DiagnosticsMetadata["tree.fallbackStage"] = result.FallbackStage ?? string.Empty;
            request.DiagnosticsMetadata["tree.fallbackReason"] = result.FallbackReason ?? string.Empty;
            request.DiagnosticsMetadata["tree.entriesRequested"] = result.EntriesRequested;
            request.DiagnosticsMetadata["tree.entriesImported"] = result.EntriesImported;
            request.DiagnosticsMetadata["tree.rebuildCompleted"] = result.RebuildCompleted;
            request.DiagnosticsMetadata["tree.rebuildDegraded"] = result.RebuildDegraded;
            request.DiagnosticsMetadata["tree.attachRequested"] = result.AttachRequested;
            request.DiagnosticsMetadata["tree.targetKey"] = targetKey ?? string.Empty;
            request.DiagnosticsMetadata["tree.refreshUI"] = !string.IsNullOrEmpty(targetKey) && refreshUI;
            request.DiagnosticsMetadata["tree.rootTypeId"] = exportData.Value<int?>("rootTypeId") ?? 0;

            var restore = DuckovTreeRestoreOrchestrator.Shared.Execute(request);
            if (!restore.Ok || restore.Value == null || restore.Value.RootItem == null)
            {
                result.ApplyFailure(restore.Code, restore.Error ?? "restore failed", diagnostics);
                return result;
            }

            result.ApplyRestoreSuccess(restore.Value, diagnostics);
            result.AttachFailedAfterRebuild = result.AttachRequested && !result.Attached && !result.DeferredScheduled;
            result.StrategyUsed = string.Equals(result.ImportMode, "tree", StringComparison.Ordinal)
                ? "TreeExportTree"
                : "TreeExportMinimal";
            if (result.Diagnostics != null)
            {
                result.Diagnostics.StrategyUsed = result.StrategyUsed;
                result.Diagnostics.Metadata["strategy"] = result.StrategyUsed;
                result.Diagnostics.FallbackUsed = result.FallbackUsed;
                result.Diagnostics.Metadata["tree.rebuildCompleted"] = result.RebuildCompleted;
                result.Diagnostics.Metadata["tree.rebuildDegraded"] = result.RebuildDegraded;
                result.Diagnostics.Metadata["tree.attachRequested"] = result.AttachRequested;
                result.Diagnostics.Metadata["tree.attachFailedAfterRebuild"] = result.AttachFailedAfterRebuild;
            }

            return result;
        }

        private static string BuildPersistenceRestoreFailureMessage(PersistenceRestoreExecutionResult result)
        {
            if (result == null)
            {
                return "restore failed";
            }

            var diagnostics = result.Diagnostics;
            if (diagnostics == null)
            {
                return result.BuildFailureMessage(
                    new KeyValuePair<string, string>("attached", result.Attached ? "true" : "false"),
                    new KeyValuePair<string, string>("targetResolved", "false"),
                    new KeyValuePair<string, string>("targetKey", result.TargetKey ?? string.Empty));
            }

            var strategy = result.StrategyUsed ?? diagnostics.StrategyUsed ?? "unknown";
            var attached = diagnostics.Metadata.TryGetValue("attached", out var attachedObj) && attachedObj is bool attachedBool && attachedBool;
            var targetResolved = diagnostics.Metadata.TryGetValue("targetResolved", out var resolvedObj) && resolvedObj is bool resolvedBool && resolvedBool;
            var targetKey = diagnostics.Metadata.TryGetValue("persistence.targetKey", out var targetObj) ? Convert.ToString(targetObj) : string.Empty;
            result.StrategyUsed = strategy;
            return result.BuildFailureMessage(
                new KeyValuePair<string, string>("attached", attached ? "true" : "false"),
                new KeyValuePair<string, string>("targetResolved", targetResolved ? "true" : "false"),
                new KeyValuePair<string, string>("targetKey", targetKey ?? string.Empty));
        }

        private static string BuildTreeRestoreFailureMessage(TreeRestoreExecutionResult result)
        {
            if (result == null)
            {
                return "tree restore failed";
            }

            var diagnostics = result.Diagnostics;
            if (diagnostics == null)
            {
                return result.BuildFailureMessage(
                    new KeyValuePair<string, string>("attached", result.Attached ? "true" : "false"),
                    new KeyValuePair<string, string>("targetResolved", "false"),
                    new KeyValuePair<string, string>("targetKey", result.TargetKey ?? string.Empty),
                    new KeyValuePair<string, string>("importMode", result.ImportMode ?? string.Empty),
                    new KeyValuePair<string, string>("fallbackStage", result.FallbackStage ?? string.Empty));
            }

            var attached = diagnostics.Metadata.TryGetValue("attached", out var attachedObj)
                && attachedObj is bool attachedBool
                && attachedBool;
            var targetResolved = diagnostics.Metadata.TryGetValue("targetResolved", out var targetResolvedObj)
                && targetResolvedObj is bool targetResolvedBool
                && targetResolvedBool;
            var targetKey = diagnostics.Metadata.TryGetValue("tree.targetKey", out var targetKeyObj)
                ? targetKeyObj as string
                : result.TargetKey;
            var importMode = diagnostics.Metadata.TryGetValue("tree.importMode", out var importModeObj)
                ? importModeObj as string
                : result.ImportMode;
            var fallbackStage = diagnostics.Metadata.TryGetValue("tree.fallbackStage", out var fallbackStageObj)
                ? fallbackStageObj as string
                : result.FallbackStage;
            var attachOutcome = diagnostics.Metadata.TryGetValue("attachOutcome", out var attachOutcomeObj)
                ? attachOutcomeObj as string
                : string.Empty;
            var rebuildCompleted = diagnostics.Metadata.TryGetValue("tree.rebuildCompleted", out var rebuildCompletedObj)
                && rebuildCompletedObj is bool rebuildCompletedBool
                && rebuildCompletedBool;
            var attachFailedAfterRebuild = diagnostics.Metadata.TryGetValue("tree.attachFailedAfterRebuild", out var attachFailedAfterRebuildObj)
                && attachFailedAfterRebuildObj is bool attachFailedAfterRebuildBool
                && attachFailedAfterRebuildBool;

            return result.BuildFailureMessage(
                new KeyValuePair<string, string>("attached", attached ? "true" : "false"),
                new KeyValuePair<string, string>("targetResolved", targetResolved ? "true" : "false"),
                new KeyValuePair<string, string>("targetKey", targetKey ?? string.Empty),
                new KeyValuePair<string, string>("importMode", importMode ?? string.Empty),
                new KeyValuePair<string, string>("fallbackStage", fallbackStage ?? string.Empty),
                new KeyValuePair<string, string>("attachOutcome", attachOutcome ?? string.Empty),
                new KeyValuePair<string, string>("rebuildCompleted", rebuildCompleted ? "true" : "false"),
                new KeyValuePair<string, string>("attachFailedAfterRebuild", attachFailedAfterRebuild ? "true" : "false"));
        }

        private static PersistenceRestoreResult CreatePersistenceRestoreResult(PersistenceRestoreExecutionResult result)
        {
            var diagnostics = result?.Diagnostics;
            var details = new Dictionary<string, object>();
            if (diagnostics != null)
            {
                foreach (var pair in diagnostics.Metadata)
                {
                    details[pair.Key] = pair.Value;
                }
            }

            var targetResolved = diagnostics != null
                && diagnostics.Metadata.TryGetValue("targetResolved", out var resolvedObj)
                && resolvedObj is bool resolvedBool
                && resolvedBool;

            details["strategy"] = result?.ResolveStrategyLabel() ?? "unknown";
            details["attached"] = result?.Attached ?? false;
            details["targetResolved"] = targetResolved;
            details["targetKey"] = result?.TargetKey ?? string.Empty;

            return new PersistenceRestoreResult
            {
                RootItem = result?.RootItem,
                Attached = result?.Attached ?? false,
                TargetResolved = targetResolved,
                AttachedIndex = result?.AttachedIndex ?? -1,
                StrategyUsed = result?.ResolveStrategyLabel() ?? "unknown",
                Diagnostics = details,
            };
        }

        private static TreeRestoreResult CreateTreeRestoreResult(TreeRestoreExecutionResult result)
        {
            var diagnostics = result?.Diagnostics;
            var details = new Dictionary<string, object>();
            if (diagnostics != null)
            {
                foreach (var pair in diagnostics.Metadata)
                {
                    details[pair.Key] = pair.Value;
                }
            }

            var targetResolved = diagnostics != null
                && diagnostics.Metadata.TryGetValue("targetResolved", out var resolvedObj)
                && resolvedObj is bool resolvedBool
                && resolvedBool;

            details["strategy"] = result?.StrategyUsed ?? result?.ResolveStrategyLabel() ?? "unknown";
            details["attached"] = result?.Attached ?? false;
            details["targetResolved"] = targetResolved;
            details["targetKey"] = result?.TargetKey ?? string.Empty;
            details["importMode"] = result?.ImportMode ?? string.Empty;
            details["attachOutcome"] = diagnostics != null && diagnostics.Metadata.TryGetValue("attachOutcome", out var attachOutcomeObj)
                ? attachOutcomeObj as string ?? string.Empty
                : string.Empty;
            details["requestedTargetResolved"] = diagnostics != null
                && diagnostics.Metadata.TryGetValue("requestedTargetResolved", out var requestedResolvedObj)
                && requestedResolvedObj is bool requestedResolvedBool
                && requestedResolvedBool;
            details["fallbackTargetUsed"] = diagnostics != null
                && diagnostics.Metadata.TryGetValue("fallbackTargetUsed", out var fallbackUsedObj)
                && fallbackUsedObj is bool fallbackUsedBool
                && fallbackUsedBool;
            details["fallbackUsed"] = result?.FallbackUsed ?? false;
            details["fallbackStage"] = result?.FallbackStage ?? string.Empty;
            details["fallbackReason"] = result?.FallbackReason ?? string.Empty;
            details["entriesRequested"] = result?.EntriesRequested ?? 0;
            details["entriesImported"] = result?.EntriesImported ?? 0;
            details["rebuildCompleted"] = result?.RebuildCompleted ?? false;
            details["rebuildDegraded"] = result?.RebuildDegraded ?? true;
            details["attachRequested"] = result?.AttachRequested ?? false;
            details["attachFailedAfterRebuild"] = result?.AttachFailedAfterRebuild ?? false;

            return new TreeRestoreResult
            {
                RootItem = result?.RootItem,
                Attached = result?.Attached ?? false,
                TargetResolved = targetResolved,
                AttachedIndex = result?.AttachedIndex ?? -1,
                StrategyUsed = result?.StrategyUsed ?? result?.ResolveStrategyLabel() ?? "unknown",
                ImportMode = result?.ImportMode ?? string.Empty,
                Diagnostics = details,
            };
        }

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
        private static readonly ConcurrentDictionary<string, byte> s_reportedFacadeFailures = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        private static void ReportFacadeFailureOnce(string operation, Exception ex)
        {
            if (string.IsNullOrEmpty(operation) || ex == null) return;
            if (!s_reportedFacadeFailures.TryAdd(operation, 0)) return;
            Log.Warn($"[IMK.Facade] {operation} degraded: {ex.GetType().Name}: {ex.Message}");
        }

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
        /// <summary>
        /// 刷新指定物品的脏写入（可选强制）。
        /// </summary>
        /// <remarks>
        /// 这是显式刷写入口。
        /// 若底层调度器自身抛错，异常会继续按调用链向上传递；该 facade 不做吞异常兼容。
        /// </remarks>
        public static void FlushDirty(object item, bool force = false) => PersistenceScheduler.Flush(item, force);
        /// <summary>
        /// 刷新所有脏队列；reason="manual-deferred" 时为延迟/分片刷新。
        /// </summary>
        /// <remarks>
        /// 这是兼容型 facade helper。
        /// 若内部刷新失败，当前方法保持 void 签名不变，不会向调用方抛出兼容层异常；
        /// 失败信息会通过一次性 facade diagnostics 日志输出，便于后置 mod 作者判断是 deferred 调度失败还是整体 flush 失败。
        /// </remarks>
        public static void FlushAllDirty(string reason = "manual")
        {
            try
            {
                if (reason == "manual-deferred")
                {
                    try { PersistenceScheduler.GetType().GetMethod("RequestFlushAllDeferred", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.Invoke(PersistenceScheduler, null); }
                    catch (Exception ex) { ReportFacadeFailureOnce("FlushAllDirty.deferred", ex); }
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
        /// <summary>宿主关系服务，用于从 handle 反查 owner、inventory、slot 等上层结构。</summary>
        public static IOwnershipService Ownership { get; } = new ItemModKit.Adapters.Duckov.Locator.DuckovOwnershipService();

        /// <summary>逻辑 ID 映射服务，用于跨 replace/rebirth 维持 handle 身份连续性。</summary>
        public static ILogicalIdMap LogicalIds { get; } = new ItemModKit.Adapters.Duckov.Locator.DuckovLogicalIdMap();

        /// <summary>新的 UI 选中项入口，返回 IItemHandle 而不是裸对象。</summary>
        public static IUISelectionV2 UISelectionV2 { get; } = new DuckovUISelectionV2Adapter();

        /// <summary>新的 locator/query 入口，提供 handle 语义、范围过滤和谓词式查询能力。</summary>
        public static LocatorQuery QueryV2 { get; } = new ItemModKit.Adapters.Duckov.Locator.DuckovItemQueryV2();
        private static readonly DuckovInventoryClassifier s_inventoryClassifier = new DuckovInventoryClassifier();
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
            catch (Exception ex) { ReportFacadeFailureOnce("IncrementalRefresh", ex); }
        }
        /// <summary>
        /// 获取最近一次 QueryV2 查询的耗时和谓词统计。
        /// </summary>
        /// <returns>
        /// 成功时返回最近一次查询的 `(ticks, results, source, predicates)`；
        /// 若 QueryV2 不可用或读取统计失败，则返回 `(0, 0, 0, 0)`。
        /// </returns>
        /// <remarks>
        /// 失败时会记录一次性 facade diagnostics 日志，而不是静默吞掉。
        /// </remarks>
        public static (long ticks, int results, int source, int predicates) QueryDiagnostics()
        {
            try
            {
                var q = QueryV2 as ItemModKit.Adapters.Duckov.Locator.DuckovItemQueryV2;
                if (q == null) return (0,0,0,0);
                return (q.LastQueryTicks, q.LastResultCount, q.LastSourceSize, q.LastPredicateCount);
            }
            catch (Exception ex) { ReportFacadeFailureOnce("QueryDiagnostics", ex); return (0,0,0,0); }
        }

        /// <summary>
        /// 汇总当前运行时已知 access plan 的环境诊断信息。
        /// </summary>
        /// <returns>
        /// 成功时返回包含 `stats`、`modifiers`、`slots` 三类能力摘要的 diagnostics 字典。
        /// 若当前没有可采样物品，也会返回摘要对象，但各能力通常会标记为 `unknown`。
        /// 发生 facade 级失败时返回空字典。
        /// </returns>
        /// <remarks>
        /// 这是阶段 4 的环境自检入口。
        /// 返回结果会优先基于当前已知物品样本初始化 access plan，然后给出 `complete`、`degraded` 或 `unknown` 摘要，
        /// 便于后置 mod 或内部工具在真正执行写入前先检查运行时约束。
        /// </remarks>
        public static Dictionary<string, object> QueryAccessPlanDiagnostics()
        {
            try
            {
                var samples = new List<object>();
                var seen = new HashSet<object>();

                void AddSample(object sample)
                {
                    if (sample == null) return;
                    if (seen.Add(sample)) samples.Add(sample);
                }

                try
                {
                    if (UISelection != null && UISelection.TryGetCurrentItem(out var selected) && selected != null)
                    {
                        AddSample(selected);
                    }
                }
                catch (Exception ex)
                {
                    ReportFacadeFailureOnce("QueryAccessPlanDiagnostics.sampleSelection", ex);
                }

                try
                {
                    foreach (var item in EnumerateAllKnownItems())
                    {
                        AddSample(item);
                        if (samples.Count >= 64) break;
                    }
                }
                catch (Exception ex)
                {
                    ReportFacadeFailureOnce("QueryAccessPlanDiagnostics.enumerateKnownItems", ex);
                }

                return WriteService.CollectAccessPlanDiagnostics(samples);
            }
            catch (Exception ex)
            {
                ReportFacadeFailureOnce("QueryAccessPlanDiagnostics", ex);
                return new Dictionary<string, object>();
            }
        }

        internal static void ResetQueryIndex()
        {
            try { (QueryV2 as ItemModKit.Adapters.Duckov.Locator.DuckovItemQueryV2)?.Clear(); } catch { }
        }
        /// <summary>
        /// 用调用方提供的枚举器重建 QueryV2 索引。
        /// 适合在外部知道“全量物品集合”时主动触发重索引。
        /// </summary>
        /// <remarks>
        /// 这是兼容型 facade helper。
        /// 重建过程中若单个对象无法转成 handle，会跳过该对象并记录一次性 diagnostics；
        /// 若整次重建失败，当前方法仍保持 void 返回，但会输出一次性 facade diagnostics 日志。
        /// </remarks>
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
                        catch (Exception ex) { ReportFacadeFailureOnce("ReindexAll.registerHandle", ex); }
                    }
                }
            }
            catch (Exception ex) { ReportFacadeFailureOnce("ReindexAll", ex); }
        }
        private static IEnumerable<object> DefaultEnumerateAll()
        {
            var set = new System.Collections.Generic.HashSet<object>();
            foreach (var inventory in EnumerateKnownInventories())
            {
                foreach (var item in EnumerateInventoryItems(inventory))
                {
                    if (item != null) set.Add(item);
                }
            }
            return set;
        }
        internal static IEnumerable<object> EnumerateAllKnownItems()
        {
            return DefaultEnumerateAll();
        }

        internal static object GetCharacterInventory()
        {
            try
            {
                var tCharacterMain = DuckovTypeUtils.FindType("CharacterMainControl") ?? DuckovTypeUtils.FindType("TeamSoda.Duckov.Core.CharacterMainControl");
                var main = tCharacterMain?.GetProperty("Main", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null, null);
                var charItem = main != null ? DuckovReflectionCache.GetGetter(main.GetType(), "CharacterItem", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.Invoke(main) : null;
                return charItem != null ? DuckovReflectionCache.GetGetter(charItem.GetType(), "Inventory", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(charItem) : null;
            }
            catch (Exception ex) { ReportFacadeFailureOnce("GetCharacterInventory", ex); return null; }
        }

        internal static object GetStorageInventory()
        {
            try
            {
                var tPlayerStorage = DuckovTypeUtils.FindType("TeamSoda.Duckov.Core.PlayerStorage") ?? DuckovTypeUtils.FindType("PlayerStorage");
                return tPlayerStorage?.GetProperty("Inventory", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null, null);
            }
            catch (Exception ex) { ReportFacadeFailureOnce("GetStorageInventory", ex); return null; }
        }

        internal static bool TryGetInventoryItem(object inventory, int index1Based, out object item)
        {
            item = null;
            if (inventory == null) return false;

            var indexer = inventory.GetType().GetMethod("get_Item", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (indexer == null) return false;

            var index0Based = Math.Max(0, index1Based - 1);
            try { item = indexer.Invoke(inventory, new object[] { index0Based }); }
            catch (Exception ex) { ReportFacadeFailureOnce("TryGetInventoryItem", ex); item = null; }
            return item != null;
        }

        internal static bool TryGetWeaponSlotItem(int slotIndex1Based, out object item)
        {
            item = null;
            try
            {
                var tCharacterMain = DuckovTypeUtils.FindType("CharacterMainControl") ?? DuckovTypeUtils.FindType("TeamSoda.Duckov.Core.CharacterMainControl");
                var main = tCharacterMain?.GetProperty("Main", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null, null);
                var charItem = main != null ? DuckovReflectionCache.GetGetter(main.GetType(), "CharacterItem", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.Invoke(main) : null;
                var slots = charItem != null ? DuckovReflectionCache.GetGetter(charItem.GetType(), "Slots", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(charItem) as System.Collections.IEnumerable : null;
                if (slots == null) return false;

                var index = 1;
                foreach (var slot in slots)
                {
                    if (index++ != slotIndex1Based) continue;
                    item = slot != null ? DuckovReflectionCache.GetGetter(slot.GetType(), "Content", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(slot) : null;
                    return item != null;
                }
            }
            catch (Exception ex) { ReportFacadeFailureOnce("TryGetWeaponSlotItem", ex); }
            return false;
        }

        private static IEnumerable<object> EnumerateKnownInventories()
        {
            var seen = new System.Collections.Generic.HashSet<object>();

            void AddInventory(object inventory)
            {
                if (inventory != null) seen.Add(inventory);
            }

            AddInventory(GetCharacterInventory());
            AddInventory(GetStorageInventory());

            try
            {
                foreach (var inventory in s_inventoryClassifier.EnumerateLootBoxes()) AddInventory(inventory);
            }
            catch (Exception ex) { ReportFacadeFailureOnce("EnumerateKnownInventories.EnumerateLootBoxes", ex); }

            return seen;
        }

        private static IEnumerable<object> EnumerateInventoryItems(object inventory)
        {
            if (inventory == null) yield break;

            int capacity = 0;
            try { capacity = Convert.ToInt32(inventory.GetType().GetProperty("Capacity", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(inventory, null) ?? 0); }
            catch (Exception ex) { ReportFacadeFailureOnce("EnumerateInventoryItems.Capacity", ex); capacity = 0; }

            var indexer = inventory.GetType().GetMethod("get_Item", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (indexer == null) yield break;

            for (int index = 0; index < capacity; index++)
            {
                object item = null;
                try { item = indexer.Invoke(inventory, new object[] { index }); }
                catch (Exception ex) { ReportFacadeFailureOnce("EnumerateInventoryItems.get_Item", ex); item = null; }
                if (item != null) yield return item;
            }
        }
        /// <summary>
        /// 使用内置 inventory 枚举逻辑重建 QueryV2 索引。
        /// </summary>
        /// <remarks>
        /// 返回语义与 `ReindexAll(Func&lt;IEnumerable&lt;object&gt;&gt;)` 一致：
        /// 失败时不抛出兼容层异常，但会输出一次性 facade diagnostics。
        /// </remarks>
        public static void ReindexAll()
        {
            ReindexAll(DefaultEnumerateAll);
        }

        /// <summary>
        /// 尝试获取当前 UI 选中的 handle。
        /// 成功时会自动注册到内部 handle 索引，便于后续 QueryV2 和 LogicalIds 复用。
        /// </summary>
        /// <returns>
        /// 成功时返回当前选中的 `IItemHandle`；
        /// 若当前没有可解析的选中项，或解析过程中发生错误，则返回 `null`。
        /// </returns>
        /// <remarks>
        /// 失败时会输出一次性 facade diagnostics 日志，帮助区分“当前确实没有选中项”和“选中项解析链路出错”。
        /// </remarks>
        public static IItemHandle TryGetCurrentSelectedHandle()
        {
            try
            {
                if (UISelectionV2.TryGetCurrent(out var h) && h != null) { RegisterHandle(h); return h; }
            }
            catch (Exception ex) { ReportFacadeFailureOnce("TryGetCurrentSelectedHandle", ex); }
            return null;
        }

        /// <summary>
        /// 刷新指定 handle 的缓存元数据，不重建 handle 本身。
        /// </summary>
        /// <remarks>
        /// 传入 `null` 时直接忽略。
        /// 若刷新过程中失败，当前方法保持 void 返回，但会输出一次性 facade diagnostics 日志。
        /// </remarks>
        public static void RefreshHandleMetadata(IItemHandle handle)
        {
            if (handle == null) return;
            try { handle.RefreshMetadata(); }
            catch (Exception ex) { ReportFacadeFailureOnce("RefreshHandleMetadata", ex); }
        }

        /// <summary>
        /// 按裸对象查找已经注册过的 handle；未注册时返回 null。
        /// </summary>
        /// <returns>
        /// 若该裸对象已注册到内部 handle 索引，则返回对应 `IItemHandle`；否则返回 `null`。
        /// </returns>
        public static IItemHandle TryGetHandle(object raw)
        {
            if (raw == null) return null; s_handleMap.TryGetValue(raw, out var h); return h;
        }

        /// <summary>
        /// 按 InstanceId 查找已经注册过的 handle；未命中时返回 null。
        /// </summary>
        /// <returns>
        /// 命中时返回对应 `IItemHandle`；未命中或遍历过程中发生错误时返回 `null`。
        /// </returns>
        /// <remarks>
        /// 失败时会输出一次性 facade diagnostics 日志，而不是继续保持完全无声。
        /// </remarks>
        public static IItemHandle TryGetHandleByInstanceId(int iid)
        {
            try
            {
                foreach (var kv in s_handleMap)
                {
                    var h = kv.Value; if (h?.InstanceId == iid) return h;
                }
            }
            catch (Exception ex) { ReportFacadeFailureOnce("TryGetHandleByInstanceId", ex); }
            return null;
        }
        /// <summary>
        /// 获取 QueryV2 的累计性能统计。
        /// </summary>
        /// <returns>
        /// 成功时返回 `(avgMs, maxMs, queries)`；
        /// 若 QueryV2 不可用或统计读取失败，则返回 `(0, 0, 0)`。
        /// </returns>
        /// <remarks>
        /// 失败时会输出一次性 facade diagnostics 日志。
        /// </remarks>
        public static (double avgMs, double maxMs, int queries) QueryPerfStats()
        {
            try
            {
                var q = QueryV2 as ItemModKit.Adapters.Duckov.Locator.DuckovItemQueryV2; return q != null ? q.SnapshotPerf() : (0,0,0);
            }
            catch (Exception ex) { ReportFacadeFailureOnce("QueryPerfStats", ex); return (0,0,0); }
        }
        /// <summary>
        /// 手动刷新世界掉落扫描（若需要强制遍历）。常规情况下事件桥会提前登记，不必频繁调用。
        /// </summary>
        /// <remarks>
        /// 这是兼容型 facade helper。
        /// 若刷新请求失败，当前方法保持 void 返回，但会输出一次性 facade diagnostics 日志。
        /// </remarks>
        public static void ForceWorldDropRescan()
        {
            try { WorldDrops?.RegisterExternalWorldItem(null); }
            catch (Exception ex) { ReportFacadeFailureOnce("ForceWorldDropRescan", ex); }
        }
    }
}
