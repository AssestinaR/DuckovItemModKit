using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
    // Minimal DTOs for unified read/write
    /// <summary>核心字段快照。</summary>
    public sealed class CoreFields
    {
        /// <summary>显示名称（已本地化）。</summary>
        public string Name { get; set; }
        /// <summary>原始名称（未本地化）。</summary>
        public string RawName { get; set; }
        /// <summary>类型 ID。</summary>
        public int TypeId { get; set; }
        /// <summary>品质。</summary>
        public int Quality { get; set; }
        /// <summary>显示用品质。</summary>
        public int DisplayQuality { get; set; }
        /// <summary>价值。</summary>
        public int Value { get; set; }
    }

    /// <summary>核心字段修改描述（null 表示忽略该字段）。</summary>
    public sealed class CoreFieldChanges
    {
        /// <summary>显示名称（null 不修改）。</summary>
        public string Name { get; set; }
        /// <summary>原始名称（null 不修改）。</summary>
        public string RawName { get; set; }
        /// <summary>类型 ID（null 不修改）。</summary>
        public int? TypeId { get; set; }
        /// <summary>品质（null 不修改）。</summary>
        public int? Quality { get; set; }
        /// <summary>显示品质（null 不修改）。</summary>
        public int? DisplayQuality { get; set; }
        /// <summary>价值（null 不修改）。</summary>
        public int? Value { get; set; }
    }

    /// <summary>堆叠信息。</summary>
    public sealed class StackInfo { 
        /// <summary>最大堆叠数。</summary>
        public int Max { get; set; } 
        /// <summary>当前数量。</summary>
        public int Count { get; set; } 
        /// <summary>是否可堆叠（Max &gt; 1）。</summary>
        public bool Stackable => Max > 1; }
    /// <summary>耐久信息。</summary>
    public sealed class DurabilityInfo { 
        /// <summary>最大耐久。</summary>
        public float Max { get; set; } 
        /// <summary>当前耐久。</summary>
        public float Current { get; set; } 
        /// <summary>耐久损耗。</summary>
        public float Loss { get; set; } 
        /// <summary>是否需要检查。</summary>
        public bool NeedInspection { get; set; } }
    /// <summary>检查状态信息。</summary>
    public sealed class InspectionInfo { 
        /// <summary>是否已检查。</summary>
        public bool Inspected { get; set; } 
        /// <summary>是否正在检查。</summary>
        public bool Inspecting { get; set; } 
        /// <summary>是否需要检查。</summary>
        public bool NeedInspection { get; set; } }

    /// <summary>排序信息。</summary>
    public sealed class OrderingInfo { 
        /// <summary>排序序号。</summary>
        public int Order { get; set; } }
    /// <summary>重量信息。</summary>
    public sealed class WeightInfo { 
        /// <summary>单件自重。</summary>
        public float UnitSelfWeight { get; set; } 
        /// <summary>自身重量。</summary>
        public float SelfWeight { get; set; } 
        /// <summary>总重量（含子树）。</summary>
        public float TotalWeight { get; set; } 
        /// <summary>基础重量（可手工设定）。</summary>
        public float BaseWeight { get; set; } }
    /// <summary>关系信息（父物品/所属状态）。</summary>
    public sealed class RelationInfo { 
        /// <summary>父物品实例。</summary>
        public object ParentItem { get; set; } 
        /// <summary>是否在背包中。</summary>
        public bool InInventory { get; set; } 
        /// <summary>是否在槽位中。</summary>
        public bool InSlot { get; set; } }
    /// <summary>标志信息。</summary>
    public sealed class FlagsInfo { 
        /// <summary>是否粘性（不随动）。</summary>
        public bool Sticky { get; set; } 
        /// <summary>是否可出售。</summary>
        public bool CanBeSold { get; set; } 
        /// <summary>是否可丢弃。</summary>
        public bool CanDrop { get; set; } 
        /// <summary>是否可修理。</summary>
        public bool Repairable { get; set; } 
        /// <summary>是否角色物品。</summary>
        public bool IsCharacter { get; set; } }

    /// <summary>单个统计值条目。</summary>
    public sealed class StatValueEntry { 
        /// <summary>统计键。</summary>
        public string Key { get; set; } 
        /// <summary>统计值。</summary>
        public float Value { get; set; } }
    /// <summary>统计快照。</summary>
    public sealed class StatsSnapshot { 
        /// <summary>统计条目集合。</summary>
        public List<StatValueEntry> Entries { get; set; } = new List<StatValueEntry>(); }
    /// <summary>子背包快照。</summary>
    public sealed class InventorySnapshot { 
        /// <summary>容量。</summary>
        public int Capacity { get; set; } 
        /// <summary>当前数量。</summary>
        public int Count { get; set; } }
    /// <summary>效果条目（基础）。</summary>
    public sealed class EffectEntry { 
        /// <summary>效果类型名。</summary>
        public string Name { get; set; } 
        /// <summary>是否启用。</summary>
        public bool Enabled { get; set; } }
    /// <summary>图形快照。</summary>
    public sealed class ItemGraphicSnapshot { 
        /// <summary>是否存在图形。</summary>
        public bool HasGraphic { get; set; } 
        /// <summary>图形名称。</summary>
        public string GraphicName { get; set; } }
    /// <summary>Agent 工具集快照。</summary>
    public sealed class AgentUtilitiesSnapshot { 
        /// <summary>是否有激活的 Agent。</summary>
        public bool HasActiveAgent { get; set; } 
        /// <summary>激活的 Agent 名称。</summary>
        public string ActiveAgentName { get; set; } }

    /// <summary>修饰描述信息。</summary>
    public sealed class ModifierDescriptionInfo
    {
        /// <summary>键。</summary>
        public string Key { get; set; }
        /// <summary>类型。</summary>
        public string Type { get; set; }
        /// <summary>数值。</summary>
        public float Value { get; set; }
        /// <summary>排序。</summary>
        public int Order { get; set; }
        /// <summary>显示与否。</summary>
        public bool Display { get; set; }
        /// <summary>目标标识。</summary>
        public string Target { get; set; }
    }

    /// <summary>效果详细信息。</summary>
    public sealed class EffectInfo
    {
        /// <summary>名称。</summary>
        public string Name { get; set; }
        /// <summary>是否启用。</summary>
        public bool Enabled { get; set; }
        /// <summary>是否在 UI 显示。</summary>
        public bool Display { get; set; }
        /// <summary>描述。</summary>
        public string Description { get; set; }
        /// <summary>Trigger 组件类型名列表。</summary>
        public string[] TriggerTypes { get; set; }
        /// <summary>Action 组件类型名列表。</summary>
        public string[] ActionTypes { get; set; }
        /// <summary>Filter 组件类型名列表。</summary>
        public string[] FilterTypes { get; set; }
    }
    /// <summary>效果创建选项。</summary>
    public sealed class EffectCreateOptions
    {
        /// <summary>可选：新建效果对象名称。</summary>
        public string Name { get; set; }
        /// <summary>可选：是否启用。</summary>
        public bool? Enabled { get; set; }
        /// <summary>可选：是否显示。</summary>
        public bool? Display { get; set; }
        /// <summary>可选：描述。</summary>
        public string Description { get; set; }
    }

    /// <summary>插槽创建选项。</summary>
    public sealed class SlotCreateOptions
    {
        /// <summary>槽位键。</summary>
        public string Key { get; set; }
        /// <summary>显示名。</summary>
        public string DisplayName { get; set; }
        /// <summary>图标/精灵。</summary>
        public object SlotIcon { get; set; }
        /// <summary>需要包含标签。</summary>
        public string[] RequireTags { get; set; }
        /// <summary>需要排除标签。</summary>
        public string[] ExcludeTags { get; set; }
        /// <summary>是否禁止同 ID 物品。</summary>
        public bool? ForbidItemsWithSameID { get; set; }
    }
    /// <summary>插槽更新选项。</summary>
    public sealed class SlotUpdateOptions
    {
        /// <summary>显示名。</summary>
        public string DisplayName { get; set; }
        /// <summary>图标/精灵。</summary>
        public object SlotIcon { get; set; }
        /// <summary>需要包含标签。</summary>
        public string[] RequireTags { get; set; }
        /// <summary>需要排除标签。</summary>
        public string[] ExcludeTags { get; set; }
        /// <summary>是否禁止同 ID 物品。</summary>
        public bool? ForbidItemsWithSameID { get; set; }
    }

    /// <summary>读取服务：统一读取物品核心字段、集合与扩展信息。</summary>
    public interface IReadService
    {
        /// <summary>读取核心字段快照。</summary>
        /// <param name="item">目标物品。</param>
        RichResult<CoreFields> TryReadCoreFields(object item);
        /// <summary>读取变量集合。</summary>
        RichResult<VariableEntry[]> TryReadVariables(object item);
        /// <summary>读取常量集合。</summary>
        RichResult<VariableEntry[]> TryReadConstants(object item);
        /// <summary>读取修饰集合。</summary>
        RichResult<ModifierEntry[]> TryReadModifiers(object item);
        /// <summary>读取标签集合。</summary>
        RichResult<string[]> TryReadTags(object item);
        /// <summary>读取插槽集合。</summary>
        RichResult<SlotEntry[]> TryReadSlots(object item);
        /// <summary>读取堆叠信息。</summary>
        RichResult<StackInfo> TryReadStackInfo(object item);
        /// <summary>读取耐久信息。</summary>
        RichResult<DurabilityInfo> TryReadDurabilityInfo(object item);
        /// <summary>读取检查状态信息。</summary>
        RichResult<InspectionInfo> TryReadInspectionInfo(object item);
        /// <summary>读取排序信息。</summary>
        RichResult<OrderingInfo> TryReadOrderingInfo(object item);
        /// <summary>读取重量信息。</summary>
        RichResult<WeightInfo> TryReadWeightInfo(object item);
        /// <summary>读取关系信息。</summary>
        RichResult<RelationInfo> TryReadRelations(object item);
        /// <summary>读取标志信息。</summary>
        RichResult<FlagsInfo> TryReadFlags(object item);
        /// <summary>读取音效键。</summary>
        RichResult<string> TryReadSoundKey(object item);
        /// <summary>读取统计快照。</summary>
        RichResult<StatsSnapshot> TryReadStats(object item);
        /// <summary>读取子背包信息。</summary>
        RichResult<InventorySnapshot> TryReadChildInventoryInfo(object item);
        /// <summary>枚举子背包物品。</summary>
        RichResult<object[]> TryEnumerateChildInventoryItems(object item);
        /// <summary>读取基础效果条目。</summary>
        RichResult<EffectEntry[]> TryReadEffects(object item);
        /// <summary>读取图形快照。</summary>
        RichResult<ItemGraphicSnapshot> TryReadItemGraphic(object item);
        /// <summary>读取 Agent 工具集快照。</summary>
        RichResult<AgentUtilitiesSnapshot> TryReadAgentUtilities(object item);
        /// <summary>读取详细效果信息。</summary>
        RichResult<EffectInfo[]> TryReadEffectsDetailed(object item);
        /// <summary>读取修饰描述集合。</summary>
        RichResult<ModifierDescriptionInfo[]> TryReadModifierDescriptions(object item);
        /// <summary>捕获完整快照。</summary>
        RichResult<ItemSnapshot> Snapshot(object item);
    }

    /// <summary>写入服务：统一修改核心字段、集合与扩展信息。</summary>
    public interface IWriteService
    {
        /// <summary>写入核心字段集合。</summary>
        RichResult TryWriteCoreFields(object item, CoreFieldChanges changes);
        /// <summary>写入变量集合。</summary>
        RichResult TryWriteVariables(object item, IEnumerable<KeyValuePair<string, object>> entries, bool overwrite);
        /// <summary>写入常量集合。</summary>
        RichResult TryWriteConstants(object item, IEnumerable<KeyValuePair<string, object>> entries, bool createIfMissing);
        /// <summary>写入标签集合。</summary>
        RichResult TryWriteTags(object item, IEnumerable<string> tags, bool merge);
        /// <summary>设置堆叠数量。</summary>
        RichResult TrySetStackCount(object item, int count);
        /// <summary>设置最大堆叠数。</summary>
        RichResult TrySetMaxStack(object item, int max);
        /// <summary>设置当前耐久。</summary>
        RichResult TrySetDurability(object item, float value);
        /// <summary>设置最大耐久。</summary>
        RichResult TrySetMaxDurability(object item, float value);
        /// <summary>设置耐久损耗。</summary>
        RichResult TrySetDurabilityLoss(object item, float value);
        /// <summary>设置已检查状态。</summary>
        RichResult TrySetInspected(object item, bool inspected);
        /// <summary>设置检查中状态。</summary>
        RichResult TrySetInspecting(object item, bool inspecting);
        /// <summary>设置排序序号。</summary>
        RichResult TrySetOrder(object item, int order);
        /// <summary>设置音效键。</summary>
        RichResult TrySetSoundKey(object item, string soundKey);
        /// <summary>设置基础重量。</summary>
        RichResult TrySetWeight(object item, float baseWeight);
        /// <summary>设置图标。</summary>
        RichResult TrySetIcon(object item, object sprite);
        /// <summary>添加到子背包。</summary>
        RichResult TryAddToChildInventory(object item, object childItem, int? index1Based = null, bool allowMerge = true);
        /// <summary>从子背包移除。</summary>
        RichResult TryRemoveFromChildInventory(object item, object childItem);
        /// <summary>在子背包移动位置。</summary>
        RichResult TryMoveInChildInventory(object item, int fromIndex, int toIndex);
        /// <summary>清空子背包。</summary>
        RichResult TryClearChildInventory(object item);
        /// <summary>添加修饰器。</summary>
        RichResult TryAddModifier(object item, string statKey, float value, bool isPercent = false, string type = null, object source = null);
        /// <summary>移除来源修饰器。</summary>
        RichResult<int> TryRemoveAllModifiersFromSource(object item, object source);
        /// <summary>添加修饰描述项。</summary>
        RichResult TryAddModifierDescription(object item, string key, string type, float value, bool? display = null, int? order = null, string target = null);
        /// <summary>移除修饰描述项。</summary>
        RichResult TryRemoveModifierDescription(object item, string key);
        /// <summary>设置修饰描述值。</summary>
        RichResult TrySetModifierDescriptionValue(object item, string key, float value);
        /// <summary>设置修饰描述类型。</summary>
        RichResult TrySetModifierDescriptionType(object item, string key, string type);
        /// <summary>设置修饰描述顺序。</summary>
        RichResult TrySetModifierDescriptionOrder(object item, string key, int order);
        /// <summary>设置修饰描述显示。</summary>
        RichResult TrySetModifierDescriptionDisplay(object item, string key, bool display);
        /// <summary>清空修饰描述集合。</summary>
        RichResult TryClearModifierDescriptions(object item);
        /// <summary>重新应用修饰。</summary>
        RichResult TryReapplyModifiers(object item);
        /// <summary>清理无效修饰描述。</summary>
        RichResult TrySanitizeModifierDescriptions(object item);
        /// <summary>添加效果组件。</summary>
        RichResult TryAddEffect(object item, string effectTypeFullName, EffectCreateOptions options = null);
        /// <summary>添加效果组件（简化）。</summary>
        RichResult TryAddEffect(object item, string effectTypeFullName);
        /// <summary>移除指定索引效果。</summary>
        RichResult TryRemoveEffect(object item, int effectIndex);
        /// <summary>启用/禁用效果。</summary>
        RichResult TryEnableEffect(object item, int effectIndex, bool enabled);
        /// <summary>设置效果属性。</summary>
        RichResult TrySetEffectProperty(object item, int effectIndex, string propName, object value);
        /// <summary>添加效果子组件。</summary>
        RichResult TryAddEffectComponent(object item, int effectIndex, string componentTypeFullName, string kind /*Trigger|Filter|Action*/);
        /// <summary>移除效果子组件。</summary>
        RichResult TryRemoveEffectComponent(object item, int effectIndex, string kind, int componentIndex);
        /// <summary>设置效果子组件属性。</summary>
        RichResult TrySetEffectComponentProperty(object item, int effectIndex, string kind, int componentIndex, string propName, object value);
        /// <summary>插入到槽位。</summary>
        RichResult TryPlugIntoSlot(object ownerItem, string slotKey, object childItem);
        /// <summary>从槽位拔出。</summary>
        RichResult TryUnplugFromSlot(object ownerItem, string slotKey);
        /// <summary>移动槽位物品。</summary>
        RichResult TryMoveBetweenSlots(object ownerItem, string fromSlotKey, string toSlotKey);
        /// <summary>添加槽位。</summary>
        RichResult TryAddSlot(object ownerItem, SlotCreateOptions options);
        /// <summary>移除槽位。</summary>
        RichResult TryRemoveSlot(object ownerItem, string slotKey);
        /// <summary>设置统计值。</summary>
        RichResult TrySetStatValue(object ownerItem, string statKey, float value);
        /// <summary>确保统计存在。</summary>
        RichResult TryEnsureStat(object ownerItem, string statKey, float? initialValue = null);
        /// <summary>移除统计。</summary>
        RichResult TryRemoveStat(object ownerItem, string statKey);
        /// <summary>设置变量元数据。</summary>
        RichResult TrySetVariableMeta(object ownerItem, string key, bool? display = null, string displayName = null, string description = null);
        /// <summary>设置常量元数据。</summary>
        RichResult TrySetConstantMeta(object ownerItem, string key, bool? display = null, string displayName = null, string description = null);
        /// <summary>开始批事务。</summary>
        string BeginTransaction(object ownerItem);
        /// <summary>提交批事务。</summary>
        RichResult CommitTransaction(object ownerItem, string token);
        /// <summary>回滚批事务。</summary>
        RichResult RollbackTransaction(object ownerItem, string token);
    }
}
