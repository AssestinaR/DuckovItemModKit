# IMK Stage 1 API Freeze

This document records the Stage 1 public contract surface that is considered stable.
For Stage 1, stability is defined by namespace, public type shape, method signatures, and documented semantics.
Physical file placement is not part of the freeze. The repo may continue to move files between Entry, Contracts, Workflows, Duckov, and Diagnostics as long as the public API below does not break.

## Stable Public Namespaces

- `ItemModKit.Core`
- `ItemModKit.Core.Locator`
- `ItemModKit.Adapters.Duckov.IMKDuckov`

These namespace names remain the compatibility boundary even after the physical layout migration.

## Stable Contract Rule

All public DTOs, enums, snapshots, result carriers, handles, and interfaces that are directly referenced by the stable surfaces below are part of the Stage 1 freeze unless this document explicitly marks them as draft or non-frozen.

This includes, but is not limited to:

- Core read/write DTOs such as `CoreFields`, `CoreFieldChanges`, `StackInfo`, `DurabilityInfo`, `InspectionInfo`, `OrderingInfo`, `WeightInfo`, `RelationInfo`, `FlagsInfo`
- Stats and catalog DTOs such as `StatValueEntry`, `StatsSnapshot`, `StatCatalogEntry`
- Inventory / effect / modifier / localization DTOs such as `InventorySnapshot`, `EffectEntry`, `EffectInfo`, `EffectDetails`, `ModifierDescriptionInfo`, `LocalizedTextEntry`, `LocalizedTextSnapshot`, `ItemGraphicSnapshot`, `AgentUtilitiesSnapshot`
- Slot and snapshot DTOs such as `SlotEntry`, `SlotCreateOptions`, `SlotUpdateOptions`, `ItemSnapshot`
- Restore / rebirth / clone result carriers such as `PersistenceRestoreResult`, `TreeRestoreResult`, `RebirthRestoreResult`, `ClonePipelineResult`
- Locator contracts and related enums such as `IItemHandle`, `IInventoryHandle`, `ISlotHandle`, `IOwnershipService`, `IItemQuery`, `IUISelectionV2`, `ILogicalIdMap`, `InventoryKind`

## Stable Service Interfaces

### `IReadService`

The following read surface is frozen for Stage 1:

- Core snapshots: `TryReadCoreFields`, `TryReadVariables`, `TryReadConstants`, `TryReadModifiers`, `TryReadTags`, `TryReadSlots`
- Basic item state: `TryReadStackInfo`, `TryReadDurabilityInfo`, `TryReadInspectionInfo`, `TryReadOrderingInfo`, `TryReadWeightInfo`, `TryReadRelations`, `TryReadFlags`, `TryReadSoundKey`
- Stats: `TryReadStats`, `TryReadBaseStats`, `TryReadCurrentStats`, `TryEnumerateAvailableStats`
- Child inventory: `TryReadChildInventoryInfo`, `TryEnumerateChildInventoryItems`
- Effects and presentation: `TryReadEffects`, `TryReadEffectsDetailed`, `TryReadEffectsDeep`, `TryReadModifierDescriptions`, `TryReadItemGraphic`, `TryReadAgentUtilities`
- Localization: `TryReadLocalizedText`, `TryReadAllLocalizedTexts`, `TryReadStatLocalizedText`, `TryReadAllStatLocalizedTexts`
- Full snapshot: `Snapshot`

### `IWriteService`

The following write surface is frozen for Stage 1:

