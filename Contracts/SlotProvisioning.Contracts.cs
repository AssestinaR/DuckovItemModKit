using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
    /// <summary>补槽草案管线的执行阶段。</summary>
    public enum SlotProvisioningPhase
    {
        /// <summary>尚未进入任何阶段。</summary>
        None = 0,

        /// <summary>解析并校验目标物品。</summary>
        ResolveOwner = 1,

        /// <summary>确保目标物品具备可写槽位宿主。</summary>
        EnsureSlotHost = 2,

        /// <summary>合并已有槽位并生成新增槽位计划。</summary>
        MergeDefinitions = 3,

        /// <summary>写入持久化元数据。</summary>
        PersistMetadata = 4,

        /// <summary>刷新运行时缓存、UI 或派生状态。</summary>
        RefreshRuntime = 5,

        /// <summary>流程成功完成。</summary>
        Completed = 6,

        /// <summary>流程失败退出。</summary>
        Failed = 7,
    }

    /// <summary>补槽时可选的模板来源。</summary>
    [Serializable]
    public sealed class SlotProvisionTemplateReference
    {
        /// <summary>优先复用的模板槽位键。</summary>
        public string TemplateSlotKey { get; set; }

        /// <summary>显式给定的模板槽位对象；不为 null 时优先于键查找。</summary>
        public object TemplateSlot { get; set; }

        /// <summary>是否复制模板的过滤标签。</summary>
        public bool CloneFilters { get; set; } = true;

        /// <summary>是否复制模板图标。</summary>
        public bool CloneIcon { get; set; } = true;

        /// <summary>是否复制模板显示名。</summary>
        public bool CloneDisplayName { get; set; }
    }

    /// <summary>单个目标槽位的草案定义。</summary>
    [Serializable]
    public sealed class SlotProvisionDefinition
    {
        /// <summary>槽位键；在宿主物品内应唯一。</summary>
        public string Key { get; set; }

        /// <summary>槽位显示名。</summary>
        public string DisplayName { get; set; }

        /// <summary>槽位图标对象；允许为 null。</summary>
        public object SlotIcon { get; set; }

        /// <summary>必需标签集合；为空时不做必需标签限制。</summary>
        public string[] RequireTags { get; set; }

        /// <summary>排除标签集合；命中任一标签时拒绝插入。</summary>
        public string[] ExcludeTags { get; set; }

        /// <summary>是否禁止插入相同 TypeID 的物品；为 null 时保持实现默认值。</summary>
        public bool? ForbidItemsWithSameID { get; set; }

        /// <summary>模板来源；用于从现有槽位克隆过滤器或图标等信息。</summary>
        public SlotProvisionTemplateReference Template { get; set; }

        /// <summary>如果目标键已存在，是否将其视为满足而不是失败。</summary>
        public bool ReuseExistingIfPresent { get; set; } = true;
    }

    /// <summary>
    /// “给原本无槽位或槽位定义不足的物品补槽”的内部草案请求。
    /// 该类型用于先固定实现语言与 Probe 验证输入，不代表稳定公开 API 已冻结。
    /// </summary>
    [Serializable]
    public sealed class EnsureSlotsRequest
    {
        /// <summary>目标宿主物品。</summary>
        public object OwnerItem { get; set; }

        /// <summary>期望最终存在的槽位集合。</summary>
        public SlotProvisionDefinition[] DesiredSlots { get; set; } = Array.Empty<SlotProvisionDefinition>();

        /// <summary>当宿主当前没有槽位系统时，是否尝试创建槽位宿主。</summary>
        public bool CreateSlotHostIfMissing { get; set; } = true;

        /// <summary>是否把动态槽位定义写入变量或其他持久化载体。</summary>
        public bool PersistDefinitionsToVariables { get; set; }

        /// <summary>动态槽位定义使用的持久化键；为空时由实现决定。</summary>
        public string PersistenceVariableKey { get; set; }

        /// <summary>成功后是否刷新 UI。</summary>
        public bool RefreshUI { get; set; } = true;

        /// <summary>成功后是否发布运行时事件。</summary>
        public bool PublishEvents { get; set; } = true;

        /// <summary>成功后是否标记脏状态。</summary>
        public bool MarkDirty { get; set; } = true;

        /// <summary>标记脏后是否立即强制 flush 持久化。</summary>
        public bool ForceFlushPersistence { get; set; } = true;

        /// <summary>调用方标签，用于 diagnostics 和 Probe 识别来源。</summary>
        public string CallerTag { get; set; }

        /// <summary>额外诊断元数据。</summary>
        public Dictionary<string, object> DiagnosticsMetadata { get; } = new Dictionary<string, object>();
    }

    /// <summary>补槽草案的共享诊断信息。</summary>
    [Serializable]
    public sealed class EnsureSlotsDiagnostics
    {
        /// <summary>开始时间（UTC）。</summary>
        public DateTime StartedAtUtc { get; set; }

        /// <summary>结束时间（UTC）。</summary>
        public DateTime CompletedAtUtc { get; set; }

        /// <summary>各阶段耗时，单位为毫秒。</summary>
        public Dictionary<SlotProvisioningPhase, long> PhaseTimings { get; } = new Dictionary<SlotProvisioningPhase, long>();

        /// <summary>是否实际创建了槽位宿主。</summary>
        public bool SlotHostCreated { get; set; }

        /// <summary>是否复用了已有槽位作为模板来源。</summary>
        public bool TemplateUsed { get; set; }

        /// <summary>是否写入了持久化元数据。</summary>
        public bool MetadataPersisted { get; set; }

        /// <summary>附加元数据。</summary>
        public Dictionary<string, object> Metadata { get; } = new Dictionary<string, object>();
    }

    /// <summary>补槽草案成功路径的内部结果。</summary>
    [Serializable]
    public sealed class EnsureSlotsResult
    {
        /// <summary>结束时所在阶段。</summary>
        public SlotProvisioningPhase FinalPhase { get; set; }

        /// <summary>最终宿主物品。</summary>
        public object OwnerItem { get; set; }

        /// <summary>本次新建的槽位键集合。</summary>
        public string[] CreatedSlotKeys { get; set; } = Array.Empty<string>();

        /// <summary>已存在并被复用的槽位键集合。</summary>
        public string[] ReusedSlotKeys { get; set; } = Array.Empty<string>();

        /// <summary>被拒绝或跳过的槽位键集合。</summary>
        public string[] RejectedSlotKeys { get; set; } = Array.Empty<string>();

        /// <summary>是否实际创建了槽位宿主。</summary>
        public bool SlotHostCreated { get; set; }

        /// <summary>是否写入了持久化元数据。</summary>
        public bool MetadataPersisted { get; set; }

        /// <summary>是否触发了运行时刷新。</summary>
        public bool RuntimeRefreshTriggered { get; set; }

        /// <summary>是否标记了脏状态。</summary>
        public bool DirtyMarked { get; set; }

        /// <summary>是否执行了持久化 flush。</summary>
        public bool PersistenceFlushed { get; set; }

        /// <summary>共享 diagnostics。</summary>
        public EnsureSlotsDiagnostics Diagnostics { get; set; }
    }
}