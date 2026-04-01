using System;
using System.Collections.Generic;
using System.Text;
using ItemModKit.Core;

namespace ItemModKit.Diagnostics
{
    /// <summary>
    /// 轻量重生报告缓冲：保存最近若干次替换结果，便于上层调试面板或外部诊断工具拉取。
    /// </summary>
    public static class IMKRebirthReports
    {
        // 缓冲队列的并发保护锁。
        private static readonly object s_gate = new object();

        // 最近报告的内存缓冲，按写入顺序保存。
        private static readonly List<RebirthRestoreResult> s_reports = new List<RebirthRestoreResult>();

        // 最大缓冲长度；超过时从最旧记录开始裁剪。
        private static int s_capacity = 32;

        /// <summary>
        /// 记录一条重生报告。
        /// 这里会先复制一份快照，避免外部后续继续修改传入对象。
        /// </summary>
        public static void Record(RebirthRestoreResult report)
        {
            if (report == null) return;

            lock (s_gate)
            {
                var snapshot = Clone(report);
                if (snapshot.ReportedAtUtc == default(DateTime)) snapshot.ReportedAtUtc = DateTime.UtcNow;
                s_reports.Add(snapshot);
                if (s_reports.Count > s_capacity)
                {
                    s_reports.RemoveRange(0, s_reports.Count - s_capacity);
                }
            }
        }

        /// <summary>
        /// 获取最近的重生报告；默认返回最近 20 条，按时间倒序。
        /// 返回的是复制后的快照数组，不会暴露内部缓冲的可变引用。
        /// </summary>
        public static RebirthRestoreResult[] SnapshotRecent(int maxCount = 20)
        {
            if (maxCount <= 0) return Array.Empty<RebirthRestoreResult>();

            lock (s_gate)
            {
                var take = Math.Min(maxCount, s_reports.Count);
                var result = new RebirthRestoreResult[take];
                for (int i = 0; i < take; i++)
                {
                    result[i] = Clone(s_reports[s_reports.Count - 1 - i]);
                }

                return result;
            }
        }

        /// <summary>
        /// 清空最近缓存的重生报告。
        /// </summary>
        public static void Clear()
        {
            lock (s_gate)
            {
                s_reports.Clear();
            }
        }

        /// <summary>
        /// 将最近的重生报告写入日志，便于在无调试 UI 时直接导出诊断信息。
        /// includeDiagnostics=true 时会附带完整 diagnostics 键值对，适合排障，不适合高频日志路径。
        /// </summary>
        public static void LogRecent(int maxCount = 10, bool includeDiagnostics = false)
        {
            var reports = SnapshotRecent(maxCount);
            if (reports.Length == 0)
            {
                Log.Info("[IMK.Rebirth.Report] no recent reports");
                return;
            }

            for (int i = 0; i < reports.Length; i++)
            {
                var report = reports[i];
                var message = FormatSummary(report, includeDiagnostics);
                switch ((report?.OperatorAlertLevel ?? string.Empty).ToLowerInvariant())
                {
                    case "error":
                        Log.Error(message);
                        break;
                    case "info":
                        Log.Info(message);
                        break;
                    default:
                        Log.Warn(message);
                        break;
                }
            }
        }

        /// <summary>把单条 report 格式化为一行日志摘要。</summary>
        private static string FormatSummary(RebirthRestoreResult report, bool includeDiagnostics)
        {
            if (report == null)
            {
                return "[IMK.Rebirth.Report] null-report";
            }

            var builder = new StringBuilder();
            builder.Append("[IMK.Rebirth.Report] ");
            builder.Append(report.Succeeded ? "success" : "failure");
            builder.Append(" ts=").Append(report.ReportedAtUtc == default(DateTime) ? "unknown" : report.ReportedAtUtc.ToString("O"));
            builder.Append(" strategy=").Append(report.StrategyUsed ?? "unknown");
            builder.Append(" attached=").Append(report.Attached ? "true" : "false");
            builder.Append(" targetResolved=").Append(report.TargetResolved ? "true" : "false");
            if (!string.IsNullOrEmpty(report.FailureKind)) builder.Append(" failureKind=").Append(report.FailureKind);
            if (!string.IsNullOrEmpty(report.MatrixPolicyKey)) builder.Append(" matrixPolicy=").Append(report.MatrixPolicyKey);
            if (!string.IsNullOrEmpty(report.RecoveryDisposition)) builder.Append(" recovery=").Append(report.RecoveryDisposition);
            if (!string.IsNullOrEmpty(report.OperatorAlertCode)) builder.Append(" alert=").Append(report.OperatorAlertCode);
            if (report.ErrorCode != ErrorCode.None) builder.Append(" errorCode=").Append(report.ErrorCode);
            if (!string.IsNullOrEmpty(report.Error)) builder.Append(" error=").Append(report.Error);

            if (includeDiagnostics && report.Diagnostics != null && report.Diagnostics.Count > 0)
            {
                builder.Append(" diagnostics=");
                var first = true;
                foreach (var pair in report.Diagnostics)
                {
                    if (!first) builder.Append(';');
                    first = false;
                    builder.Append(pair.Key).Append('=').Append(Convert.ToString(pair.Value) ?? string.Empty);
                }
            }

            return builder.ToString();
        }

        /// <summary>复制 report，确保缓冲区保存的是稳定快照而不是外部共享实例。</summary>
        private static RebirthRestoreResult Clone(RebirthRestoreResult report)
        {
            var diagnostics = new Dictionary<string, object>();
            if (report.Diagnostics != null)
            {
                foreach (var pair in report.Diagnostics)
                {
                    diagnostics[pair.Key] = pair.Value;
                }
            }

            return new RebirthRestoreResult
            {
                ReportedAtUtc = report.ReportedAtUtc,
                Succeeded = report.Succeeded,
                ErrorCode = report.ErrorCode,
                Error = report.Error,
                RootItem = report.RootItem,
                Attached = report.Attached,
                TargetResolved = report.TargetResolved,
                AttachedIndex = report.AttachedIndex,
                StrategyUsed = report.StrategyUsed,
                RollbackOutcome = report.RollbackOutcome,
                FailureKind = report.FailureKind,
                FailureAction = report.FailureAction,
                FailurePhase = report.FailurePhase,
                FailureMatrixKey = report.FailureMatrixKey,
                PolicyDecision = report.PolicyDecision,
                MatrixPolicyKey = report.MatrixPolicyKey,
                RecoveryDisposition = report.RecoveryDisposition,
                ManualRecoveryRequired = report.ManualRecoveryRequired,
                OperatorAlertLevel = report.OperatorAlertLevel,
                OperatorAlertCode = report.OperatorAlertCode,
                OperatorAlertMessage = report.OperatorAlertMessage,
                Diagnostics = diagnostics,
            };
        }
    }
}