- Core and collections: `TryWriteCoreFields`, `TryWriteVariables`, `TryWriteConstants`, `TryWriteTags`
- Stack / durability / inspection / ordering / sound / weight / icon: `TrySetStackCount`, `TrySetMaxStack`, `TrySetDurability`, `TrySetMaxDurability`, `TrySetDurabilityLoss`, `TrySetInspected`, `TrySetInspecting`, `TrySetOrder`, `TrySetSoundKey`, `TrySetWeight`, `TrySetIcon`
- Child inventory: `TryAddToChildInventory`, `TryRemoveFromChildInventory`, `TryMoveInChildInventory`, `TryClearChildInventory`
- Modifiers and modifier host: `TryAddModifier`, `TryRemoveAllModifiersFromSource`, `TryEnsureModifierHost`, `TryRemoveModifierHost`, `TrySetModifierHostEnabled`, `TryReapplyModifiers`
- Modifier descriptions: `TryAddModifierDescription`, `TryRemoveModifierDescription`, `TrySetModifierDescriptionValue`, `TrySetModifierDescriptionType`, `TrySetModifierDescriptionOrder`, `TrySetModifierDescriptionDisplay`, `TrySetModifierDescriptionTarget`, `TrySetModifierDescriptionEnableInInventory`, `TryClearModifierDescriptions`, `TrySanitizeModifierDescriptions`
- Effects: `TryAddEffect` (all overloads), `TryRemoveEffect`, `TryEnableEffect`, `TrySetEffectProperty`, `TryAddEffectComponent`, `TryRemoveEffectComponent`, `TrySetEffectComponentProperty`
- Effect helpers: `TryRenameEffect`, `TrySetEffectDisplay`, `TrySetEffectDescription`, `TryMoveEffect`, `TryMoveEffectComponent`, `TrySanitizeEffects`
- Slots: `TryPlugIntoSlot`, `TryUnplugFromSlot`, `TryMoveBetweenSlots`, `TryAddSlot`, `TryEnsureSlotHost`, `TryEnsureSlots`, `TryRemoveSlot`, `TryRemoveSlotHost`, `TryRemoveDynamicSlot`, `TryRemoveBuiltinSlot`, `TryRemoveSlots`, `TryRemoveSlotSystem`, `TrySetSlotTags`
- Stats: `TrySetStatValue`, `TryEnsureStat`, `TryRemoveStat`, `TryMoveStat`, `TryEnsureStatsHost`, `TryRemoveStatsHost`
- Metadata: `TrySetVariableMeta`, `TrySetConstantMeta`
- Transactions: `BeginTransaction`, `CommitTransaction`, `RollbackTransaction`

## Stable Locator Surface

The handle / locator model is now part of the Stage 1 stable contract.

Frozen public contracts:

- `IItemHandle`
- `IItemLocator`
- `IItemIndex`
- `IInventoryClassifier`
- `IItemScope`
- `IInventoryHandle`
- `ISlotHandle`
- `IOwnershipService`
- `IItemQuery`
- `IUISelectionV2`
- `ILogicalIdMap`

## Stable Facade Members

The following `IMKDuckov` members are stable Stage 1 entry points.

### Stable service properties

- `Item`, `Inventory`, `Slot`
- `Query`, `Persistence`, `Rebirth`, `UISelection`
- `ItemEvents`, `WorldDrops`
- `Read`, `Write`, `Factory`, `Mover`, `Clone`, `VariableMerge`, `UIRefresh`
- `InventoryResolver`, `InventoryPlacement`, `PersistenceScheduler`
- `Ownership`, `LogicalIds`, `UISelectionV2`, `QueryV2`

### Stable version / runtime helpers

- `Version`, `Capabilities`, `Require`
- `GetOwnerId`, `IsOwnedBy`, `UseLogger`
- `TryLock`, `Unlock`
- `EnsureMigrated`

### Stable restore / rebirth helpers

- `RestoreFromMeta(...)`
- `RestoreFromMetaDetailed(...)`
- `RestoreFromTreeExport(...)`
- `RestoreFromTreeExportDetailed(...)`
- `ReplaceRebirthDetailed(...)`
- `ReplaceSafeRebirthDetailed(...)`
- `ReplaceCleanRebirthDetailed(...)`
- `ReplaceRebirthReport(...)`
- `ReplaceSafeRebirthReport(...)`
- `ReplaceCleanRebirthReport(...)`
- `GetRecentRebirthReports(...)`
- `ClearRecentRebirthReports()`
- `LogRecentRebirthReports(...)`

### Stable event / persistence orchestration helpers

- `BeginExternalEvents()`, `EndExternalEvents()`
- `PublishItemAdded(...)`, `PublishItemRemoved(...)`, `PublishItemChanged(...)`, `PublishItemMoved(...)`, `PublishItemMerged(...)`, `PublishItemSplit(...)`
- `MarkDirty(...)`, `FlushDirty(...)`, `FlushAllDirty(...)`

### Stable locator / query helpers on the facade

