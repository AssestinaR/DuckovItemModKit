using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
    /// <summary>
    /// Rebirth 的显式意图。
    /// SafeReplace 表示尽量保持当前 IMK 接管与替换语义；
    /// CleanRebirth 表示以“新实例替换旧实例”为目标，但不主动继承 IMK 管理痕迹。
    /// </summary>
    public enum RebirthIntent
    {
        /// <summary>安全替换：保留当前 IMK 接管语义。</summary>
        SafeReplace = 0,

        /// <summary>干净重生：面向未来“从 IMK 释放”的显式意图。</summary>
        CleanRebirth = 1,
    }

    /// <summary>
    /// 重生辅助：统一包装 IRebirthService.ReplaceRebirth 调用，处理入参校验与异常捕获。
    /// </summary>
    public static class Rebirth
    {
        /// <summary>
        /// 用指定元数据替换旧物品，支持 keepLocation 控制是否保持原位置。
        /// 这是面向最小成功语义的包装器；如果调用方需要结构化 diagnostics、回滚处置和 operator alert，应改走 IMKDuckov 的 detailed/report facade。
        /// </summary>
        /// <param name="svc">重生服务实现。</param>
        /// <param name="oldItem">旧物品。</param>
        /// <param name="meta">元数据。</param>
        /// <param name="keepLocation">是否保持位置。</param>
        /// <returns>结果，包含新物品。</returns>
        public static RichResult<object> Replace(IRebirthService svc, object oldItem, ItemMeta meta, bool keepLocation = true)
        {
            if (svc == null || oldItem == null)
            {
                Log.Warn("Rebirth.Replace invalid args: svc/oldItem must not be null");
                return RichResult<object>.Fail(ErrorCode.InvalidArgument, "invalid args");
            }
            try { return svc.ReplaceRebirth(oldItem, meta, keepLocation); }
            catch (Exception ex)
            {
                Log.Error("Rebirth.Replace threw", ex);
                return RichResult<object>.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }
    }

    /// <summary>
    /// Rebirth 结构化结果：承载新根物品、共享诊断快照以及 failure/recovery/operator alert 元数据。
    /// </summary>
    public sealed class RebirthRestoreResult
    {
        /// <summary>报告生成时间（UTC）。</summary>
        public DateTime ReportedAtUtc { get; set; }

        /// <summary>本次 rebirth 是否完整成功。</summary>
        public bool Succeeded { get; set; }

        /// <summary>失败时的错误码；成功时为 None。</summary>
        public ErrorCode ErrorCode { get; set; }

        /// <summary>失败时的错误消息；成功时为空。</summary>
        public string Error { get; set; }

        /// <summary>重生得到的新根物品。</summary>
        public object RootItem { get; set; }

        /// <summary>是否已成功附加到目标宿主。</summary>
        public bool Attached { get; set; }

        /// <summary>目标宿主是否已成功解析。</summary>
        public bool TargetResolved { get; set; }

        /// <summary>附加索引；未知或不适用时为 -1。</summary>
        public int AttachedIndex { get; set; } = -1;

        /// <summary>实际使用的恢复策略。</summary>
        public string StrategyUsed { get; set; }

        /// <summary>本次 rebirth 采用的显式意图。</summary>
        public RebirthIntent IntentUsed { get; set; } = RebirthIntent.SafeReplace;

        /// <summary>回滚结果标签；成功路径通常为空，例如 restored-inventory-index、old-tree-still-in-place。</summary>
        public string RollbackOutcome { get; set; }

        /// <summary>失败类别；成功路径通常为空，例如 attach-target-unresolved、attach-failed。</summary>
        public string FailureKind { get; set; }

        /// <summary>失败动作；成功路径通常为空，例如 rollback-and-fail、preserve-old-tree-and-fail。</summary>
        public string FailureAction { get; set; }

        /// <summary>失败阶段；成功路径通常为空，例如 pre-detach、restore、attach。</summary>
        public string FailurePhase { get; set; }

        /// <summary>失败矩阵键；成功路径通常为空，用于精确区分 failureKind、attachOutcome 和 rollbackOutcome 的组合。</summary>
        public string FailureMatrixKey { get; set; }

        /// <summary>策略决策；成功路径通常为空，表示当前 failure matrix 对应的总体处理策略。</summary>
        public string PolicyDecision { get; set; }

        /// <summary>细分矩阵策略键；成功路径通常为空，通常可直接用于诊断页或日志聚类。</summary>
        public string MatrixPolicyKey { get; set; }

        /// <summary>恢复处置语义；成功路径通常为空，例如 rolled-back-in-place、manual-recovery-required。</summary>
        public string RecoveryDisposition { get; set; }

        /// <summary>是否需要人工恢复。</summary>
        public bool ManualRecoveryRequired { get; set; }

        /// <summary>operator alert 级别；成功路径通常为空，例如 info、warn、error。</summary>
        public string OperatorAlertLevel { get; set; }

        /// <summary>operator alert 代码；成功路径通常为空，适合日志检索和 UI 展示。</summary>
        public string OperatorAlertCode { get; set; }

        /// <summary>operator alert 消息；成功路径通常为空。</summary>
        public string OperatorAlertMessage { get; set; }

        /// <summary>可选共享诊断字典；包含 strategy、attach、rollback、failure matrix 等结构化上下文。</summary>
        public Dictionary<string, object> Diagnostics { get; set; }
    }
}
