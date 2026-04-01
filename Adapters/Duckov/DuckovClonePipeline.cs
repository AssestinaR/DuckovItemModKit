using System;
using System.Collections.Generic;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 克隆管线：支持 TreeData 与 Unity 两种克隆策略，随后可按需合并变量/复制标签并尝试放入背包。
    /// </summary>
    internal sealed class DuckovClonePipeline : IClonePipeline
    {
        private sealed class CloneExecutionResult : RestoreExecutionResultBase
        {
        }

        private static readonly ITreeRestoreOrchestrator s_restoreOrchestrator = DuckovTreeRestoreOrchestrator.Shared;

        /// <summary>
        /// 从源物品克隆一个副本，按策略完成克隆并尝试放入目标背包。
        /// </summary>
        /// <param name="source">源物品。</param>
        /// <param name="options">管线选项（可为 null）。</param>
        /// <returns>包含新物品、放置情况与诊断信息的结果。</returns>
        public RichResult<ClonePipelineResult> TryCloneToInventory(object source, ClonePipelineOptions options = null)
        {
            options = options ?? new ClonePipelineOptions();
            if (source == null) return RichResult<ClonePipelineResult>.Fail(ErrorCode.InvalidArgument, "source null");
            var execution = ExecuteClone(source, options);
            if (!execution.Succeeded || execution.RootItem == null)
            {
                return RichResult<ClonePipelineResult>.Fail(execution.ErrorCode, BuildCloneFailureMessage(execution));
            }

            var diag = options.Diagnostics ? BuildCloneDiagnostics(execution, options) : null;
            var res = new ClonePipelineResult
            {
                NewItem = execution.RootItem,
                Added = execution.Attached,
                Index = execution.AttachedIndex,
                StrategyUsed = execution.StrategyUsed,
                RestoreDiagnostics = execution.Diagnostics,
                Diagnostics = diag,
            };
            return RichResult<ClonePipelineResult>.Success(res);
        }

        private CloneExecutionResult ExecuteClone(object source, ClonePipelineOptions options)
        {
            RestoreDiagnostics diagnostics = null;
            var request = new RestoreRequest
            {
                Source = source,
                SourceKind = RestoreSourceKind.Clone,
                Target = options.Target,
                TargetMode = string.IsNullOrEmpty(options.Target) ? RestoreTargetMode.AttachToResolvedHost : RestoreTargetMode.AttachToResolvedHost,
                Strategy = options.Strategy,
                VariableMergeMode = options.VariableMerge,
                CopyTags = options.CopyTags,
                AllowDegraded = options.Strategy == CloneStrategy.Auto,
                PublishEvents = false,
                RefreshUI = options.RefreshUI,
                MarkDirty = false,
                DiagnosticsEnabled = options.Diagnostics,
                CallerTag = "clone",
                ResolvedTargetKey = options.Target,
                AcceptVariableKey = options.AcceptVariableKey,
            };
            request.DiagnosticsFinalized = (diag, _) => diagnostics = diag;
            request.DiagnosticsMetadata["clone.target"] = options.Target ?? string.Empty;
            request.DiagnosticsMetadata["clone.copyTags"] = options.CopyTags;
            request.DiagnosticsMetadata["clone.variableMerge"] = options.VariableMerge.ToString();
            request.DiagnosticsMetadata["clone.strategyRequested"] = options.Strategy.ToString();

            var restore = s_restoreOrchestrator.Execute(request);
            if (!restore.Ok || restore.Value == null)
            {
                var failed = new CloneExecutionResult();
                failed.ApplyFailure(restore.Code, restore.Error ?? "clone restore failed", diagnostics);
                return failed;
            }

            var succeeded = new CloneExecutionResult
            {
                TargetMode = request.TargetMode,
            };
            succeeded.ApplyRestoreSuccess(restore.Value, diagnostics);
            return succeeded;
        }

        private static Dictionary<string, object> BuildCloneDiagnostics(CloneExecutionResult execution, ClonePipelineOptions options)
        {
            var diag = new Dictionary<string, object>
            {
                ["strategy"] = execution.StrategyUsed,
                ["target"] = options.Target,
                ["added"] = execution.Attached,
                ["index"] = execution.AttachedIndex,
            };

            if (execution.Diagnostics != null)
            {
                diag["phase"] = execution.FinalPhase.ToString();
                diag["fallbackUsed"] = execution.Diagnostics.FallbackUsed;
                foreach (var pair in execution.Diagnostics.Metadata)
                {
                    diag[pair.Key] = pair.Value;
                }
            }

            try { diag["newTid"] = IMKDuckov.Item.GetTypeId(execution.RootItem); } catch { }
            try { diag["newName"] = IMKDuckov.Item.GetDisplayNameRaw(execution.RootItem) ?? IMKDuckov.Item.GetName(execution.RootItem); } catch { }
            return diag;
        }

        private static string BuildCloneFailureMessage(CloneExecutionResult execution)
        {
            if (execution == null)
            {
                return "clone restore failed";
            }

            var diagnostics = execution.Diagnostics;
            var attached = diagnostics != null && diagnostics.Metadata.TryGetValue("attached", out var attachedObj) && attachedObj is bool attachedBool && attachedBool;
            var target = diagnostics != null && diagnostics.Metadata.TryGetValue("clone.target", out var targetObj) ? Convert.ToString(targetObj) : string.Empty;
            return execution.BuildFailureMessage(
                new KeyValuePair<string, string>("attached", attached ? "true" : "false"),
                new KeyValuePair<string, string>("target", target ?? string.Empty));
        }
    }
}
