using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
    /// <summary>消耗状态补建草案的执行阶段。</summary>
    public enum ResourceProvisioningPhase
    {
        None = 0,
        ResolveOwner = 1,
        MergeDefinition = 2,
        ApplyRuntimeState = 3,
        PersistMetadata = 4,
        RefreshRuntime = 5,
        Completed = 6,
        Failed = 7,
    }

    /// <summary>资源补建模式。</summary>
    public enum ResourceProvisioningMode
    {
        Durability = 0,
        UseCount = 1,
    }

    /// <summary>单个 durability/use-count 初始化定义。</summary>
    [Serializable]
    public sealed class ResourceProvisionDefinition
    {
        /// <summary>补建模式。</summary>
        public ResourceProvisioningMode Mode { get; set; }

        /// <summary>当前值；Durability 下表示 Current，UseCount 下表示当前剩余次数。</summary>
        public float Current { get; set; }

        /// <summary>最大值；Durability 下表示 MaxDurability，UseCount 下表示 MaxUses。</summary>
        public float Maximum { get; set; }

        /// <summary>损耗值；当前仅对 Durability 生效。</summary>
        public float Loss { get; set; }

        /// <summary>如果目标已存在同模式状态，是否允许覆盖。</summary>
        public bool OverwriteExisting { get; set; } = true;
    }

    /// <summary>补建 durability/use-count 的草案请求。</summary>
    [Serializable]
    public sealed class EnsureResourceProvisionRequest
    {
        /// <summary>目标物品。</summary>
        public object OwnerItem { get; set; }

        /// <summary>期望补建的资源定义。</summary>
        public ResourceProvisionDefinition Definition { get; set; }

        /// <summary>是否把草案定义写入变量持久化载体。</summary>
        public bool PersistDefinitionToVariables { get; set; } = true;

        /// <summary>草案定义使用的持久化键；为空时由实现决定。</summary>
        public string PersistenceVariableKey { get; set; }

        /// <summary>成功后是否刷新 UI。</summary>
        public bool RefreshUI { get; set; } = true;

        /// <summary>成功后是否发布运行时事件。</summary>
        public bool PublishEvents { get; set; } = true;

        /// <summary>成功后是否标记脏状态。</summary>
        public bool MarkDirty { get; set; } = true;

        /// <summary>标记脏后是否立即强制 flush 持久化。</summary>
        public bool ForceFlushPersistence { get; set; } = true;

        /// <summary>调用方标签。</summary>
        public string CallerTag { get; set; }

        /// <summary>附加诊断元数据。</summary>
        public Dictionary<string, object> DiagnosticsMetadata { get; } = new Dictionary<string, object>();
    }

    /// <summary>补建 durability/use-count 的共享诊断信息。</summary>
    [Serializable]
    public sealed class EnsureResourceProvisionDiagnostics
    {
        public DateTime StartedAtUtc { get; set; }
        public DateTime CompletedAtUtc { get; set; }
        public Dictionary<ResourceProvisioningPhase, long> PhaseTimings { get; } = new Dictionary<ResourceProvisioningPhase, long>();
        public bool RuntimeStateApplied { get; set; }
        public bool MetadataPersisted { get; set; }
        public Dictionary<string, object> Metadata { get; } = new Dictionary<string, object>();
    }

    /// <summary>补建 durability/use-count 的结果。</summary>
    [Serializable]
    public sealed class EnsureResourceProvisionResult
    {
        public ResourceProvisioningPhase FinalPhase { get; set; }
        public object OwnerItem { get; set; }
        public ResourceProvisioningMode Mode { get; set; }
        public bool RuntimeStateApplied { get; set; }
        public bool MetadataPersisted { get; set; }
        public bool DirtyMarked { get; set; }
        public bool PersistenceFlushed { get; set; }
        public EnsureResourceProvisionDiagnostics Diagnostics { get; set; }
    }
}