# IMK Stage 1 API Freeze

This document enumerates the public contract surface considered STABLE for Stage 1 (do not change signatures / semantics in minor patches).

Stable Namespaces/Types:
- ItemModKit.Core (DTOs, interfaces, snapshot utilities)
- ItemModKit.Adapters.Duckov.IMKDuckov (static facade)

Stable DTOs (all marked [Serializable]):
- CoreFields
- CoreFieldChanges
- StackInfo, DurabilityInfo, InspectionInfo, OrderingInfo, WeightInfo, RelationInfo, FlagsInfo
- StatValueEntry, StatsSnapshot
- InventorySnapshot
- EffectEntry, EffectInfo, EffectCreateOptions
- ItemGraphicSnapshot, AgentUtilitiesSnapshot
- SlotCreateOptions, SlotUpdateOptions
- ItemSnapshot (Capture() + ToPrettyString())
 - SlotEntry (extended fields: SlotIcon, RequireTagKeys, ExcludeTagKeys, ForbidSameID, ContentTypeId, ContentName, OriginHint)

Stable Service Interfaces:
- IReadService
  - TryReadCoreFields / Variables / Constants / Modifiers / Tags / Slots
  - TryReadStackInfo / DurabilityInfo / InspectionInfo / OrderingInfo / WeightInfo / Relations / Flags / SoundKey
  - TryReadStats / TryReadChildInventoryInfo / TryEnumerateChildInventoryItems
  - TryReadEffects / TryReadItemGraphic / TryReadAgentUtilities
  - TryReadEffectsDetailed / TryReadModifierDescriptions
  - Snapshot
- IWriteService
  - Core: TryWriteCoreFields
  - Variables / Constants / Tags: TryWriteVariables / TryWriteConstants / TryWriteTags
  - Stack & Durability: TrySetStackCount / TrySetMaxStack / TrySetDurability / TrySetMaxDurability / TrySetDurabilityLoss
  - Inspection: TrySetInspected / TrySetInspecting
  - Ordering / Sound / Weight / Icon: TrySetOrder / TrySetSoundKey / TrySetWeight / TrySetIcon
  - Child Inventory: TryAddToChildInventory / TryRemoveFromChildInventory / TryMoveInChildInventory / TryClearChildInventory
  - Modifiers (raw): TryAddModifier / TryRemoveAllModifiersFromSource / TryReapplyModifiers
  - Modifier Descriptions: TryAddModifierDescription / TryRemoveModifierDescription / TrySetModifierDescriptionValue / TrySetModifierDescriptionType / TrySetModifierDescriptionOrder / TrySetModifierDescriptionDisplay / TryClearModifierDescriptions / TrySanitizeModifierDescriptions
  - Effects: TryAddEffect (both overloads) / TryRemoveEffect / TryEnableEffect / TrySetEffectProperty / TryAddEffectComponent / TryRemoveEffectComponent / TrySetEffectComponentProperty
  - Slots: TryPlugIntoSlot / TryUnplugFromSlot / TryMoveBetweenSlots / TryAddSlot / TryEnsureSlotHost / TryEnsureSlots / TryRemoveSlot / TryRemoveSlotHost / TryRemoveDynamicSlot / TryRemoveBuiltinSlot / TryRemoveSlots / TryRemoveSlotSystem / TrySetSlotTags
  - Stats: TrySetStatValue / TryEnsureStat / TryRemoveStat
  - Metadata: TrySetVariableMeta / TrySetConstantMeta
  - Transactions: BeginTransaction / CommitTransaction / RollbackTransaction

Stable Facade Members (IMKDuckov):
- Item / Inventory / Slot / Query / Persistence / Rebirth / UISelection / ItemEvents / WorldDrops
- Read / Write / Factory / Mover / Clone / VariableMerge / UIRefresh
- InventoryResolver / InventoryPlacement / PersistenceScheduler
- Version / Capabilities / Require
- Ownership / LogicalIds / UISelectionV2 / QueryV2
- MarkDirty / FlushDirty / FlushAllDirty
- BeginExternalEvents / EndExternalEvents / PublishItemAdded/Removed/Changed/Moved/Merged/Split

Added Helper (Stage 1):
- SnapshotHelper (CaptureCore / RollbackCore). Considered stable for Stage 1.

