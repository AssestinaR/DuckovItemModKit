using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
    // Minimal DTOs for unified read/write
    public sealed class CoreFields
    {
        public string Name { get; set; }
        public string RawName { get; set; }
        public int TypeId { get; set; }
        public int Quality { get; set; }
        public int DisplayQuality { get; set; }
        public int Value { get; set; }
    }

    public sealed class CoreFieldChanges
    {
        public string Name { get; set; } // null to ignore
        public string RawName { get; set; } // null to ignore
        public int? TypeId { get; set; }
        public int? Quality { get; set; }
        public int? DisplayQuality { get; set; }
        public int? Value { get; set; }
    }

    public sealed class StackInfo { public int Max { get; set; } public int Count { get; set; } public bool Stackable => Max >1; }
    public sealed class DurabilityInfo { public float Max { get; set; } public float Current { get; set; } public float Loss { get; set; } public bool NeedInspection { get; set; } }
    public sealed class InspectionInfo { public bool Inspected { get; set; } public bool Inspecting { get; set; } public bool NeedInspection { get; set; } }

    // New: extra item infos
    public sealed class OrderingInfo { public int Order { get; set; } }
    public sealed class WeightInfo { public float UnitSelfWeight { get; set; } public float SelfWeight { get; set; } public float TotalWeight { get; set; } public float BaseWeight { get; set; } }
    public sealed class RelationInfo { public object ParentItem { get; set; } public bool InInventory { get; set; } public bool InSlot { get; set; } }
    public sealed class FlagsInfo { public bool Sticky { get; set; } public bool CanBeSold { get; set; } public bool CanDrop { get; set; } public bool Repairable { get; set; } public bool IsCharacter { get; set; } }

    public sealed class StatValueEntry { public string Key { get; set; } public float Value { get; set; } }
    public sealed class StatsSnapshot { public List<StatValueEntry> Entries { get; set; } = new List<StatValueEntry>(); }
    public sealed class InventorySnapshot { public int Capacity { get; set; } public int Count { get; set; } }
    public sealed class EffectEntry { public string Name { get; set; } public bool Enabled { get; set; } }
    public sealed class ItemGraphicSnapshot { public bool HasGraphic { get; set; } public string GraphicName { get; set; } }
    public sealed class AgentUtilitiesSnapshot { public bool HasActiveAgent { get; set; } public string ActiveAgentName { get; set; } }

    // Modifier description DTO
    public sealed class ModifierDescriptionInfo
    {
        public string Key { get; set; }
        public string Type { get; set; }
        public float Value { get; set; }
        public int Order { get; set; }
        public bool Display { get; set; }
        public string Target { get; set; }
    }

    // Effect DTOs
    public sealed class EffectInfo
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public bool Display { get; set; }
        public string Description { get; set; }
        public string[] TriggerTypes { get; set; }
        public string[] ActionTypes { get; set; }
        public string[] FilterTypes { get; set; }
    }
    public sealed class EffectCreateOptions
    {
        public string Name { get; set; }
        public bool? Enabled { get; set; }
        public bool? Display { get; set; }
        public string Description { get; set; }
    }

    // Slot creation/update descriptors
    public sealed class SlotCreateOptions
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public object SlotIcon { get; set; }
        public string[] RequireTags { get; set; }
        public string[] ExcludeTags { get; set; }
        public bool? ForbidItemsWithSameID { get; set; }
    }
    public sealed class SlotUpdateOptions
    {
        public string DisplayName { get; set; }
        public object SlotIcon { get; set; }
        public string[] RequireTags { get; set; }
        public string[] ExcludeTags { get; set; }
        public bool? ForbidItemsWithSameID { get; set; }
    }

    public interface IReadService
    {
        // Core & collections
        RichResult<CoreFields> TryReadCoreFields(object item);
        RichResult<VariableEntry[]> TryReadVariables(object item);
        RichResult<VariableEntry[]> TryReadConstants(object item);
        RichResult<ModifierEntry[]> TryReadModifiers(object item);
        RichResult<string[]> TryReadTags(object item);
        RichResult<SlotEntry[]> TryReadSlots(object item);
        // Derived infos
        RichResult<StackInfo> TryReadStackInfo(object item);
        RichResult<DurabilityInfo> TryReadDurabilityInfo(object item);
        RichResult<InspectionInfo> TryReadInspectionInfo(object item);
        RichResult<OrderingInfo> TryReadOrderingInfo(object item);
        RichResult<WeightInfo> TryReadWeightInfo(object item);
        RichResult<RelationInfo> TryReadRelations(object item);
        RichResult<FlagsInfo> TryReadFlags(object item);
        RichResult<string> TryReadSoundKey(object item);
        // Advanced infos
        RichResult<StatsSnapshot> TryReadStats(object item);
        RichResult<InventorySnapshot> TryReadChildInventoryInfo(object item);
        RichResult<object[]> TryEnumerateChildInventoryItems(object item);
        RichResult<EffectEntry[]> TryReadEffects(object item);
        RichResult<ItemGraphicSnapshot> TryReadItemGraphic(object item);
        RichResult<AgentUtilitiesSnapshot> TryReadAgentUtilities(object item);
        // New: detailed Effects
        RichResult<EffectInfo[]> TryReadEffectsDetailed(object item);
        // New: modifier descriptions
        RichResult<ModifierDescriptionInfo[]> TryReadModifierDescriptions(object item);

        RichResult<ItemSnapshot> Snapshot(object item);
    }

    public interface IWriteService
    {
        // Core write
        RichResult TryWriteCoreFields(object item, CoreFieldChanges changes);
        RichResult TryWriteVariables(object item, IEnumerable<KeyValuePair<string, object>> entries, bool overwrite);
        RichResult TryWriteConstants(object item, IEnumerable<KeyValuePair<string, object>> entries, bool createIfMissing);
        RichResult TryWriteTags(object item, IEnumerable<string> tags, bool merge);
        // Convenience write
        RichResult TrySetStackCount(object item, int count);
        RichResult TrySetMaxStack(object item, int max);
        RichResult TrySetDurability(object item, float value);
        RichResult TrySetMaxDurability(object item, float value);
        RichResult TrySetDurabilityLoss(object item, float value);
        RichResult TrySetInspected(object item, bool inspected);
        RichResult TrySetInspecting(object item, bool inspecting);
        // Extra write
        RichResult TrySetOrder(object item, int order);
        RichResult TrySetSoundKey(object item, string soundKey);
        RichResult TrySetWeight(object item, float baseWeight);
        RichResult TrySetIcon(object item, object sprite);
        // Child inventory write
        RichResult TryAddToChildInventory(object item, object childItem, int? index1Based = null, bool allowMerge = true);
        RichResult TryRemoveFromChildInventory(object item, object childItem);
        RichResult TryMoveInChildInventory(object item, int fromIndex, int toIndex);
        RichResult TryClearChildInventory(object item);
        // Modifiers write
        RichResult TryAddModifier(object item, string statKey, float value, bool isPercent = false, string type = null, object source = null);
        RichResult<int> TryRemoveAllModifiersFromSource(object item, object source);
        // New: ModifierDescriptionCollection write APIs
        RichResult TryAddModifierDescription(object item, string key, string type, float value, bool? display = null, int? order = null, string target = null);
        RichResult TryRemoveModifierDescription(object item, string key);
        RichResult TrySetModifierDescriptionValue(object item, string key, float value);
        RichResult TrySetModifierDescriptionType(object item, string key, string type);
        RichResult TrySetModifierDescriptionOrder(object item, string key, int order);
        RichResult TrySetModifierDescriptionDisplay(object item, string key, bool display);
        RichResult TryClearModifierDescriptions(object item);
        RichResult TryReapplyModifiers(object item);
        // Sanitization helper
        RichResult TrySanitizeModifierDescriptions(object item);
        // Effects write (basic)
        RichResult TryAddEffect(object item, string effectTypeFullName, EffectCreateOptions options = null);
        RichResult TryAddEffect(object item, string effectTypeFullName);
        RichResult TryRemoveEffect(object item, int effectIndex);
        RichResult TryEnableEffect(object item, int effectIndex, bool enabled);
        RichResult TrySetEffectProperty(object item, int effectIndex, string propName, object value);
        RichResult TryAddEffectComponent(object item, int effectIndex, string componentTypeFullName, string kind /*Trigger|Filter|Action*/);
        RichResult TryRemoveEffectComponent(object item, int effectIndex, string kind, int componentIndex);
        RichResult TrySetEffectComponentProperty(object item, int effectIndex, string kind, int componentIndex, string propName, object value);
        // Slots write
        RichResult TryPlugIntoSlot(object ownerItem, string slotKey, object childItem);
        RichResult TryUnplugFromSlot(object ownerItem, string slotKey);
        RichResult TryMoveBetweenSlots(object ownerItem, string fromSlotKey, string toSlotKey);
        RichResult TryAddSlot(object ownerItem, SlotCreateOptions options);
        RichResult TryRemoveSlot(object ownerItem, string slotKey);
        // Stats write
        RichResult TrySetStatValue(object ownerItem, string statKey, float value);
        RichResult TryEnsureStat(object ownerItem, string statKey, float? initialValue = null);
        RichResult TryRemoveStat(object ownerItem, string statKey);
        // Variables/Constants metadata
        RichResult TrySetVariableMeta(object ownerItem, string key, bool? display = null, string displayName = null, string description = null);
        RichResult TrySetConstantMeta(object ownerItem, string key, bool? display = null, string displayName = null, string description = null);
        // Batch transaction
        string BeginTransaction(object ownerItem);
        RichResult CommitTransaction(object ownerItem, string token);
        RichResult RollbackTransaction(object ownerItem, string token);
    }
}