- `QueryDiagnostics()`
- `ReindexAll(Func<IEnumerable<object>> enumerator)`
- `ReindexAll()`
- `TryGetCurrentSelectedHandle()`
- `RefreshHandleMetadata(...)`
- `TryGetHandle(...)`
- `TryGetHandleByInstanceId(...)`
- `QueryPerfStats()`
- `ForceWorldDropRescan()`

## Compatibility Guidance For Legacy Facades

- `IMKDuckov.Query` remains frozen as a Stage 1 compatibility facade.
- `IMKDuckov.UISelection` remains frozen as a Stage 1 compatibility facade.
- New integrations should prefer `IMKDuckov.QueryV2`, `IMKDuckov.UISelectionV2`, and `IMKDuckov.TryGetCurrentSelectedHandle()`.
- Obsolete warnings on legacy members are guidance only; they do not revoke Stage 1 compatibility.

## Stable Semantics

- `RichResult.Ok == true` means the requested operation succeeded according to the documented behavior and did not silently degrade into partial success.
- `TryWriteCoreFields` must preserve rollback semantics when a multi-field write fails midway.
- Transaction semantics remain frozen: `BeginTransaction` creates a tokenized write scope, `CommitTransaction` finalizes it, and `RollbackTransaction` reverts writes performed inside that scope for the target item.
- `MarkDirty` represents a persistence request, not necessarily an immediate flush.
- `FlushDirty(item, force)` and `FlushAllDirty(reason)` remain the stable explicit flush controls.
- Restore and rebirth detailed/report helpers must continue to expose structured diagnostics through their result carriers rather than forcing callers to scrape logs.
- `ReplaceRebirthDetailed(...)` remains success-oriented under `RichResult<T>` semantics.
- `ReplaceRebirthReport(...)` and its explicit-intent variants remain failure-safe report carriers.

## Draft Surface Not Covered By The Freeze

The following surface is intentionally additive and draft-oriented. It may evolve during Stage 1 and should not be treated as a hard compatibility contract yet.

### Draft provisioning / schema helpers

- `EnsureSlotsDraft(...)`
- `EnsureResourceProvisionDraft(...)`
- `EnumerateEffectSchemaDraft(...)`

Associated draft DTOs / enums remain draft as well, including:

- `EnsureSlotsRequest`, `EnsureSlotsResult`
- `ResourceProvisionDefinition`, `EnsureResourceProvisionRequest`, `EnsureResourceProvisionResult`, `EnsureResourceProvisionDiagnostics`
- `ResourceProvisioningMode`, `ResourceProvisioningPhase`
- `EffectSchemaCatalogDraft` and related schema draft entries

### Draft buff helpers

- `EnumerateBuffCatalogDraft()`
- `TryReadBuffsDraft(...)`
- `TryFindBuffDraft(...)`
- `TryFindBuffByExclusiveTagDraft(...)`
- `TryFindBuffByTypeDraft(...)`
- `TryHasBuffDraft(...)`
- `TryAddBuffDraft(...)`
- `TryRemoveBuffDraft(...)`
- `TrySetBuffLayersDraft(...)`
- `TryAddBuffLayersDraft(...)`
- `TryRemoveBuffLayersDraft(...)`
- `TryRemoveBuffsByExclusiveTagDraft(...)`

Associated draft DTOs remain draft as well:

- `BuffCatalogDraft`
- `BuffCatalogEntryDraft`
- `BuffSnapshotDraft`

Draft meaning in Stage 1:

- usable for Probe, internal tooling, and controlled downstream incubation
- additive growth is allowed
- signatures and bounded semantics may still be refined before they graduate into the frozen read/write or capability surface

## Backward Compatibility Rules

1. Do not remove, rename, or reorder parameters for any stable member listed in this document.
2. Do not repurpose an existing DTO field, enum value, capability bit, or error code to mean something else.
3. Additive expansion is allowed when defaults preserve old behavior and existing callers do not need source changes.
4. Physical refactors are allowed only when namespace and public behavior remain compatible.
5. New experimental helpers must stay outside the stable list until they are explicitly promoted here.

## Non-Frozen Internal Diagnostics

The following remain intentionally non-frozen internal tooling and may change without API notice:

- internal perf counters and perf snapshots
- internal flush metrics plumbing
- internal migration / patching / infrastructure helpers that are not exposed as public Stage 1 contracts

End of Stage 1 API Freeze.
