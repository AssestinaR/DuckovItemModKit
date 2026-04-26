using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
    /// <summary>槽位持久化草案的当前 schema 版本。</summary>
    public static class SlotPersistenceDraftSchema
    {
        /// <summary>当前 schema 版本号。</summary>
        public const int CurrentVersion = 2;
    }

    /// <summary>槽位来源分类提示。</summary>
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

    /// <summary>槽位变更草案类型。</summary>
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
    [Serializable]
    public sealed class SlotPersistenceDraftData
    {
        /// <summary>schema 版本。</summary>
        public int SchemaVersion { get; set; } = SlotPersistenceDraftSchema.CurrentVersion;

        /// <summary>
        /// 动态新增槽位定义集合。
        /// 这里继续沿用既有 JSON 键名 Slots，保持对旧存档的兼容读取。
        /// </summary>
        public List<SlotPersistenceSlotDefinition> Slots { get; set; } = new List<SlotPersistenceSlotDefinition>();

        /// <summary>被持久化移除的原版槽位键集合。</summary>
        public List<string> RemovedBuiltinSlotKeys { get; set; } = new List<string>();

        /// <summary>更细粒度的槽位修改记录；当前主要作为后续演进预留。</summary>
        public List<SlotPersistenceSlotMutation> Mutations { get; set; } = new List<SlotPersistenceSlotMutation>();
    }

    /// <summary>单个动态槽位定义。</summary>
    [Serializable]
    public sealed class SlotPersistenceSlotDefinition
    {
        /// <summary>槽位键。</summary>
        public string Key { get; set; }

        /// <summary>显示名草案。</summary>
        public string DisplayName { get; set; }

        /// <summary>模板槽位键。</summary>
        public string TemplateSlotKey { get; set; }

        /// <summary>必需标签集合。</summary>
        public string[] RequireTags { get; set; }

        /// <summary>排除标签集合。</summary>
        public string[] ExcludeTags { get; set; }

        /// <summary>是否禁止相同 TypeID。</summary>
        public bool? ForbidItemsWithSameID { get; set; }

        /// <summary>来源分类提示。</summary>
        public SlotPersistenceOriginHint OriginHint { get; set; } = SlotPersistenceOriginHint.Dynamic;
    }

    /// <summary>单条槽位修改记录。</summary>
    [Serializable]
    public sealed class SlotPersistenceSlotMutation
    {
        /// <summary>目标槽位键。</summary>
        public string Key { get; set; }

        /// <summary>修改类型。</summary>
        public SlotPersistenceMutationKind Kind { get; set; }

        /// <summary>目标槽位来源分类提示。</summary>
        public SlotPersistenceOriginHint OriginHint { get; set; } = SlotPersistenceOriginHint.Unknown;

        /// <summary>可选的必需标签覆盖。</summary>
        public string[] RequireTags { get; set; }

        /// <summary>可选的排除标签覆盖。</summary>
        public string[] ExcludeTags { get; set; }

        /// <summary>可选的 forbid same-id 覆盖。</summary>
        public bool? ForbidItemsWithSameID { get; set; }
    }
}