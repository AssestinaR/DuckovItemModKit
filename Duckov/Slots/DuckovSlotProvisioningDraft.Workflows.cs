using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// Duckov 侧补槽草案（流程入口）：
    /// 负责执行补槽请求、回放持久化定义，以及从运行时反向同步动态槽位定义。
    /// </summary>
    public static partial class DuckovSlotProvisioningDraft
    {
        /// <summary>补槽草案默认持久化变量键。</summary>
        public const string DefaultPersistenceVariableKey = "IMK_Meta.DynamicSlotsDraft";

        /// <summary>
        /// 执行补槽草案请求。
        /// 该流程会校验宿主、确保槽位宿主存在、合并槽位定义、写入草案持久化并刷新运行时状态。
        /// </summary>
        /// <param name="request">补槽请求对象。</param>
        /// <returns>成功时返回包含诊断信息的补槽结果；失败时返回对应错误码与错误信息。</returns>
        public static RichResult<EnsureSlotsResult> EnsureSlots(EnsureSlotsRequest request)
        {
            if (request == null)
            {
                return RichResult<EnsureSlotsResult>.Fail(ErrorCode.InvalidArgument, "ensure-slots.request-null");
            }

            if (request.OwnerItem == null)
            {
                return RichResult<EnsureSlotsResult>.Fail(ErrorCode.InvalidArgument, "ensure-slots.owner-null");
            }

            if (request.DesiredSlots == null || request.DesiredSlots.Length == 0)
            {
                return RichResult<EnsureSlotsResult>.Fail(ErrorCode.InvalidArgument, "ensure-slots.no-desired-slots");
            }

            var diagnostics = new EnsureSlotsDiagnostics
            {
                StartedAtUtc = DateTime.UtcNow,
            };
            var timings = new Dictionary<SlotProvisioningPhase, Stopwatch>();
            var createdKeys = new List<string>();
            var reusedKeys = new List<string>();
            var rejectedKeys = new List<string>();

            try
            {
                StartPhase(SlotProvisioningPhase.ResolveOwner, timings);
                var ownerItem = request.OwnerItem;
                var slots = GetSlotsObject(ownerItem);
                CompletePhase(SlotProvisioningPhase.ResolveOwner, diagnostics, timings);

                StartPhase(SlotProvisioningPhase.EnsureSlotHost, timings);
                if (slots == null && request.CreateSlotHostIfMissing)
                {
                    diagnostics.SlotHostCreated = TryInvokeCreateSlotsComponent(ownerItem);
                    slots = GetSlotsObject(ownerItem);
                }

                if (slots == null)
                {
                    diagnostics.CompletedAtUtc = DateTime.UtcNow;
                    return RichResult<EnsureSlotsResult>.Fail(ErrorCode.NotSupported, "ensure-slots.owner-has-no-slot-host");
                }

                CompletePhase(SlotProvisioningPhase.EnsureSlotHost, diagnostics, timings);

                StartPhase(SlotProvisioningPhase.MergeDefinitions, timings);
                foreach (var definition in request.DesiredSlots)
                {
                    if (definition == null || string.IsNullOrEmpty(definition.Key))
                    {
                        continue;
                    }

                    if (ResolveSlot(slots, definition.Key) != null)
                    {
                        if (definition.ReuseExistingIfPresent)
                        {
                            reusedKeys.Add(definition.Key);
                            continue;
                        }

                        rejectedKeys.Add(definition.Key);
                        RollbackCreatedSlots(ownerItem, createdKeys);
                        diagnostics.CompletedAtUtc = DateTime.UtcNow;
                        return RichResult<EnsureSlotsResult>.Fail(ErrorCode.Conflict, "ensure-slots.slot-already-exists");
                    }

                    var templateSlot = ResolveTemplateSlot(slots, definition.Template);
                    if (templateSlot != null)
                    {
                        diagnostics.TemplateUsed = true;
                    }

                    var createOptions = BuildCreateOptions(definition, templateSlot);
                    var add = IMKDuckov.Write.TryAddSlot(ownerItem, createOptions);
                    if (!add.Ok)
                    {
                        rejectedKeys.Add(definition.Key);
                        RollbackCreatedSlots(ownerItem, createdKeys);
                        diagnostics.CompletedAtUtc = DateTime.UtcNow;
                        return RichResult<EnsureSlotsResult>.Fail(add.Code, string.IsNullOrEmpty(add.Error) ? "ensure-slots.add-slot-failed" : add.Error);
                    }

                    createdKeys.Add(definition.Key);
                }

                CompletePhase(SlotProvisioningPhase.MergeDefinitions, diagnostics, timings);

                StartPhase(SlotProvisioningPhase.PersistMetadata, timings);
                var metadataPersisted = !request.PersistDefinitionsToVariables || TryPersistDefinitions(ownerItem, request);
                diagnostics.MetadataPersisted = metadataPersisted;
                if (!metadataPersisted)
                {
                    RollbackCreatedSlots(ownerItem, createdKeys);
                    diagnostics.CompletedAtUtc = DateTime.UtcNow;
                    return RichResult<EnsureSlotsResult>.Fail(ErrorCode.OperationFailed, "ensure-slots.persist-failed");
                }

                CompletePhase(SlotProvisioningPhase.PersistMetadata, diagnostics, timings);

                StartPhase(SlotProvisioningPhase.RefreshRuntime, timings);
                var dirtyMarked = false;
                var persistenceFlushed = false;
                if (request.MarkDirty)
                {
                    using (IMKDuckov.AllowDirtyFromWriteService())
                    {
                        IMKDuckov.MarkDirty(ownerItem, DirtyKind.Slots | DirtyKind.Variables, immediate: request.ForceFlushPersistence);
                        dirtyMarked = true;
                        if (request.ForceFlushPersistence)
                        {
                            IMKDuckov.FlushDirty(ownerItem, force: true);
                            persistenceFlushed = true;
                        }
                    }
                }

                if (request.RefreshUI)
                {
                    TryRefreshOwnerInventory(ownerItem);
                }

                if (request.PublishEvents)
                {
                    IMKDuckov.PublishItemChanged(ownerItem);
                }

                CompletePhase(SlotProvisioningPhase.RefreshRuntime, diagnostics, timings);
                diagnostics.CompletedAtUtc = DateTime.UtcNow;

                return RichResult<EnsureSlotsResult>.Success(new EnsureSlotsResult
                {
                    FinalPhase = SlotProvisioningPhase.Completed,
                    OwnerItem = ownerItem,
                    CreatedSlotKeys = createdKeys.ToArray(),
                    ReusedSlotKeys = reusedKeys.ToArray(),
                    RejectedSlotKeys = rejectedKeys.ToArray(),
                    SlotHostCreated = diagnostics.SlotHostCreated,
                    MetadataPersisted = diagnostics.MetadataPersisted,
                    RuntimeRefreshTriggered = request.RefreshUI || request.PublishEvents || dirtyMarked,
                    DirtyMarked = dirtyMarked,
                    PersistenceFlushed = persistenceFlushed,
                    Diagnostics = diagnostics,
                });
            }
            catch (Exception ex)
            {
                RollbackCreatedSlots(request.OwnerItem, createdKeys);
                diagnostics.Metadata["Exception"] = ex.ToString();
                diagnostics.CompletedAtUtc = DateTime.UtcNow;
                return RichResult<EnsureSlotsResult>.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>
        /// 判断目标物品上是否存在补槽草案持久化定义。
        /// </summary>
        /// <param name="ownerItem">目标物品。</param>
        /// <param name="variableKey">持久化变量键；为 null 时使用默认键。</param>
        /// <returns>存在可读定义时返回 true；否则返回 false。</returns>
        public static bool HasPersistedDefinitions(object ownerItem, string variableKey = null)
        {
            if (ownerItem == null)
            {
                return false;
            }

            var effectiveKey = string.IsNullOrEmpty(variableKey) ? DefaultPersistenceVariableKey : variableKey;
            try
            {
                var raw = IMKDuckov.Item.GetVariable(ownerItem, effectiveKey) as string;
                return !string.IsNullOrEmpty(raw);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 从补槽草案变量 JSON 回放动态槽位定义。
        /// 该入口主要用于持久化加载路径，因此默认不改写变量也不主动发布事件。
        /// </summary>
        /// <param name="ownerItem">目标物品。</param>
        /// <param name="variableKey">持久化变量键；为 null 时使用默认键。</param>
        /// <returns>成功应用任意槽位定义或移除标记时返回 true；否则返回 false。</returns>
        public static bool TryApplyPersistedDefinitions(object ownerItem, string variableKey = null)
        {
            if (ownerItem == null)
            {
                return false;
            }

            var effectiveKey = string.IsNullOrEmpty(variableKey) ? DefaultPersistenceVariableKey : variableKey;
            var persisted = ReadPersistedData(ownerItem, effectiveKey);
            if (persisted == null)
            {
                return false;
            }

            var desired = new List<SlotProvisionDefinition>();
            for (var index = 0; index < persisted.Slots.Count; index++)
            {
                var entry = persisted.Slots[index];
                if (entry == null || string.IsNullOrEmpty(entry.Key))
                {
                    continue;
                }

                desired.Add(new SlotProvisionDefinition
                {
                    Key = entry.Key,
                    DisplayName = entry.DisplayName,
                    RequireTags = entry.RequireTags,
                    ExcludeTags = entry.ExcludeTags,
                    ForbidItemsWithSameID = entry.ForbidItemsWithSameID,
                    ReuseExistingIfPresent = true,
                    Template = !string.IsNullOrEmpty(entry.TemplateSlotKey)
                        ? new SlotProvisionTemplateReference
                        {
                            TemplateSlotKey = entry.TemplateSlotKey,
                            CloneFilters = true,
                            CloneIcon = true,
                            CloneDisplayName = false,
                        }
                        : null,
                });
            }

            var applied = false;
            if (desired.Count > 0)
            {
                var apply = EnsureSlots(new EnsureSlotsRequest
                {
                    OwnerItem = ownerItem,
                    DesiredSlots = desired.ToArray(),
                    CreateSlotHostIfMissing = true,
                    PersistDefinitionsToVariables = false,
                    PersistenceVariableKey = effectiveKey,
                    RefreshUI = false,
                    PublishEvents = false,
                    MarkDirty = false,
                    ForceFlushPersistence = false,
                    CallerTag = "ItemModKit.Persistence.ApplyPersistedSlotProvisioning",
                });
                if (!apply.Ok)
                {
                    return false;
                }

                applied = true;
            }

            if (TryApplyRemovedBuiltinSlots(ownerItem, persisted.RemovedBuiltinSlotKeys))
            {
                applied = true;
            }

            return applied;
        }

        /// <summary>
        /// 从当前运行时槽位状态反向同步动态槽位定义到持久化草案。
        /// 该过程只同步 Dynamic 槽位条目，不会重新生成原版槽位移除标记。
        /// </summary>
        /// <param name="ownerItem">目标物品。</param>
        /// <param name="variableKey">持久化变量键；为 null 时使用默认键。</param>
        /// <returns>同步并写回成功时返回 true；否则返回 false。</returns>
        public static bool TrySyncPersistedDefinitionsFromRuntime(object ownerItem, string variableKey = null)
        {
            if (ownerItem == null)
            {
                return false;
            }

            var effectiveKey = string.IsNullOrEmpty(variableKey) ? DefaultPersistenceVariableKey : variableKey;
            var persisted = ReadPersistedData(ownerItem, effectiveKey);
            if (persisted == null)
            {
                return false;
            }

            NormalizePersistedData(persisted);
            var slots = GetSlotsObject(ownerItem);
            var syncedDynamicSlots = new List<SlotPersistenceSlotDefinition>();
            for (var index = 0; index < persisted.Slots.Count; index++)
            {
                var entry = persisted.Slots[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                {
                    continue;
                }

                var slot = ResolveSlot(slots, entry.Key);
                if (slot == null)
                {
                    continue;
                }

                syncedDynamicSlots.Add(new SlotPersistenceSlotDefinition
                {
                    Key = entry.Key,
                    DisplayName = entry.DisplayName,
                    TemplateSlotKey = entry.TemplateSlotKey,
                    RequireTags = ReadStringArray(slot, "requireTags") ?? ReadStringArray(slot, "RequireTags") ?? entry.RequireTags,
                    ExcludeTags = ReadStringArray(slot, "excludeTags") ?? ReadStringArray(slot, "ExcludeTags") ?? entry.ExcludeTags,
                    ForbidItemsWithSameID = ReadBool(slot, "forbidItemsWithSameID") ?? ReadBool(slot, "ForbidItemsWithSameID") ?? entry.ForbidItemsWithSameID,
                    OriginHint = SlotPersistenceOriginHint.Dynamic,
                });
            }

            persisted.SchemaVersion = SlotPersistenceDraftSchema.CurrentVersion;
            persisted.Slots = syncedDynamicSlots.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase).ToList();
            return WritePersistedData(ownerItem, effectiveKey, persisted);
        }
    }
}