Semantics Freeze:
- RichResult.Ok true means operation succeeded and performed requested mutation (or data returned). No silent partial success.
- TryWriteCoreFields must perform full rollback to original values if any field write fails.
- Transactions: BeginTransaction returns token; CommitTransaction returns RichResult (Ok on success), RollbackTransaction reverts all writes since BeginTransaction for the given item+token.
- MarkDirty may be a deferred persistence request; FlushDirty(item, force=false) triggers immediate persistence attempt; FlushAllDirty(reason) flushes all queued.

Backward Compatibility Rules:
1. Do not remove, rename, or change parameter order of any method/type listed.
2. Adding new optional members (nullable payload fields) is allowed if defaults preserve behavior.
3. New interfaces or extension helpers must not depend on experimental namespaces unless described.
4. RichResult failure codes must remain within existing ErrorCode enum values (no repurposing meanings).

Change Process:
- Stage 1 modifications can only add new surface (additive) and must update this doc under a new section "Stage 1 Additions".
- Breaking proposals require drafting Stage 2 plan and migration notes before implementation.

Diagnostics (non-frozen): PerfCounters, PerfFlushMetrics may change internally without affecting contract.

End of Stage 1 API Freeze.

## Stage 1 Additions (Slot Refactor)
Additive, non-breaking enhancements:
1. SlotEntry struct gained optional metadata fields (default-safe): SlotIcon, RequireTagKeys, ExcludeTagKeys, ForbidSameID, ContentTypeId, ContentName, OriginHint.
2. Internal slot write logic now uses game signature Plug(Item,out Item) with CanPlug pre-check; error messages standardized using slot.* prefixes.
3. Experimental helper methods (not frozen): TrySetSlotIcon. This may change or be removed; treat as optional.
4. SlotCreateOptions.DisplayName currently ignored (game derives DisplayName from first requireTag). Field retained for future compatibility.
5. RichResult semantics unchanged: Ok only on full success.
6. Stable slot additions: TryEnsureSlotHost provides a direct stable path to initialize a slot host on items that currently have no Slots component, TryEnsureSlots provides a direct stable combination path for host initialization plus missing-slot provisioning, TryRemoveSlotHost removes an empty slot host and clears the default dynamic-slot persistence key, TryRemoveDynamicSlot removes IMK-persisted dynamic slots from both runtime and draft metadata, TryRemoveBuiltinSlot records builtin-slot tombstones so runtime removal can survive restart, TryRemoveSlots provides a stable batch-removal entry, TryRemoveSlotSystem removes a fully emptied slot system as a single workflow, and TrySetSlotTags is part of the stable slot write surface.

## Stage 1 Additions (Effects Deep/Helpers)
- New read API: IReadService.TryReadEffectsDeep to enumerate effects and component-level properties (primitive fields/properties only), for diagnostics/UI.
- New helper writes (non-breaking additions on IWriteService):
  - TryRenameEffect / TrySetEffectDisplay / TrySetEffectDescription
  - TryMoveEffect (reorder effects) / TryMoveEffectComponent (reorder triggers/filters/actions)
  - TrySanitizeEffects (remove nulls, clean invalid references)
These are additive. Defaults preserve previous behavior. External mods can opt-in; no change required for existing code.

### Migration notes for UI mods
- Data source should prefer TryReadEffectsDeep, fallback to TryReadEffectsDetailed, then TryReadEffects.
- Editing UI:
  - Toggle Enabled => Write.TryEnableEffect
  - Toggle Display / Edit Description => Write.TrySetEffectDisplay / TrySetEffectDescription
  - Rename => Write.TryRenameEffect
  - Reorder (drag & drop): Write.TryMoveEffect (for effects), Write.TryMoveEffectComponent (for components)
  - Property editing for components => Write.TrySetEffectComponentProperty
- Consistency:
  - After writes, re-read with TryReadEffectsDeep or call existing UI refresh.
  - Use transactions for batch edits to provide atomic UX.
- Compatibility:
  - If some fields (Display/Description) are missing on a game build, hide corresponding controls.
  - Deep read is optional; fallbacks ensure UI remains usable on older environments.

## Stage 1 Additions (Facade Guidance)
- `IMKDuckov.Query` and `IMKDuckov.UISelection` remain part of the frozen Stage 1 facade.
- New code should prefer `IMKDuckov.QueryV2`, `IMKDuckov.UISelectionV2`, and `IMKDuckov.TryGetCurrentSelectedHandle()`.
- The legacy `Query` / `UISelection` members are compatibility facades and may emit non-blocking obsolete warnings to steer new integrations onto the V2 path.

