using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
    // DTOs ---------------------------------------------------
    /// <summary>
    /// 物品核心字段的读取结果。
    /// 这里表示“当前快照值”，不是可选修改补丁。
    /// </summary>
    [Serializable]
    public sealed class CoreFields
    {
        /// <summary>当前显示名称。</summary>
        public string Name { get; set; }

        /// <summary>原始显示名称，通常用于未本地化或底层显示名。</summary>
        public string RawName { get; set; }

        /// <summary>底层类型 ID。</summary>
        public int TypeId { get; set; }

        /// <summary>品质值。</summary>
        public int Quality { get; set; }

        /// <summary>用于 UI 呈现的显示品质。</summary>
        public int DisplayQuality { get; set; }

        /// <summary>物品价值。</summary>
        public int Value { get; set; }
    }

    /// <summary>
    /// 核心字段写入补丁。
    /// 可空字段表示“不修改该字段”，而不是清空为默认值。
    /// </summary>
    [Serializable]
    public sealed class CoreFieldChanges
    {
        /// <summary>新的显示名称；为 null 时保持原值。</summary>
        public string Name { get; set; }

        /// <summary>新的原始显示名称；为 null 时保持原值。</summary>
        public string RawName { get; set; }

        /// <summary>新的类型 ID；为 null 时保持原值。</summary>
        public int? TypeId { get; set; }

        /// <summary>新的品质；为 null 时保持原值。</summary>
        public int? Quality { get; set; }

        /// <summary>新的显示品质；为 null 时保持原值。</summary>
        public int? DisplayQuality { get; set; }

        /// <summary>新的价值；为 null 时保持原值。</summary>
        public int? Value { get; set; }
    }

    /// <summary>堆叠相关信息。</summary>
    [Serializable]
    public sealed class StackInfo
    {
        /// <summary>最大堆叠数；大于 1 说明该物品可堆叠。</summary>
        public int Max { get; set; }

        /// <summary>当前堆叠数量。</summary>
        public int Count { get; set; }

        /// <summary>是否可堆叠。</summary>
        public bool Stackable => Max > 1;
    }

    /// <summary>耐久度相关信息。</summary>
    [Serializable]
    public sealed class DurabilityInfo
    {
        /// <summary>最大耐久。</summary>
        public float Max { get; set; }

        /// <summary>当前耐久。</summary>
        public float Current { get; set; }

        /// <summary>累计耐久损失值。</summary>
        public float Loss { get; set; }

        /// <summary>当前是否仍要求鉴定后才能确认耐久信息。</summary>
        public bool NeedInspection { get; set; }
    }

    /// <summary>鉴定状态信息。</summary>
    [Serializable]
    public sealed class InspectionInfo
    {
        /// <summary>是否已经完成鉴定。</summary>
        public bool Inspected { get; set; }

        /// <summary>是否正处于鉴定过程。</summary>
        public bool Inspecting { get; set; }

        /// <summary>是否要求鉴定。</summary>
        public bool NeedInspection { get; set; }
    }

    /// <summary>排序相关信息。</summary>
    [Serializable]
    public sealed class OrderingInfo
    {
        /// <summary>当前顺序值。</summary>
        public int Order { get; set; }
    }

    /// <summary>重量相关信息。</summary>
    [Serializable]
    public sealed class WeightInfo
    {
        /// <summary>单件自重。</summary>
        public float UnitSelfWeight { get; set; }

        /// <summary>当前实例自重。</summary>
        public float SelfWeight { get; set; }

        /// <summary>包含子内容物后的总重量。</summary>
        public float TotalWeight { get; set; }

        /// <summary>基础重量配置值。</summary>
        public float BaseWeight { get; set; }
    }

    /// <summary>宿主关系信息。</summary>
    [Serializable]
    public sealed class RelationInfo
    {
        /// <summary>父级物品；没有父物品时可为 null。</summary>
        public object ParentItem { get; set; }

        /// <summary>是否位于某个背包中。</summary>
        public bool InInventory { get; set; }

        /// <summary>是否位于某个槽位中。</summary>
        public bool InSlot { get; set; }
    }

    /// <summary>常见布尔标记快照。</summary>
    [Serializable]
    public sealed class FlagsInfo
    {
        /// <summary>是否为粘附物品，通常不应被随意移除。</summary>
        public bool Sticky { get; set; }

        /// <summary>是否允许售卖。</summary>
        public bool CanBeSold { get; set; }

        /// <summary>是否允许丢弃。</summary>
        public bool CanDrop { get; set; }

        /// <summary>是否可修复。</summary>
        public bool Repairable { get; set; }

        /// <summary>该对象是否为角色本体物品。</summary>
        public bool IsCharacter { get; set; }
    }

    /// <summary>单条统计项。</summary>
    [Serializable]
    public sealed class StatValueEntry
    {
        /// <summary>统计键。</summary>
        public string Key { get; set; }

        /// <summary>当前在 Stats 集合中的顺序索引。</summary>
        public int Index { get; set; }

        /// <summary>基础值；对应原版 BaseValue。</summary>
        public float BaseValue { get; set; }

        /// <summary>生效值；对应原版 Value。</summary>
        public float EffectiveValue { get; set; }

        /// <summary>原版本地化显示键，通常形如 Stat_xxx。</summary>
        public string DisplayNameKey { get; set; }

        /// <summary>当前语言下的本地化名称。</summary>
        public string LocalizedNameCurrent { get; set; }

        /// <summary>
        /// 兼容字段：历史上 StatsSnapshot 只暴露单值，当前保持为生效值。
        /// 新代码应优先使用 BaseValue 或 EffectiveValue。
        /// </summary>
        public float Value
        {
            get => EffectiveValue;
            set => EffectiveValue = value;
        }
    }

    /// <summary>统计快照。</summary>
    [Serializable]
    public sealed class StatsSnapshot
    {
        /// <summary>所有统计项。</summary>
        public List<StatValueEntry> Entries { get; set; } = new List<StatValueEntry>();
    }

    /// <summary>可用 stat 字段目录项。</summary>
    [Serializable]
    public sealed class StatCatalogEntry
    {
        /// <summary>统计键。</summary>
        public string Key { get; set; }

        /// <summary>原版本地化显示键，通常形如 Stat_xxx。</summary>
        public string DisplayNameKey { get; set; }

        /// <summary>当前语言下的本地化名称。</summary>
        public string LocalizedNameCurrent { get; set; }
    }

    /// <summary>子背包摘要。</summary>
    [Serializable]
    public sealed class InventorySnapshot
    {
        /// <summary>子背包容量。</summary>
        public int Capacity { get; set; }

        /// <summary>当前子背包内物品数量。</summary>
        public int Count { get; set; }
    }

    /// <summary>效果的浅层摘要。</summary>
    [Serializable]
    public sealed class EffectEntry
    {
        /// <summary>效果名称。</summary>
        public string Name { get; set; }

        /// <summary>是否启用。</summary>
        public bool Enabled { get; set; }
    }

    /// <summary>物品图形资源摘要。</summary>
    [Serializable]
    public sealed class ItemGraphicSnapshot
    {
        /// <summary>是否存在图形资源。</summary>
        public bool HasGraphic { get; set; }

        /// <summary>图形资源名称；无资源时可为 null。</summary>
        public string GraphicName { get; set; }
    }

    /// <summary>Agent Utilities 相关状态。</summary>
    [Serializable]
    public sealed class AgentUtilitiesSnapshot
    {
        /// <summary>是否存在活动中的 agent。</summary>
        public bool HasActiveAgent { get; set; }

        /// <summary>当前活动 agent 名称；无活动 agent 时可为 null。</summary>
        public string ActiveAgentName { get; set; }
    }

    /// <summary>修饰器描述信息，用于 UI 或序列化后的显示层。</summary>
    [Serializable]
    public sealed class ModifierDescriptionInfo
    {
        /// <summary>描述项键，通常也是更新/删除该描述时的主键。</summary>
        public string Key { get; set; }

        /// <summary>描述类型，例如 Add、Multiply 等。</summary>
        public string Type { get; set; }

        /// <summary>描述值。</summary>
        public float Value { get; set; }

        /// <summary>显示排序值。</summary>
        public int Order { get; set; }

        /// <summary>是否参与显示。</summary>
        public bool Display { get; set; }

        /// <summary>作用目标文本；无特定目标时可为 null。</summary>
        public string Target { get; set; }

        /// <summary>是否允许该描述在背包中生效。</summary>
        public bool EnableInInventory { get; set; }
    }

    /// <summary>单语言本地化文本条目。</summary>
    [Serializable]
    public sealed class LocalizedTextEntry
    {
        /// <summary>语言名。</summary>
        public string Language { get; set; }

        /// <summary>该语言下解析得到的文本；缺失时可为 null。</summary>
        public string Text { get; set; }
    }

    /// <summary>本地化文本快照。</summary>
    [Serializable]
    public sealed class LocalizedTextSnapshot
    {
        /// <summary>本地化键。</summary>
        public string Key { get; set; }

        /// <summary>当前语言名。</summary>
        public string CurrentLanguage { get; set; }

        /// <summary>当前语言下解析得到的文本。</summary>
        public string CurrentText { get; set; }

        /// <summary>可选的全语言文本集合。</summary>
        public List<LocalizedTextEntry> Entries { get; set; } = new List<LocalizedTextEntry>();
    }

    /// <summary>效果的中层摘要。</summary>
    [Serializable]
    public sealed class EffectInfo
    {
        /// <summary>效果名称。</summary>
        public string Name { get; set; }

        /// <summary>是否启用。</summary>
        public bool Enabled { get; set; }

        /// <summary>是否参与显示。</summary>
        public bool Display { get; set; }

        /// <summary>效果说明文本。</summary>
        public string Description { get; set; }

        /// <summary>触发器组件类型名集合。</summary>
        public string[] TriggerTypes { get; set; }

        /// <summary>动作组件类型名集合。</summary>
        public string[] ActionTypes { get; set; }

        /// <summary>过滤器组件类型名集合。</summary>
        public string[] FilterTypes { get; set; }
    }

    /// <summary>创建效果时可选的初始化参数。</summary>
    [Serializable]
    public sealed class EffectCreateOptions
    {
        /// <summary>效果名称；为 null 时由实现决定默认值。</summary>
        public string Name { get; set; }

        /// <summary>是否启用；为 null 时保持实现默认值。</summary>
        public bool? Enabled { get; set; }

        /// <summary>是否显示；为 null 时保持实现默认值。</summary>
        public bool? Display { get; set; }

        /// <summary>效果说明；为 null 时保持实现默认值。</summary>
        public string Description { get; set; }
    }

    // Deep effect models
    /// <summary>单个效果组件的深层信息。</summary>
    [Serializable]
    public sealed class EffectComponentDetails
    {
        /// <summary>组件种类，只能是 Trigger、Filter、Action 之一。</summary>
        public string Kind { get; set; }

        /// <summary>组件具体类型名。</summary>
        public string Type { get; set; }

        /// <summary>组件属性集合。</summary>
        public Dictionary<string, object> Properties { get; set; }
    }

    /// <summary>效果的深层结构化信息。</summary>
    [Serializable]
    public sealed class EffectDetails
    {
        /// <summary>效果名称。</summary>
        public string Name { get; set; }

        /// <summary>是否启用。</summary>
        public bool Enabled { get; set; }

        /// <summary>是否参与显示。</summary>
        public bool Display { get; set; }

        /// <summary>效果说明文本。</summary>
        public string Description { get; set; }

        /// <summary>触发器组件集合。</summary>
        public EffectComponentDetails[] Triggers { get; set; }

        /// <summary>过滤器组件集合。</summary>
        public EffectComponentDetails[] Filters { get; set; }

        /// <summary>动作组件集合。</summary>
        public EffectComponentDetails[] Actions { get; set; }
    }

    // Slots
    /// <summary>新增槽位时使用的参数。</summary>
    [Serializable]
    public sealed class SlotCreateOptions
    {
        /// <summary>槽位键；通常要求在宿主物品内唯一。</summary>
        public string Key { get; set; }

        /// <summary>槽位显示名。</summary>
        public string DisplayName { get; set; }

        /// <summary>槽位图标对象。</summary>
        public object SlotIcon { get; set; }

        /// <summary>必需标签集合；为空时不做必需标签过滤。</summary>
        public string[] RequireTags { get; set; }

        /// <summary>排除标签集合；命中任一标签时拒绝插入。</summary>
        public string[] ExcludeTags { get; set; }

        /// <summary>是否禁止插入相同 TypeID 的物品；为 null 时保持实现默认值。</summary>
        public bool? ForbidItemsWithSameID { get; set; }
    }

    /// <summary>更新已有槽位时使用的补丁参数。</summary>
    [Serializable]
    public sealed class SlotUpdateOptions
    {
        /// <summary>新的显示名；为 null 时保持原值。</summary>
        public string DisplayName { get; set; }

        /// <summary>新的图标对象；为 null 时保持原值。</summary>
        public object SlotIcon { get; set; }

        /// <summary>新的必需标签集合；为 null 时保持原值。</summary>
        public string[] RequireTags { get; set; }

        /// <summary>新的排除标签集合；为 null 时保持原值。</summary>
        public string[] ExcludeTags { get; set; }

        /// <summary>是否禁止相同 TypeID；为 null 时保持原值。</summary>
        public bool? ForbidItemsWithSameID { get; set; }
    }

    // Interfaces ---------------------------------------------------
    /// <summary>
    /// 聚合读取服务。
    /// 所有读取入口都返回 RichResult，失败时调用方应优先查看错误码和错误消息，而不是假定默认值有效。
    /// </summary>
    public interface IReadService
    {
        /// <summary>读取核心字段快照。</summary>
        RichResult<CoreFields> TryReadCoreFields(object item);

        /// <summary>读取变量集合。</summary>
        RichResult<VariableEntry[]> TryReadVariables(object item);

        /// <summary>读取常量集合。</summary>
        RichResult<VariableEntry[]> TryReadConstants(object item);

        /// <summary>读取修饰器集合。</summary>
        RichResult<ModifierEntry[]> TryReadModifiers(object item);

        /// <summary>读取标签集合。</summary>
        RichResult<string[]> TryReadTags(object item);

        /// <summary>读取插槽集合。</summary>
        RichResult<SlotEntry[]> TryReadSlots(object item);

        /// <summary>读取堆叠信息。</summary>
        RichResult<StackInfo> TryReadStackInfo(object item);

        /// <summary>读取耐久信息。</summary>
        RichResult<DurabilityInfo> TryReadDurabilityInfo(object item);

        /// <summary>读取鉴定状态。</summary>
        RichResult<InspectionInfo> TryReadInspectionInfo(object item);

        /// <summary>读取排序信息。</summary>
        RichResult<OrderingInfo> TryReadOrderingInfo(object item);

        /// <summary>读取重量信息。</summary>
        RichResult<WeightInfo> TryReadWeightInfo(object item);

        /// <summary>读取宿主关系信息。</summary>
        RichResult<RelationInfo> TryReadRelations(object item);

        /// <summary>读取常见标记。</summary>
        RichResult<FlagsInfo> TryReadFlags(object item);

        /// <summary>读取音效键。</summary>
        RichResult<string> TryReadSoundKey(object item);

        /// <summary>读取统计快照。</summary>
        RichResult<StatsSnapshot> TryReadStats(object item);

        /// <summary>读取统计基础值视图。</summary>
        RichResult<StatsSnapshot> TryReadBaseStats(object item);

        /// <summary>读取统计生效值视图。</summary>
        RichResult<StatsSnapshot> TryReadCurrentStats(object item);

        /// <summary>枚举原版当前可用的 stat 字段目录。</summary>
        RichResult<StatCatalogEntry[]> TryEnumerateAvailableStats();

        /// <summary>读取子背包摘要。</summary>
        RichResult<InventorySnapshot> TryReadChildInventoryInfo(object item);

        /// <summary>枚举子背包内容物。</summary>
        RichResult<object[]> TryEnumerateChildInventoryItems(object item);

        /// <summary>读取浅层效果摘要。</summary>
        RichResult<EffectEntry[]> TryReadEffects(object item);

        /// <summary>读取物品图形摘要。</summary>
        RichResult<ItemGraphicSnapshot> TryReadItemGraphic(object item);

        /// <summary>读取 Agent Utilities 状态。</summary>
        RichResult<AgentUtilitiesSnapshot> TryReadAgentUtilities(object item);

        /// <summary>读取效果的中层摘要。</summary>
        RichResult<EffectInfo[]> TryReadEffectsDetailed(object item);

        /// <summary>读取修饰器描述集合。</summary>
        RichResult<ModifierDescriptionInfo[]> TryReadModifierDescriptions(object item);

        /// <summary>按本地化键读取当前语言文本。</summary>
        RichResult<LocalizedTextSnapshot> TryReadLocalizedText(string localizationKey);

        /// <summary>按本地化键读取全部可用语言文本。</summary>
        RichResult<LocalizedTextSnapshot> TryReadAllLocalizedTexts(string localizationKey);

        /// <summary>按 stat key 读取当前语言显示文本。</summary>
        RichResult<LocalizedTextSnapshot> TryReadStatLocalizedText(string statKey);

        /// <summary>按 stat key 读取全部可用语言显示文本。</summary>
        RichResult<LocalizedTextSnapshot> TryReadAllStatLocalizedTexts(string statKey);

        /// <summary>抓取完整物品快照。</summary>
        RichResult<ItemSnapshot> Snapshot(object item);

        /// <summary>读取效果的深层组件结构。</summary>
        RichResult<EffectDetails[]> TryReadEffectsDeep(object item);
    }

    /// <summary>
    /// 聚合写入服务。
    /// 大部分方法都以 Try 前缀暴露失败路径，调用方不应假定写入一定成功。
    /// 当需要多步一致性时，应显式使用事务接口包裹一组修改。
    /// </summary>
    public interface IWriteService
    {
        /// <summary>写入核心字段补丁；changes 中的 null 字段表示保持原值。</summary>
        RichResult TryWriteCoreFields(object item, CoreFieldChanges changes);

        /// <summary>写入变量集合；overwrite=true 时允许覆盖同名变量。</summary>
        RichResult TryWriteVariables(object item, IEnumerable<KeyValuePair<string, object>> entries, bool overwrite);

        /// <summary>写入常量集合；createIfMissing=true 时允许补建缺失常量。</summary>
        RichResult TryWriteConstants(object item, IEnumerable<KeyValuePair<string, object>> entries, bool createIfMissing);

        /// <summary>写入标签；merge=true 时按合并模式处理，否则按替换模式处理。</summary>
        RichResult TryWriteTags(object item, IEnumerable<string> tags, bool merge);

        /// <summary>设置当前堆叠数量。</summary>
        RichResult TrySetStackCount(object item, int count);

        /// <summary>设置最大堆叠数量。</summary>
        RichResult TrySetMaxStack(object item, int max);

        /// <summary>设置当前耐久。</summary>
        RichResult TrySetDurability(object item, float value);

        /// <summary>设置最大耐久。</summary>
        RichResult TrySetMaxDurability(object item, float value);

        /// <summary>设置累计耐久损失值。</summary>
        RichResult TrySetDurabilityLoss(object item, float value);

        /// <summary>设置是否已鉴定。</summary>
        RichResult TrySetInspected(object item, bool inspected);

        /// <summary>设置是否处于鉴定中。</summary>
        RichResult TrySetInspecting(object item, bool inspecting);

        /// <summary>设置排序值。</summary>
        RichResult TrySetOrder(object item, int order);

        /// <summary>设置音效键。</summary>
        RichResult TrySetSoundKey(object item, string soundKey);

        /// <summary>设置基础重量。</summary>
        RichResult TrySetWeight(object item, float baseWeight);

        /// <summary>设置图标对象。</summary>
        RichResult TrySetIcon(object item, object sprite);

        /// <summary>向子背包添加物品；index1Based 为 null 时由实现自行放置。</summary>
        RichResult TryAddToChildInventory(object item, object childItem, int? index1Based = null, bool allowMerge = true);

        /// <summary>从子背包中移除指定物品。</summary>
        RichResult TryRemoveFromChildInventory(object item, object childItem);

        /// <summary>在子背包中移动物品；索引语义由实现定义，调用前应与具体适配器约定。</summary>
        RichResult TryMoveInChildInventory(object item, int fromIndex, int toIndex);

        /// <summary>清空子背包。</summary>
        RichResult TryClearChildInventory(object item);

        /// <summary>新增修饰器条目。</summary>
        RichResult TryAddModifier(object item, string statKey, float value, bool isPercent = false, string type = null, object source = null);

        /// <summary>移除指定来源的所有修饰器，并返回移除数量。</summary>
        RichResult<int> TryRemoveAllModifiersFromSource(object item, object source);

        /// <summary>确保目标物品具备可写 Modifier 宿主。</summary>
        RichResult TryEnsureModifierHost(object item);

        /// <summary>移除整个 Modifier 宿主；会清空现有描述并解除宿主引用。</summary>
        RichResult TryRemoveModifierHost(object item);

        /// <summary>启用或禁用 Modifier 宿主。</summary>
        RichResult TrySetModifierHostEnabled(object item, bool enabled);

        /// <summary>新增修饰器描述项。</summary>
        RichResult TryAddModifierDescription(object item, string key, string type, float value, bool? display = null, int? order = null, string target = null);

        /// <summary>按 key 移除修饰器描述项。</summary>
        RichResult TryRemoveModifierDescription(object item, string key);

        /// <summary>更新修饰器描述值。</summary>
        RichResult TrySetModifierDescriptionValue(object item, string key, float value);

        /// <summary>更新修饰器描述类型。</summary>
        RichResult TrySetModifierDescriptionType(object item, string key, string type);

        /// <summary>更新修饰器描述排序值。</summary>
        RichResult TrySetModifierDescriptionOrder(object item, string key, int order);

        /// <summary>更新修饰器描述的显示标记。</summary>
        RichResult TrySetModifierDescriptionDisplay(object item, string key, bool display);

        /// <summary>更新修饰器描述的目标语义。</summary>
        RichResult TrySetModifierDescriptionTarget(object item, string key, string target);

        /// <summary>更新修饰器描述是否允许在背包中生效。</summary>
        RichResult TrySetModifierDescriptionEnableInInventory(object item, string key, bool enabled);

        /// <summary>清空所有修饰器描述项。</summary>
        RichResult TryClearModifierDescriptions(object item);

        /// <summary>重算修饰器派生状态。</summary>
        RichResult TryReapplyModifiers(object item);

        /// <summary>清理或修正异常的修饰器描述项。</summary>
        RichResult TrySanitizeModifierDescriptions(object item);

        /// <summary>按类型名新增效果，可附带初始化选项。</summary>
        RichResult TryAddEffect(object item, string effectTypeFullName, EffectCreateOptions options = null);

        /// <summary>按类型名新增效果，使用默认初始化选项。</summary>
        RichResult TryAddEffect(object item, string effectTypeFullName);

        /// <summary>按索引移除效果。</summary>
        RichResult TryRemoveEffect(object item, int effectIndex);

        /// <summary>启用或禁用某个效果。</summary>
        RichResult TryEnableEffect(object item, int effectIndex, bool enabled);

        /// <summary>设置效果属性值。</summary>
        RichResult TrySetEffectProperty(object item, int effectIndex, string propName, object value);

        /// <summary>给效果新增组件，kind 只能是 Trigger、Filter、Action 之一。</summary>
        RichResult TryAddEffectComponent(object item, int effectIndex, string componentTypeFullName, string kind /*Trigger|Filter|Action*/);

        /// <summary>从效果中移除指定组件。</summary>
        RichResult TryRemoveEffectComponent(object item, int effectIndex, string kind, int componentIndex);

        /// <summary>更新效果组件属性值。</summary>
        RichResult TrySetEffectComponentProperty(object item, int effectIndex, string kind, int componentIndex, string propName, object value);

        // Helpers for Effects
        /// <summary>重命名效果。</summary>
        RichResult TryRenameEffect(object item, int effectIndex, string newName);

        /// <summary>设置效果是否显示。</summary>
        RichResult TrySetEffectDisplay(object item, int effectIndex, bool display);

        /// <summary>设置效果说明文本。</summary>
        RichResult TrySetEffectDescription(object item, int effectIndex, string description);

        /// <summary>移动效果顺序。</summary>
        RichResult TryMoveEffect(object item, int fromIndex, int toIndex);

        /// <summary>移动效果内组件顺序。</summary>
        RichResult TryMoveEffectComponent(object item, int effectIndex, string kind, int fromIndex, int toIndex);

        /// <summary>清理或修复异常效果结构。</summary>
        RichResult TrySanitizeEffects(object item);

        // Slots
        /// <summary>将子物品插入到指定槽位。</summary>
        RichResult TryPlugIntoSlot(object ownerItem, string slotKey, object childItem);

        /// <summary>从指定槽位拔出内容物。</summary>
        RichResult TryUnplugFromSlot(object ownerItem, string slotKey);

        /// <summary>在两个槽位之间移动内容物。</summary>
        RichResult TryMoveBetweenSlots(object ownerItem, string fromSlotKey, string toSlotKey);

        /// <summary>新增槽位。</summary>
        RichResult TryAddSlot(object ownerItem, SlotCreateOptions options);

        /// <summary>确保目标物品具备可写槽位宿主。</summary>
        RichResult TryEnsureSlotHost(object ownerItem);

        /// <summary>确保目标物品具备给定槽位集合；缺失宿主时先初始化，再补齐缺失槽位。</summary>
        RichResult TryEnsureSlots(object ownerItem, SlotCreateOptions[] desiredSlots, bool reuseExistingIfPresent = true);

        /// <summary>移除目标物品的槽位宿主；要求所有槽位为空。</summary>
        RichResult TryRemoveSlotHost(object ownerItem);

        /// <summary>移除 IMK 动态新增槽位，并同步删除对应持久化定义。</summary>
        RichResult TryRemoveDynamicSlot(object ownerItem, string slotKey);

        /// <summary>移除原版槽位，并将该键写入持久化 tombstone。</summary>
        RichResult TryRemoveBuiltinSlot(object ownerItem, string slotKey);

        /// <summary>按给定槽位键集合批量移除槽位；可选在单项失败后继续后续移除。</summary>
        RichResult TryRemoveSlots(object ownerItem, string[] slotKeys, bool continueOnError = false);

        /// <summary>移除整个槽位系统：先移除全部槽位，再移除槽位宿主与相关持久化键。</summary>
        RichResult TryRemoveSlotSystem(object ownerItem);

        /// <summary>移除槽位。</summary>
        RichResult TryRemoveSlot(object ownerItem, string slotKey);

        /// <summary>更新空槽位的标签限制。</summary>
        RichResult TrySetSlotTags(object ownerItem, string slotKey, string[] requireTagKeys, string[] excludeTagKeys);

        // Stats
        /// <summary>设置统计基础值；对应原版 BaseValue。</summary>
        RichResult TrySetStatValue(object ownerItem, string statKey, float value);

        /// <summary>确保统计存在；不存在时可按 initialValue 初始化。</summary>
        RichResult TryEnsureStat(object ownerItem, string statKey, float? initialValue = null);

        /// <summary>移除统计项。</summary>
        RichResult TryRemoveStat(object ownerItem, string statKey);

        /// <summary>移动统计项顺序；fromIndex 与 toIndex 都按当前 Stats 集合顺序解释。</summary>
        RichResult TryMoveStat(object ownerItem, int fromIndex, int toIndex);

        /// <summary>确保目标物品具备可写 Stats 宿主。</summary>
        RichResult TryEnsureStatsHost(object ownerItem);

        /// <summary>移除整个 Stats 宿主；会清空现有 stats 并解除宿主引用。</summary>
        RichResult TryRemoveStatsHost(object ownerItem);

        // Metadata
        /// <summary>更新变量元信息，例如显示名、说明和显示标记。</summary>
        RichResult TrySetVariableMeta(object ownerItem, string key, bool? display = null, string displayName = null, string description = null);

        /// <summary>更新常量元信息，例如显示名、说明和显示标记。</summary>
        RichResult TrySetConstantMeta(object ownerItem, string key, bool? display = null, string displayName = null, string description = null);

        // Transactions
        /// <summary>开始事务，并返回事务 token。</summary>
        string BeginTransaction(object ownerItem);

        /// <summary>提交事务。</summary>
        RichResult CommitTransaction(object ownerItem, string token);

        /// <summary>回滚事务。</summary>
        RichResult RollbackTransaction(object ownerItem, string token);
    }
}
