using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
    /// <summary>restore 请求的来源类别。</summary>
    internal enum RestoreSourceKind
    {
        /// <summary>未知来源。</summary>
        Unknown = 0,

        /// <summary>来自 clone 管线。</summary>
        Clone = 1,

        /// <summary>来自 rebirth/replace 流程。</summary>
        Rebirth = 2,

        /// <summary>来自持久化元数据恢复。</summary>
        Persistence = 3,

        /// <summary>来自 vanilla tree data 或导出树重建。</summary>
        VanillaTreeData = 4,
    }

    /// <summary>restore 的目标附加模式。</summary>
    internal enum RestoreTargetMode
    {
        /// <summary>只恢复出 detached 根物品，不做附加。</summary>
        DetachedTree = 0,

        /// <summary>附加到通过 target key 或 fallback 解析出来的宿主。</summary>
        AttachToResolvedHost = 1,

        /// <summary>附加到显式给定的背包对象。</summary>
        AttachToExplicitInventory = 2,

        /// <summary>尝试附加到角色槽位体系。</summary>
        AttachToCharacter = 3,

        /// <summary>附加到显式给定的槽位。</summary>
        AttachToExplicitSlot = 4,
    }

    /// <summary>restore 管线阶段。</summary>
    public enum RestorePhase
    {
        /// <summary>尚未进入任何阶段。</summary>
        None = 0,

        /// <summary>解码输入数据，例如解析导出树或元数据。</summary>
        Decode = 1,

        /// <summary>创建根物品或节点实例。</summary>
        Instantiate = 2,

        /// <summary>把元数据、变量、标签等内容写回新对象。</summary>
        Hydrate = 3,

        /// <summary>连接树结构、背包关系或槽位关系。</summary>
        Connect = 4,

        /// <summary>把新对象附加到最终目标宿主。</summary>
        Attach = 5,

        /// <summary>执行最终收尾，例如 handle 绑定、diagnostics 整理等。</summary>
        Finalize = 6,

        /// <summary>整个流程成功完成。</summary>
        Completed = 7,

        /// <summary>流程失败退出。</summary>
        Failed = 8,
    }

    /// <summary>
    /// restore 请求对象。
    /// 它同时承载调用方输入、执行偏好和 diagnostics 元数据，是 clone/rebirth/persistence/tree restore 共享的内部契约。
    /// </summary>
    internal sealed class RestoreRequest
    {
        /// <summary>原始来源对象，例如 source item、ItemMeta 或导出数据。</summary>
        public object Source { get; set; }

        /// <summary>已提前构建好的根对象；不为 null 时可跳过部分 instantiate 流程。</summary>
        public object PreparedRoot { get; set; }

        /// <summary>来源类别。</summary>
        public RestoreSourceKind SourceKind { get; set; }

        /// <summary>目标对象；可能是具体背包、具体槽位宿主或 target key。</summary>
        public object Target { get; set; }

        /// <summary>目标附加模式。</summary>
        public RestoreTargetMode TargetMode { get; set; }

        /// <summary>请求的克隆/恢复策略。</summary>
        public CloneStrategy Strategy { get; set; }

        /// <summary>变量合并模式。</summary>
        public VariableMergeMode VariableMergeMode { get; set; }

        /// <summary>是否复制标签。</summary>
        public bool CopyTags { get; set; }

        /// <summary>是否允许降级路径或 fallback。</summary>
        public bool AllowDegraded { get; set; }

        /// <summary>是否在 restore 期间发布事件。</summary>
        public bool PublishEvents { get; set; }

        /// <summary>是否在附加后刷新 UI。</summary>
        public bool RefreshUI { get; set; }

        /// <summary>是否在成功后标记脏状态。</summary>
        public bool MarkDirty { get; set; }

        /// <summary>是否开启 shared diagnostics 收集。</summary>
        public bool DiagnosticsEnabled { get; set; }

        /// <summary>调用方标签，用于 diagnostics 识别来源。</summary>
        public string CallerTag { get; set; }

        /// <summary>解析后的 target key。</summary>
        public string ResolvedTargetKey { get; set; }

        /// <summary>背包附加时优先使用的索引；小于 0 表示不强制位置。</summary>
        public int PreferredInventoryIndex { get; set; } = -1;

        /// <summary>角色附加时优先尝试的槽位起始索引。</summary>
        public int PreferredCharacterSlotIndex { get; set; } = 0;

        /// <summary>显式槽位附加时的槽位键。</summary>
        public string PreferredSlotKey { get; set; }

        /// <summary>变量键过滤器；为 null 时不过滤。</summary>
        public Func<string, bool> AcceptVariableKey { get; set; }

        /// <summary>自定义实例化入口；用于 rebirth 等需要特殊建根逻辑的场景。</summary>
        public Func<RichResult<object>> CustomInstantiate { get; set; }

        /// <summary>自定义 hydrate 回调；用于在新根创建后补充游戏特定数据。</summary>
        public Action<object> CustomHydrate { get; set; }

        /// <summary>附加 diagnostics 元数据。</summary>
        public Dictionary<string, object> DiagnosticsMetadata { get; } = new Dictionary<string, object>();

        /// <summary>diagnostics 完成后的回调。</summary>
        public Action<RestoreDiagnostics, RestoreResult> DiagnosticsFinalized { get; set; }
    }

    /// <summary>restore 共享诊断信息。</summary>
    public sealed class RestoreDiagnostics
    {
        /// <summary>开始时间（UTC）。</summary>
        public DateTime StartedAtUtc { get; set; }

        /// <summary>结束时间（UTC）。</summary>
        public DateTime CompletedAtUtc { get; set; }

        /// <summary>各阶段耗时，单位为毫秒。</summary>
        public Dictionary<RestorePhase, long> PhaseTimings { get; } = new Dictionary<RestorePhase, long>();

        /// <summary>是否使用了 fallback 或降级路径。</summary>
        public bool FallbackUsed { get; set; }

        /// <summary>最终采用的策略标签。</summary>
        public string StrategyUsed { get; set; }

        /// <summary>重试次数。</summary>
        public int RetryCount { get; set; }

        /// <summary>额外元数据集合。</summary>
        public Dictionary<string, object> Metadata { get; } = new Dictionary<string, object>();
    }

    /// <summary>restore 成功路径的内部结果对象。</summary>
    internal sealed class RestoreResult
    {
        /// <summary>结束时所在阶段。</summary>
        public RestorePhase FinalPhase { get; set; }

        /// <summary>恢复出的根物品。</summary>
        public object RootItem { get; set; }

        /// <summary>是否完成了树级恢复。</summary>
        public bool TreeRestored { get; set; }

        /// <summary>是否已经附加到目标宿主。</summary>
        public bool Attached { get; set; }

        /// <summary>是否安排了延迟附加。</summary>
        public bool DeferredScheduled { get; set; }

        /// <summary>附加索引；未知时为 -1。</summary>
        public int AttachedIndex { get; set; } = -1;

        /// <summary>实际采用的策略标签。</summary>
        public string StrategyUsed { get; set; }

        /// <summary>共享诊断对象。</summary>
        public RestoreDiagnostics Diagnostics { get; set; }
    }

    /// <summary>
    /// restore 执行结果基类。
    /// 供 clone、persistence restore、tree restore、rebirth 等不同 facade 在内部复用统一的成功/失败映射逻辑。
    /// </summary>
    internal class RestoreExecutionResultBase
    {
        /// <summary>是否成功。</summary>
        public bool Succeeded { get; set; }

        /// <summary>恢复出的根物品。</summary>
        public object RootItem { get; set; }

        /// <summary>失败时的错误码。</summary>
        public ErrorCode ErrorCode { get; set; }

        /// <summary>失败时的错误消息。</summary>
        public string Error { get; set; }

        /// <summary>目标模式。</summary>
        public RestoreTargetMode TargetMode { get; set; }

        /// <summary>结束时所在阶段。</summary>
        public RestorePhase FinalPhase { get; set; }

        /// <summary>实际采用的策略标签。</summary>
        public string StrategyUsed { get; set; }

        /// <summary>是否已经附加到目标宿主。</summary>
        public bool Attached { get; set; }

        /// <summary>是否安排了延迟附加。</summary>
        public bool DeferredScheduled { get; set; }

        /// <summary>附加索引；未知时为 -1。</summary>
        public int AttachedIndex { get; set; } = -1;

        /// <summary>共享 diagnostics。</summary>
        public RestoreDiagnostics Diagnostics { get; set; }

        /// <summary>把当前对象标记为失败，并写入错误上下文。</summary>
        public void ApplyFailure(ErrorCode code, string error, RestoreDiagnostics diagnostics = null, RestorePhase phase = RestorePhase.Failed)
        {
            Succeeded = false;
            ErrorCode = code;
            Error = error;
            Diagnostics = diagnostics;
            FinalPhase = phase;
        }

        /// <summary>从 RestoreResult 复制成功字段到当前对象。</summary>
        public void ApplyRestoreSuccess(RestoreResult restore, RestoreDiagnostics fallbackDiagnostics = null)
        {
            if (restore == null)
            {
                ApplyFailure(ErrorCode.OperationFailed, "restore result null", fallbackDiagnostics);
                return;
            }

            Succeeded = true;
            RootItem = restore.RootItem;
            Attached = restore.Attached;
            DeferredScheduled = restore.DeferredScheduled;
            AttachedIndex = restore.AttachedIndex;
            FinalPhase = restore.FinalPhase;
            StrategyUsed = restore.StrategyUsed;
            Diagnostics = restore.Diagnostics ?? fallbackDiagnostics;
            ErrorCode = ErrorCode.None;
            Error = null;
        }

        /// <summary>解析策略标签；优先当前结果上的 StrategyUsed，其次 diagnostics 中的 StrategyUsed。</summary>
        public string ResolveStrategyLabel()
        {
            return StrategyUsed ?? Diagnostics?.StrategyUsed ?? "unknown";
        }

        /// <summary>解析附加状态标签。</summary>
        public string ResolveAttachStateLabel()
        {
            if (Diagnostics != null && Diagnostics.Metadata.TryGetValue("attached", out var attachedObj) && attachedObj is bool attached)
            {
                return attached ? "attached" : "not-attached";
            }

            return Attached ? "attached" : "not-attached";
        }

        /// <summary>构建统一失败消息。</summary>
        public string BuildFailureMessage(params KeyValuePair<string, string>[] extraParts)
        {
            var parts = new List<string>
            {
                "strategy=" + ResolveStrategyLabel(),
            };

            if (extraParts != null)
            {
                foreach (var part in extraParts)
                {
                    if (string.IsNullOrEmpty(part.Key)) continue;
                    parts.Add(part.Key + "=" + (part.Value ?? string.Empty));
                }
            }

            return string.Concat(Error ?? "restore failed", " [", string.Join(", ", parts.ToArray()), "]");
        }
    }

    /// <summary>restore orchestrator 的内部接口。</summary>
    internal interface ITreeRestoreOrchestrator
    {
        /// <summary>执行一次 restore 请求。</summary>
        RichResult<RestoreResult> Execute(RestoreRequest request);
    }
}