## Stage 1 Additions (Persistence Restore Guidance)
- New additive facade helpers: `IMKDuckov.RestoreFromMeta(ItemMeta)` and `IMKDuckov.RestoreFromMeta(ItemMeta, string targetKey, bool refreshUI = true)`.
- New additive detailed helpers: `IMKDuckov.RestoreFromMetaDetailed(ItemMeta)` and `IMKDuckov.RestoreFromMetaDetailed(ItemMeta, string targetKey, bool refreshUI = true)`.
- New additive DTO: `PersistenceRestoreResult` with `RootItem`, `Attached`, `TargetResolved`, `AttachedIndex`, `StrategyUsed`, `Diagnostics`.
- These helpers restore from persisted `ItemMeta` through the shared restore orchestrator.
- `targetKey == null` returns a detached root; specifying `targetKey` requests attach to a resolved inventory host.

## Stage 1 Additions (Tree Export Restore Guidance)
- New additive facade helpers: `IMKDuckov.RestoreFromTreeExport(JObject)` and `IMKDuckov.RestoreFromTreeExport(JObject, string targetKey, bool refreshUI = true)`.
- New additive detailed helpers: `IMKDuckov.RestoreFromTreeExportDetailed(JObject)` and `IMKDuckov.RestoreFromTreeExportDetailed(JObject, string targetKey, bool refreshUI = true)`.
- New additive DTO: `TreeRestoreResult` with `RootItem`, `Attached`, `TargetResolved`, `AttachedIndex`, `StrategyUsed`, `ImportMode`, `Diagnostics`.
- These helpers restore from `DuckovTreeDataService.TryExport(...)` payloads through the shared restore orchestrator.
- Current bounded v1 prefers reconstructing exported inventory/slot structure and variable values, then falls back to minimal import when tree reconstruction cannot be completed.
- Export payload now also carries `rootInstanceId` so tree restore can resolve the root explicitly instead of relying on entry ordering.
- Tree restore diagnostics now also carry `tree.fallbackUsed`, `tree.fallbackStage`, `tree.fallbackReason`, `tree.entriesRequested`, and `tree.entriesImported` metadata.
- Tree restore diagnostics also distinguish rebuild vs attach outcomes via `tree.rebuildCompleted`, `tree.rebuildDegraded`, `tree.attachRequested`, and `tree.attachFailedAfterRebuild`.
- Shared restore diagnostics also carry `attachOutcome`, `requestedTargetResolved`, and `fallbackTargetUsed` so callers can distinguish unresolved targets, fallback-target placement, deferred retry, and direct attach failure.

## Stage 1 Additions (Shared Restore Diagnostics)
- `ClonePipelineResult` gained additive property `RestoreDiagnostics` so clone success paths can expose the shared restore diagnostics object directly.
- Existing `Diagnostics` dictionary remains supported; `RestoreDiagnostics` is the canonical structured carrier for shared orchestrator diagnostics when available.

## Stage 1 Additions (Rebirth Detailed Guidance)
- New additive facade helper: `IMKDuckov.ReplaceRebirthDetailed(object oldItem, ItemMeta meta, bool keepLocation = true)`.
- New additive facade helper: `IMKDuckov.ReplaceRebirthReport(object oldItem, ItemMeta meta, bool keepLocation = true)`.
- `ReplaceRebirthDetailed(...)` / `ReplaceRebirthReport(...)` remain stable compatibility entry points and currently resolve to `RebirthIntent.SafeReplace` semantics.
- New additive facade helpers: `IMKDuckov.ReplaceSafeRebirthDetailed(object oldItem, ItemMeta meta, bool keepLocation = true)`, `IMKDuckov.ReplaceSafeRebirthReport(object oldItem, ItemMeta meta, bool keepLocation = true)`.
- New additive facade helpers: `IMKDuckov.ReplaceCleanRebirthDetailed(object oldItem, ItemMeta meta, bool keepLocation = true)`, `IMKDuckov.ReplaceCleanRebirthReport(object oldItem, ItemMeta meta, bool keepLocation = true)`.
- New additive facade helpers: `IMKDuckov.GetRecentRebirthReports(int maxCount = 20)`, `IMKDuckov.ClearRecentRebirthReports()`, and `IMKDuckov.LogRecentRebirthReports(int maxCount = 10, bool includeDiagnostics = false)`.
- New additive enum: `RebirthIntent` with `SafeReplace` and `CleanRebirth`.
- New additive DTO: `RebirthRestoreResult` with `ReportedAtUtc`, `Succeeded`, `ErrorCode`, `Error`, `RootItem`, `Attached`, `TargetResolved`, `AttachedIndex`, `StrategyUsed`, `IntentUsed`, `RollbackOutcome`, `FailureKind`, `FailureAction`, `FailurePhase`, `FailureMatrixKey`, `PolicyDecision`, `MatrixPolicyKey`, `RecoveryDisposition`, `ManualRecoveryRequired`, `OperatorAlertLevel`, `OperatorAlertCode`, `OperatorAlertMessage`, and `Diagnostics`.
- `ReplaceRebirthDetailed(...)` remains success-oriented under existing `RichResult<T>` semantics; `ReplaceRebirthReport(...)` is the failure-safe structured carrier for shared diagnostics, rebirth failure taxonomy, recovery disposition, and operator alert metadata.
- `ReplaceSafeRebirth*` and `ReplaceCleanRebirth*` are additive explicit-intent helpers; existing call sites do not need migration unless they want to opt into clean rebirth semantics.
- Recent rebirth reports are now buffered in a lightweight in-memory diagnostics queue so UI/debug tools can pull the latest replacement outcomes without scraping logs.
- The `RebirthReports` capability bit indicates that recent rebirth report buffering and log export are available through the stable facade.

