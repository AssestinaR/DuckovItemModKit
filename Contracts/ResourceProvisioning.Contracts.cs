using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
    /// <summary>
    /// 消耗状态补建草案的执行阶段。
    /// 用于描述一次 durability / use-count 补建请求当前执行到哪一步。
    /// </summary>
    public enum ResourceProvisioningPhase
    {
        /// <summary>尚未进入任何阶段。</summary>
        None = 0,
        /// <summary>解析并校验目标物品。</summary>
        ResolveOwner = 1,
        /// <summary>合并调用方给出的补建定义并判断是否允许覆盖。</summary>
        MergeDefinition = 2,
        /// <summary>把定义真正写入运行时状态。</summary>
        ApplyRuntimeState = 3,
        /// <summary>把草案定义写入变量持久化载体。</summary>
        PersistMetadata = 4,
        /// <summary>刷新 UI、事件或脏标记等运行时副作用。</summary>
        RefreshRuntime = 5,
        /// <summary>流程成功完成。</summary>
        Completed = 6,
        /// <summary>流程失败退出。</summary>
        Failed = 7,
    }

    /// <summary>
    /// 资源补建模式。
    /// 用于区分这次草案是在补 durability，还是在补 use-count / stack 语义。
    /// </summary>
    public enum ResourceProvisioningMode
    {
        /// <summary>补建 durability 语义。</summary>
        Durability = 0,
        /// <summary>补建 use-count 或 stack count 语义。</summary>
        UseCount = 1,
    }

    /// <summary>
    /// 单个 durability / use-count 初始化定义。
    /// 该对象描述“目标物品最后应该具备什么样的资源状态”。
    /// </summary>
    [Serializable]
    public sealed class ResourceProvisionDefinition
    {
        /// <summary>
        /// 补建模式。
        /// 该值决定 Current / Maximum / Loss 会被解释为 durability 还是 use-count 语义。
        /// </summary>
        public ResourceProvisioningMode Mode { get; set; }

        /// <summary>
        /// 当前值；Durability 下表示 Current，UseCount 下表示当前剩余次数。
        /// 该值应位于 0 和 Maximum 之间。
        /// </summary>
        public float Current { get; set; }

        /// <summary>
        /// 最大值；Durability 下表示 MaxDurability，UseCount 下表示 MaxUses。
        /// 对调用方来说，这是最关键的合法性字段，必须大于 0。
        /// </summary>
        public float Maximum { get; set; }

        /// <summary>
        /// 损耗值；当前仅对 Durability 生效。
        /// 当 Mode 为 UseCount 时，该值通常会被忽略。
        /// </summary>
        public float Loss { get; set; }

        /// <summary>
        /// 如果目标已存在同模式状态，是否允许覆盖。
        /// 为 false 时，目标已经具备相应状态会直接返回冲突失败。
        /// </summary>
        public bool OverwriteExisting { get; set; } = true;
    }

    /// <summary>
    /// 补建 durability / use-count 的草案请求。
    /// 用于统一描述目标物品、目标资源状态，以及成功后是否要刷新 UI、写回变量或立即持久化。
    /// </summary>
    [Serializable]
    public sealed class EnsureResourceProvisionRequest
    {
        /// <summary>
        /// 目标物品。
        /// 草案执行器会在该实例上写入 durability 或 use-count 相关运行时状态。
        /// </summary>
        public object OwnerItem { get; set; }

        /// <summary>
        /// 期望补建的资源定义。
        /// 为空时请求无意义，会直接按无效参数失败。
        /// </summary>
        public ResourceProvisionDefinition Definition { get; set; }

        /// <summary>
        /// 是否把草案定义写入变量持久化载体。
        /// 开启后，后续持久化回放路径可以按同样定义重新补建资源状态。
        /// </summary>
        public bool PersistDefinitionToVariables { get; set; } = true;

        /// <summary>
        /// 草案定义使用的持久化键；为空时由实现决定。
        /// 通常保留默认值即可，只有需要和其它实验性草案隔离时才建议自定义。
        /// </summary>
        public string PersistenceVariableKey { get; set; }

        /// <summary>
        /// 成功后是否刷新 UI。
        /// 适合当前物品正处于用户可见界面中的即时修改场景。
        /// </summary>
        public bool RefreshUI { get; set; } = true;

        /// <summary>
        /// 成功后是否发布运行时事件。
        /// 开启后，依赖物品变化事件的其它后置逻辑能更快感知补建结果。
        /// </summary>
        public bool PublishEvents { get; set; } = true;

        /// <summary>
        /// 成功后是否标记脏状态。
        /// 开启后，本次补建会被纳入 IMK 的持久化调度。
        /// </summary>
        public bool MarkDirty { get; set; } = true;

        /// <summary>
        /// 标记脏后是否立即强制 flush 持久化。
        /// 更适合想要“本次补建成功就立刻落盘”的场景。
        /// </summary>
        public bool ForceFlushPersistence { get; set; } = true;

        /// <summary>
        /// 调用方标签。
        /// 建议写入后置 mod 或内部工具自己的标识，方便 diagnostics 记录调用来源。
        /// </summary>
        public string CallerTag { get; set; }

        /// <summary>
        /// 附加诊断元数据。
        /// 调用方可放入额外上下文信息，供日志或调试面板展示。
        /// </summary>
        public Dictionary<string, object> DiagnosticsMetadata { get; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// 补建 durability / use-count 的共享诊断信息。
    /// 用于说明本次草案在各阶段花了多久、是否真的改到了运行时状态、以及是否成功写入持久化草案。
    /// </summary>
    [Serializable]
    public sealed class EnsureResourceProvisionDiagnostics
    {
        /// <summary>开始时间（UTC）。</summary>
        public DateTime StartedAtUtc { get; set; }

        /// <summary>结束时间（UTC）。</summary>
        public DateTime CompletedAtUtc { get; set; }

        /// <summary>各阶段耗时，单位为毫秒。</summary>
        public Dictionary<ResourceProvisioningPhase, long> PhaseTimings { get; } = new Dictionary<ResourceProvisioningPhase, long>();

        /// <summary>是否真正把定义应用到了运行时状态。</summary>
        public bool RuntimeStateApplied { get; set; }

        /// <summary>是否成功写入了持久化元数据。</summary>
        public bool MetadataPersisted { get; set; }

        /// <summary>附加元数据。</summary>
        public Dictionary<string, object> Metadata { get; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// 补建 durability / use-count 的结果。
    /// 该对象描述成功路径上最终补建了什么，以及是否已经触发了脏标记、持久化和诊断记录。
    /// </summary>
    [Serializable]
    public sealed class EnsureResourceProvisionResult
    {
        /// <summary>结束时所在阶段。</summary>
        public ResourceProvisioningPhase FinalPhase { get; set; }

        /// <summary>最终目标物品。</summary>
        public object OwnerItem { get; set; }

        /// <summary>本次补建的模式。</summary>
        public ResourceProvisioningMode Mode { get; set; }

        /// <summary>是否真正把定义应用到了运行时状态。</summary>
        public bool RuntimeStateApplied { get; set; }

        /// <summary>是否成功写入了持久化元数据。</summary>
        public bool MetadataPersisted { get; set; }

        /// <summary>是否标记了脏状态。</summary>
        public bool DirtyMarked { get; set; }

        /// <summary>是否执行了持久化 flush。</summary>
        public bool PersistenceFlushed { get; set; }

        /// <summary>本次补建对应的共享 diagnostics。</summary>
        public EnsureResourceProvisionDiagnostics Diagnostics { get; set; }
    }
}