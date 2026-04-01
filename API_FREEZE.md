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
 - SlotEntry (extended fields: SlotIcon, RequireTagKeys, ExcludeTagKeys, ForbidSameID, ContentTypeId, ContentName)

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
  - Slots: TryPlugIntoSlot / TryUnplugFromSlot / TryMoveBetweenSlots / TryAddSlot / TryRemoveSlot
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
1. SlotEntry struct gained optional metadata fields (default-safe): SlotIcon, RequireTagKeys, ExcludeTagKeys, ForbidSameID, ContentTypeId, ContentName.
2. Internal slot write logic now uses game signature Plug(Item,out Item) with CanPlug pre-check; error messages standardized using slot.* prefixes.
3. Experimental helper methods (not frozen): TrySetSlotIcon / TrySetSlotTags. These may change or be removed; treat as optional.
4. SlotCreateOptions.DisplayName currently ignored (game derives DisplayName from first requireTag). Field retained for future compatibility.
5. RichResult semantics unchanged: Ok only on full success.

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