## Stage 1 Additions (Draft Resource Provisioning Guidance)
- New additive facade helper: `IMKDuckov.EnsureResourceProvisionDraft(EnsureResourceProvisionRequest request)`.
- New additive DTOs: `ResourceProvisionDefinition`, `EnsureResourceProvisionRequest`, `EnsureResourceProvisionResult`, and `EnsureResourceProvisionDiagnostics`.
- New additive enums: `ResourceProvisioningMode` with `Durability` and `UseCount`, and `ResourceProvisioningPhase` for draft pipeline diagnostics.
- Current bounded v1 semantics:
  - `Durability` writes `MaxDurability`, `Durability`, and `DurabilityLoss`.
  - `UseCount` currently reuses `MaxStackCount` and `Count` as the minimal resource-like runtime representation.
  - When `PersistDefinitionToVariables=true`, the draft payload is written to `IMK_Meta.ResourceProvisionDraft` by default and is replayed on load through the persistence adapter.
- This surface is additive but still draft-oriented: suitable for Probe, internal tools, and controlled mod incubation. It should not be treated as a fully frozen gameplay contract yet.

## Stage 1 Additions (Draft Buff Guidance)
- New additive facade helpers: `IMKDuckov.EnumerateBuffCatalogDraft()`, `IMKDuckov.TryReadBuffsDraft(object hostContext = null)`, `IMKDuckov.TryFindBuffDraft(int buffId, object hostContext = null)`, `IMKDuckov.TryAddBuffDraft(int buffId, object hostContext = null, int overrideWeaponId = 0)`, `IMKDuckov.TryRemoveBuffDraft(int buffId, bool removeOneLayer = false, object hostContext = null)`, and `IMKDuckov.TrySetBuffLayersDraft(int buffId, int layers, object hostContext = null)`.
- Additional additive draft helpers: `IMKDuckov.TryFindBuffByExclusiveTagDraft(string exclusiveTag, object hostContext = null)`, `IMKDuckov.TryFindBuffByTypeDraft(string typeFullName, object hostContext = null)`, `IMKDuckov.TryHasBuffDraft(int buffId, object hostContext = null)`, `IMKDuckov.TryAddBuffLayersDraft(int buffId, int layerDelta, bool addIfMissing = true, object hostContext = null, int overrideWeaponId = 0)`, `IMKDuckov.TryRemoveBuffLayersDraft(int buffId, int layerDelta, object hostContext = null)`, and `IMKDuckov.TryRemoveBuffsByExclusiveTagDraft(string exclusiveTag, bool removeOneLayer = false, object hostContext = null)`.
- New additive DTOs: `BuffCatalogDraft`, `BuffCatalogEntryDraft`, and `BuffSnapshotDraft`.
- Current bounded v1 semantics:
  - `hostContext == null` targets the main character.
  - Passing the main character, the main character item, or an item under the main character item tree resolves to the same buff manager.
  - Catalog enumeration reads runtime buff prefabs from `GameplayDataSettings.Buffs`.
  - Direct mutation is runtime-only and currently covers add, remove, and layer writes for active buffs.
- Draft query/mutation helpers now also cover active-buff lookup by `ExclusiveTag` or runtime type name, boolean existence checks, additive/subtractive layer helpers, and exclusive-tag batch removal.
- This surface is additive but still draft-oriented: suitable for Probe, internal tools, and controlled mod incubation. It is intentionally not part of the frozen `IReadService` / `IWriteService` or the stable capability bitset yet.
