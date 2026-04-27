using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
    /// <summary>
    /// 槽位持久化草案的当前 schema 版本。
    /// 用于区分不同版本的草案 JSON 结构，方便后续做兼容读取或渐进迁移。
    /// </summary>
    public static class SlotPersistenceDraftSchema
    {
        /// <summary>
        /// 当前 schema 版本号。
        /// 写入草案时通常应落这个值；读取老版本草案时可据此决定是否执行迁移逻辑。
        /// </summary>
        public const int CurrentVersion = 2;
    }

    /// <summary>
    /// 槽位来源分类提示。
    /// 该枚举不直接决定运行时行为，主要用于持久化草案、调试信息和后续迁移策略辨识槽位来源。
    /// </summary>
    [Serializable]
    public enum SlotPersistenceOriginHint
    {
        /// <summary>未知来源。</summary>
        Unknown = 0,

        /// <summary>原版自带槽位。</summary>
        Builtin = 1,

        /// <summary>IMK 动态新增槽位。</summary>
        Dynamic = 2,
    }

    /// <summary>
    /// 槽位变更草案类型。
    /// 用于描述“这条持久化 mutation 到底想改什么”，便于后续在重放阶段按类型分派处理逻辑。
    /// </summary>
    [Serializable]
    public enum SlotPersistenceMutationKind
    {
        /// <summary>未指定。</summary>
        None = 0,

        /// <summary>移除原版槽位。</summary>
        RemoveBuiltinSlot = 1,

        /// <summary>移除动态槽位。</summary>
        RemoveDynamicSlot = 2,

        /// <summary>修改槽位过滤标签。</summary>
        OverrideFilters = 3,

        /// <summary>修改槽位图标。</summary>
        OverrideIcon = 4,

        /// <summary>修改 forbid same-id 语义。</summary>
        OverrideForbidSameId = 5,
    }

    /// <summary>
    /// 槽位持久化草案总载体。
    /// 当前先兼容已有 DynamicSlotsDraft 的“新增槽位定义”语义，并为后续原版槽位 tombstone / 槽位修改记录预留字段。
    /// </summary>
    /// <remarks>
    /// 对后置作者来说，可以把它理解成“某个物品的动态槽结构补丁包”：
    /// `Slots` 记录应存在的动态槽，`RemovedBuiltinSlotKeys` 记录应被 tombstone 的原版槽，`Mutations` 记录更细粒度的后续改动。
    /// </remarks>
    [Serializable]
    public sealed class SlotPersistenceDraftData
    {
        /// <summary>
        /// schema 版本。
        /// 读写该草案时应优先参考此值，以判断是否需要兼容旧格式或补跑迁移。
        /// </summary>
        public int SchemaVersion { get; set; } = SlotPersistenceDraftSchema.CurrentVersion;

        /// <summary>
        /// 动态新增槽位定义集合。
        /// 这里继续沿用既有 JSON 键名 Slots，保持对旧存档的兼容读取。
        /// </summary>
        public List<SlotPersistenceSlotDefinition> Slots { get; set; } = new List<SlotPersistenceSlotDefinition>();

        /// <summary>
        /// 被持久化移除的原版槽位键集合。
        /// 这些键代表“原版本来存在，但在当前草案语义下应被删除”的 tombstone 记录。
        /// </summary>
        public List<string> RemovedBuiltinSlotKeys { get; set; } = new List<string>();

        /// <summary>
        /// 更细粒度的槽位修改记录；当前主要作为后续演进预留。
        /// 当仅靠 Slots / RemovedBuiltinSlotKeys 不能完整表达变更时，可通过这里记录覆盖式 mutation。
        /// </summary>
        public List<SlotPersistenceSlotMutation> Mutations { get; set; } = new List<SlotPersistenceSlotMutation>();
    }

    /// <summary>
    /// 单个动态槽位定义。
    /// 这是写入草案 JSON 的持久化视图，语义上和运行时的 <see cref="SlotProvisionDefinition"/> 接近，但更偏向存档兼容。
    /// </summary>
    [Serializable]
    public sealed class SlotPersistenceSlotDefinition
    {
        /// <summary>
        /// 槽位键。
        /// 它既是回放时的目标键，也是同步运行时状态时用于匹配现有槽位的主键。
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// 显示名草案。
        /// 该值主要用于保留动态槽定义的展示语义；若运行时不支持直接写入显示名，可作为调试或未来迁移输入。
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 模板槽位键。
        /// 若回放时仍能解析到该模板键，可继续使用它补齐过滤器、图标等未显式存下来的结构信息。
        /// </summary>
        public string TemplateSlotKey { get; set; }

        /// <summary>
        /// 必需标签集合。
        /// 回放动态槽位时，这些标签会成为槽位的 require 约束来源之一。
        /// </summary>
        public string[] RequireTags { get; set; }

        /// <summary>
        /// 排除标签集合。
        /// 回放动态槽位时，这些标签会成为槽位的 exclude 约束来源之一。
        /// </summary>
        public string[] ExcludeTags { get; set; }

        /// <summary>
        /// 是否禁止相同 TypeID。
        /// 用于保留槽位关于“是否允许重复类型插入”的核心语义。
        /// </summary>
        public bool? ForbidItemsWithSameID { get; set; }

        /// <summary>
        /// 来源分类提示。
        /// 绝大多数由补槽草案生成的定义都应为 Dynamic；只有做兼容迁移时才可能出现其它值。
        /// </summary>
        public SlotPersistenceOriginHint OriginHint { get; set; } = SlotPersistenceOriginHint.Dynamic;
    }

    /// <summary>
    /// 单条槽位修改记录。
    /// 当需要表达“不是新增/删除，而是改槽位某个属性”时，使用这类 mutation 进行补充。
    /// </summary>
    [Serializable]
    public sealed class SlotPersistenceSlotMutation
    {
        /// <summary>
        /// 目标槽位键。
        /// 回放 mutation 时，会优先在当前宿主的槽位集合中定位这个键对应的槽位。
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// 修改类型。
        /// 决定当前这条 mutation 应被解释为删除、覆盖过滤器、覆盖图标还是覆盖 forbid same-id 等动作。
        /// </summary>
        public SlotPersistenceMutationKind Kind { get; set; }

        /// <summary>
        /// 目标槽位来源分类提示。
        /// 用于帮助回放逻辑区分当前键对应的是原版槽还是动态槽，避免误删或误覆盖。
        /// </summary>
        public SlotPersistenceOriginHint OriginHint { get; set; } = SlotPersistenceOriginHint.Unknown;

        /// <summary>
        /// 可选的必需标签覆盖。
        /// 只有当 <see cref="Kind"/> 对应需要覆盖过滤器时，这个字段才应被解释和应用。
        /// </summary>
        public string[] RequireTags { get; set; }

        /// <summary>
        /// 可选的排除标签覆盖。
        /// 与 <see cref="RequireTags"/> 一起构成完整的过滤器覆盖语义。
        /// </summary>
        public string[] ExcludeTags { get; set; }

        /// <summary>
        /// 可选的 forbid same-id 覆盖。
        /// 仅在 mutation 需要覆盖该语义位时使用；为 null 表示不修改当前实现值。
        /// </summary>
        public bool? ForbidItemsWithSameID { get; set; }
    }
}