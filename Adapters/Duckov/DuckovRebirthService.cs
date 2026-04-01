using System;
using System.Collections.Generic;
using ItemModKit.Core;
using ItemModKit.Diagnostics;
using ItemStatsSystem;
using UnityEngine;
using static ItemModKit.Adapters.Duckov.DuckovTypeUtils;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 重生服务：根据旧物品与元数据生成新物品，并尽量保持原位置（背包或角色槽位）。
    /// - 若 keepLocation=true 且旧物品在背包：尝试原索引替换，失败时优先回滚旧树位置
    /// - 若 keepLocation=false：尝试插入角色槽位；失败时保留旧树，不完成替换
    /// 最后销毁旧物品，刷新相关背包，并立即持久化新物品的核心/变量/标签
    /// 兼容说明：支持对 IMK_MissingType 旧物品执行重生，并保留原有 IMK_ 标记变量。
    /// </summary>
    internal sealed class DuckovRebirthService : IRebirthService
    {
        private sealed class RebirthExecutionResult : RestoreExecutionResultBase
        {
            public RebirthAbortOutcome AbortOutcome { get; set; }
            public string FailureKind { get; set; }
            public RebirthFailureAction FailureAction { get; set; }
            public RebirthIntent IntentUsed { get; set; } = RebirthIntent.SafeReplace;
        }

        private enum RebirthAbortOutcome
        {
            None = 0,
            OldTreeStillInPlace = 1,
            RestoredInventoryIndex = 2,
            RestoredInventoryMerged = 3,
            RestoredSlot = 4,
            SentOldToPlayer = 5,
        }

        private enum RebirthFailureAction
        {
            None = 0,
            FailBeforeDetach = 1,
            RollbackAndFail = 2,
            PreserveOldTreeAndFail = 3,
        }

        private readonly IItemAdapter _item; private readonly IInventoryAdapter _inv; private readonly ISlotAdapter _slot; private readonly IItemPersistence _persist;
        /// <summary>构造函数：注入物品/背包/槽位/持久化适配器。</summary>
        public DuckovRebirthService(IItemAdapter item, IInventoryAdapter inv, ISlotAdapter slot, IItemPersistence persist) { _item = item; _inv = inv; _slot = slot; _persist = persist; }
        /// <summary>
        /// 用指定元数据替换旧物品并生成新物品（若 meta 为空则从旧物品推导）。
        /// keepLocation 控制是否尝试保持原位置。
        /// </summary>
        public RichResult<object> ReplaceRebirth(object oldItem, ItemMeta meta, bool keepLocation = true)
        {
            var detailed = ReplaceRebirthDetailed(oldItem, meta, keepLocation, RebirthIntent.SafeReplace);
            if (!detailed.Ok || detailed.Value == null || detailed.Value.RootItem == null)
            {
                return RichResult<object>.Fail(detailed.Code, detailed.Error);
            }

            return RichResult<object>.Success(detailed.Value.RootItem);
        }

        internal RebirthRestoreResult ReplaceRebirthReport(object oldItem, ItemMeta meta, bool keepLocation = true)
        {
            return ReplaceRebirthReport(oldItem, meta, keepLocation, RebirthIntent.SafeReplace);
        }

        internal RebirthRestoreResult ReplaceRebirthReport(object oldItem, ItemMeta meta, bool keepLocation, RebirthIntent intent)
        {
            try
            {
                var metaToApply = EnsureMetaFromObject(meta, oldItem);
                var replace = ExecuteReplacement(oldItem, metaToApply, keepLocation, intent);
                if (!replace.Succeeded || replace.RootItem == null)
                {
                    var failureMessage = BuildFailureMessage(replace);
                    EmitOperatorAlert(replace);
                    var failureReport = CreateRebirthRestoreResult(replace, failureMessage);
                    IMKRebirthReports.Record(failureReport);
                    return failureReport;
                }

                DestroyOldItem(oldItem);
                TryRefreshInventories();
                try { IMKDuckov.MarkDirty(replace.RootItem, DirtyKind.Core | DirtyKind.Tags | DirtyKind.Variables, immediate: true); IMKDuckov.FlushDirty(replace.RootItem, force: true); } catch { }
                var successReport = CreateRebirthRestoreResult(replace, null);
                IMKRebirthReports.Record(successReport);
                return successReport;
            }
            catch (Exception ex)
            {
                Log.Error("ReplaceRebirth failed", ex);
                var failureReport = new RebirthRestoreResult
                {
                    ReportedAtUtc = DateTime.UtcNow,
                    Succeeded = false,
                    ErrorCode = ErrorCode.OperationFailed,
                    Error = ex.Message,
                    IntentUsed = intent,
                    StrategyUsed = "unknown",
                    Diagnostics = new Dictionary<string, object>
                    {
                        ["rebirth.intent"] = intent.ToString(),
                        ["strategy"] = "unknown",
                        ["attached"] = false,
                        ["targetResolved"] = false,
                    }
                };
                IMKRebirthReports.Record(failureReport);
                return failureReport;
            }
        }

        internal RichResult<RebirthRestoreResult> ReplaceRebirthDetailed(object oldItem, ItemMeta meta, bool keepLocation = true)
        {
            return ReplaceRebirthDetailed(oldItem, meta, keepLocation, RebirthIntent.SafeReplace);
        }

        internal RichResult<RebirthRestoreResult> ReplaceRebirthDetailed(object oldItem, ItemMeta meta, bool keepLocation, RebirthIntent intent)
        {
            var report = ReplaceRebirthReport(oldItem, meta, keepLocation, intent);
            if (report == null || !report.Succeeded || report.RootItem == null)
            {
                return RichResult<RebirthRestoreResult>.Fail(report?.ErrorCode ?? ErrorCode.OperationFailed, report?.Error ?? "rebirth failed");
            }

            return RichResult<RebirthRestoreResult>.Success(report);
        }

        private RebirthExecutionResult ExecuteReplacement(object oldItem, ItemMeta meta, bool keepLocation, RebirthIntent intent)
        {
            var request = CreateRebirthRestoreRequest(oldItem, meta, keepLocation, intent);
            var detachedOldFromInventory = false;
            var detachedOldFromSlot = false;
            var originalInventory = request.TargetMode == RestoreTargetMode.AttachToExplicitInventory ? request.Target : null;
            var originalInventoryIndex = request.PreferredInventoryIndex;
            var originalSlotOwner = request.TargetMode == RestoreTargetMode.AttachToExplicitSlot ? request.Target : null;
            var originalSlotKey = request.TargetMode == RestoreTargetMode.AttachToExplicitSlot ? request.PreferredSlotKey : null;
            var result = new RebirthExecutionResult
            {
                Succeeded = false,
                TargetMode = request.TargetMode,
                AbortOutcome = RebirthAbortOutcome.None,
                ErrorCode = ErrorCode.OperationFailed,
                IntentUsed = intent,
            };
            request.DiagnosticsFinalized = (diagnostics, _) => result.Diagnostics = diagnostics;
            request.DiagnosticsMetadata["rebirth.keepLocation"] = keepLocation;
            request.DiagnosticsMetadata["rebirth.targetMode"] = request.TargetMode.ToString();
            request.DiagnosticsMetadata["rebirth.intent"] = intent.ToString();

            if (request.TargetMode == RestoreTargetMode.AttachToExplicitInventory)
            {
                try
                {
                    _inv.Detach(oldItem);
                    detachedOldFromInventory = true;
                }
                catch { }
            }
            else if (request.TargetMode == RestoreTargetMode.AttachToExplicitSlot)
            {
                var unplug = IMKDuckov.Write.TryUnplugFromSlot(originalSlotOwner, originalSlotKey);
                if (!unplug.Ok)
                {
                    result.ApplyFailure(unplug.Code, unplug.Error ?? "rebirth slot unplug failed", result.Diagnostics, RestorePhase.None);
                    result.FailureKind = "pre-detach-failure";
                    ApplyFailurePolicy(result, oldItem, null, detachedOldFromInventory, originalInventory, originalInventoryIndex, detachedOldFromSlot, originalSlotOwner, originalSlotKey);
                    return result;
                }

                detachedOldFromSlot = true;
            }

            var restore = DuckovTreeRestoreOrchestrator.Shared.Execute(request);
            if (!restore.Ok || restore.Value == null || restore.Value.RootItem == null)
            {
                result.ApplyFailure(restore.Code, restore.Error ?? "rebirth restore failed", result.Diagnostics);
                ApplyFailurePolicy(result, oldItem, null, detachedOldFromInventory, originalInventory, originalInventoryIndex, detachedOldFromSlot, originalSlotOwner, originalSlotKey);
                return result;
            }

            if (!restore.Value.Attached)
            {
                result.ApplyFailure(ErrorCode.OperationFailed, "rebirth attach failed", restore.Value.Diagnostics ?? result.Diagnostics, restore.Value.FinalPhase);
                ApplyFailurePolicy(result, oldItem, restore.Value.RootItem, detachedOldFromInventory, originalInventory, originalInventoryIndex, detachedOldFromSlot, originalSlotOwner, originalSlotKey);
                return result;
            }

            result.ApplyRestoreSuccess(restore.Value, result.Diagnostics);
            return result;
        }

        private string BuildFailureMessage(RebirthExecutionResult result)
        {
            if (result == null)
            {
                return "rebirth failed";
            }

            var target = result != null ? result.TargetMode.ToString() : RestoreTargetMode.DetachedTree.ToString();
            var rollback = DescribeAbortOutcome(result != null ? result.AbortOutcome : RebirthAbortOutcome.None);
            var attachState = result.ResolveAttachStateLabel();
            var attachOutcome = ResolveDiagnosticsString(result?.Diagnostics, "attachOutcome");
            var failureKind = ResolveFailureKind(result);
            var failureMatrixKey = ResolveFailureMatrixKey(result);
            var policyDecision = ResolvePolicyDecision(result);
            var matrixPolicyKey = ResolveMatrixPolicyKey(result);
            var recoveryDisposition = ResolveRecoveryDisposition(result);
            var message = result.BuildFailureMessage(
                new KeyValuePair<string, string>("target", target),
                new KeyValuePair<string, string>("rollback", rollback),
                new KeyValuePair<string, string>("action", DescribeFailureAction(result != null ? result.FailureAction : RebirthFailureAction.None)),
                new KeyValuePair<string, string>("attachState", attachState),
                new KeyValuePair<string, string>("attachOutcome", attachOutcome ?? string.Empty),
                new KeyValuePair<string, string>("failureKind", failureKind ?? string.Empty),
                new KeyValuePair<string, string>("failureMatrix", failureMatrixKey ?? string.Empty),
                new KeyValuePair<string, string>("policy", policyDecision ?? string.Empty),
                new KeyValuePair<string, string>("matrixPolicy", matrixPolicyKey ?? string.Empty),
                new KeyValuePair<string, string>("recovery", recoveryDisposition ?? string.Empty),
                new KeyValuePair<string, string>("alertLevel", ResolveOperatorAlertLevel(result) ?? string.Empty),
                new KeyValuePair<string, string>("alertCode", ResolveOperatorAlertCode(result) ?? string.Empty));
            Log.Warn(message);
            return message;
        }

        private static RebirthRestoreResult CreateRebirthRestoreResult(RebirthExecutionResult result, string failureMessage)
        {
            var diagnostics = result?.Diagnostics;
            var details = new Dictionary<string, object>();
            if (diagnostics != null)
            {
                foreach (var pair in diagnostics.Metadata)
                {
                    details[pair.Key] = pair.Value;
                }
            }

            var targetResolved = diagnostics != null
                && diagnostics.Metadata.TryGetValue("targetResolved", out var resolvedObj)
                && resolvedObj is bool resolvedBool
                && resolvedBool;

            var rollbackOutcome = result != null && result.AbortOutcome != RebirthAbortOutcome.None
                ? DescribeAbortOutcome(result.AbortOutcome)
                : string.Empty;
            var failureKind = result != null && !result.Succeeded ? ResolveFailureKind(result) : string.Empty;
            var failureAction = result != null && !result.Succeeded ? DescribeFailureAction(result.FailureAction) : string.Empty;
            var failurePhase = result != null && !result.Succeeded ? ResolveFailurePhase(result) : string.Empty;
            var failureMatrixKey = result != null && !result.Succeeded ? ResolveFailureMatrixKey(result) : string.Empty;
            var policyDecision = result != null && !result.Succeeded ? ResolvePolicyDecision(result) : string.Empty;
            var matrixPolicyKey = result != null && !result.Succeeded ? ResolveMatrixPolicyKey(result) : string.Empty;
            var recoveryDisposition = result != null && !result.Succeeded ? ResolveRecoveryDisposition(result) : string.Empty;
            var operatorAlertLevel = result != null && !result.Succeeded ? ResolveOperatorAlertLevel(result) : string.Empty;
            var operatorAlertCode = result != null && !result.Succeeded ? ResolveOperatorAlertCode(result) : string.Empty;
            var operatorAlertMessage = result != null && !result.Succeeded ? BuildOperatorAlertMessage(result) : string.Empty;

            details["strategy"] = result?.ResolveStrategyLabel() ?? "unknown";
            details["rebirth.intent"] = (result?.IntentUsed ?? RebirthIntent.SafeReplace).ToString();
            details["attached"] = result?.Attached ?? false;
            details["targetResolved"] = targetResolved;
            details["errorCode"] = result?.ErrorCode ?? ErrorCode.OperationFailed;
            details["error"] = failureMessage ?? string.Empty;
            details["rollbackOutcome"] = rollbackOutcome;
            details["failureKind"] = failureKind;
            details["failureAction"] = failureAction;
            details["failurePhase"] = failurePhase;
            details["failureMatrixKey"] = failureMatrixKey;
            details["policyDecision"] = policyDecision;
            details["matrixPolicyKey"] = matrixPolicyKey;
            details["recoveryDisposition"] = recoveryDisposition;
            details["manualRecoveryRequired"] = result != null && !result.Succeeded && IsManualRecoveryRequired(result);
            details["operatorAlertLevel"] = operatorAlertLevel;
            details["operatorAlertCode"] = operatorAlertCode;
            details["operatorAlertMessage"] = operatorAlertMessage;

            return new RebirthRestoreResult
            {
                ReportedAtUtc = DateTime.UtcNow,
                Succeeded = result?.Succeeded ?? false,
                ErrorCode = result?.Succeeded ?? false ? ErrorCode.None : result?.ErrorCode ?? ErrorCode.OperationFailed,
                Error = failureMessage,
                RootItem = result?.RootItem,
                Attached = result?.Attached ?? false,
                TargetResolved = targetResolved,
                AttachedIndex = result?.AttachedIndex ?? -1,
                StrategyUsed = result?.ResolveStrategyLabel() ?? "unknown",
                IntentUsed = result?.IntentUsed ?? ResolveIntentUsed(details),
                RollbackOutcome = rollbackOutcome,
                FailureKind = failureKind,
                FailureAction = failureAction,
                FailurePhase = failurePhase,
                FailureMatrixKey = failureMatrixKey,
                PolicyDecision = policyDecision,
                MatrixPolicyKey = matrixPolicyKey,
                RecoveryDisposition = recoveryDisposition,
                ManualRecoveryRequired = result != null && !result.Succeeded && IsManualRecoveryRequired(result),
                OperatorAlertLevel = operatorAlertLevel,
                OperatorAlertCode = operatorAlertCode,
                OperatorAlertMessage = operatorAlertMessage,
                Diagnostics = details,
            };
        }

        private static RebirthIntent ResolveIntentUsed(Dictionary<string, object> diagnostics)
        {
            if (diagnostics != null
                && diagnostics.TryGetValue("rebirth.intent", out var intentObj)
                && intentObj != null
                && Enum.TryParse(Convert.ToString(intentObj), out RebirthIntent intent))
            {
                return intent;
            }

            return RebirthIntent.SafeReplace;
        }

        private RebirthAbortOutcome AbortReplacement(object oldItem, object newItemObj, bool detachedOldFromInventory, object originalInventory, int originalInventoryIndex, bool detachedOldFromSlot, object originalSlotOwner, string originalSlotKey)
        {
            DestroyItem(newItemObj);

            if (detachedOldFromInventory)
            {
                var restored = TryRestoreOldInventoryPlacement(oldItem, originalInventory, originalInventoryIndex);
                TryRefreshInventories();
                return restored;
            }

            if (detachedOldFromSlot)
            {
                var restored = TryRestoreOldSlotPlacement(oldItem, originalSlotOwner, originalSlotKey);
                TryRefreshInventories();
                return restored;
            }

            TryRefreshInventories();
            return RebirthAbortOutcome.OldTreeStillInPlace;
        }

        private RebirthAbortOutcome TryRestoreOldInventoryPlacement(object oldItem, object inventory, int preferredInventoryIndex)
        {
            if (oldItem == null) return RebirthAbortOutcome.None;

            try
            {
                if (inventory != null && preferredInventoryIndex >= 0 && _inv.AddAt(inventory, oldItem, preferredInventoryIndex))
                {
                    return RebirthAbortOutcome.RestoredInventoryIndex;
                }
            }
            catch { }

            try
            {
                if (inventory != null && _inv.AddAndMerge(inventory, oldItem))
                {
                    return RebirthAbortOutcome.RestoredInventoryMerged;
                }
            }
            catch { }

            try { SendToPlayer(UnwrapToItem(oldItem)); return RebirthAbortOutcome.SentOldToPlayer; } catch { }
            return RebirthAbortOutcome.None;
        }

        private RebirthAbortOutcome TryRestoreOldSlotPlacement(object oldItem, object ownerItem, string slotKey)
        {
            if (oldItem == null || ownerItem == null || string.IsNullOrEmpty(slotKey))
            {
                try { SendToPlayer(UnwrapToItem(oldItem)); return RebirthAbortOutcome.SentOldToPlayer; } catch { }
                return RebirthAbortOutcome.None;
            }

            try
            {
                var plug = IMKDuckov.Write.TryPlugIntoSlot(ownerItem, slotKey, oldItem);
                if (plug.Ok)
                {
                    return RebirthAbortOutcome.RestoredSlot;
                }
            }
            catch { }

            try { SendToPlayer(UnwrapToItem(oldItem)); return RebirthAbortOutcome.SentOldToPlayer; } catch { }
            return RebirthAbortOutcome.None;
        }

        private static string DescribeAbortOutcome(RebirthAbortOutcome abort)
        {
            switch (abort)
            {
                case RebirthAbortOutcome.OldTreeStillInPlace:
                    return "old-tree-still-in-place";
                case RebirthAbortOutcome.RestoredInventoryIndex:
                    return "restored-inventory-index";
                case RebirthAbortOutcome.RestoredInventoryMerged:
                    return "restored-inventory-merged";
                case RebirthAbortOutcome.RestoredSlot:
                    return "restored-slot";
                case RebirthAbortOutcome.SentOldToPlayer:
                    return "sent-old-to-player";
                default:
                    return "unknown";
            }
        }

        private static string DescribeFailureAction(RebirthFailureAction action)
        {
            switch (action)
            {
                case RebirthFailureAction.FailBeforeDetach:
                    return "fail-before-detach";
                case RebirthFailureAction.RollbackAndFail:
                    return "rollback-and-fail";
                case RebirthFailureAction.PreserveOldTreeAndFail:
                    return "preserve-old-tree-and-fail";
                default:
                    return "none";
            }
        }

        private void ApplyFailurePolicy(RebirthExecutionResult result, object oldItem, object newItemObj, bool detachedOldFromInventory, object originalInventory, int originalInventoryIndex, bool detachedOldFromSlot, object originalSlotOwner, string originalSlotKey)
        {
            if (result == null) return;

            result.FailureKind = ResolveFailureKind(result);
            result.FailureAction = ResolveFailureAction(result, detachedOldFromInventory, detachedOldFromSlot);

            switch (result.FailureAction)
            {
                case RebirthFailureAction.FailBeforeDetach:
                    result.AbortOutcome = RebirthAbortOutcome.None;
                    break;

                case RebirthFailureAction.RollbackAndFail:
                    result.AbortOutcome = AbortReplacement(oldItem, newItemObj, detachedOldFromInventory, originalInventory, originalInventoryIndex, detachedOldFromSlot, originalSlotOwner, originalSlotKey);
                    break;

                case RebirthFailureAction.PreserveOldTreeAndFail:
                    DestroyItem(newItemObj);
                    result.AbortOutcome = RebirthAbortOutcome.OldTreeStillInPlace;
                    TryRefreshInventories();
                    break;
            }

            AttachFailureDiagnostics(result);
        }

        private static void AttachFailureDiagnostics(RebirthExecutionResult result)
        {
            result.FailureKind = ResolveFailureKind(result);
            result.ErrorCode = ResolveFailureCode(result);
            if (result?.Diagnostics == null) return;
            result.Diagnostics.Metadata["rebirth.rollbackOutcome"] = DescribeAbortOutcome(result.AbortOutcome);
            result.Diagnostics.Metadata["rebirth.rollbackPreservedOldTree"] = result.AbortOutcome != RebirthAbortOutcome.None;
            result.Diagnostics.Metadata["rebirth.failureKind"] = result.FailureKind ?? string.Empty;
            result.Diagnostics.Metadata["rebirth.failureAction"] = DescribeFailureAction(result.FailureAction);
            result.Diagnostics.Metadata["rebirth.failurePhase"] = ResolveFailurePhase(result);
            result.Diagnostics.Metadata["rebirth.failureMatrixKey"] = ResolveFailureMatrixKey(result);
            result.Diagnostics.Metadata["rebirth.policyDecision"] = ResolvePolicyDecision(result);
            result.Diagnostics.Metadata["rebirth.matrixPolicyKey"] = ResolveMatrixPolicyKey(result);
            result.Diagnostics.Metadata["rebirth.recoveryDisposition"] = ResolveRecoveryDisposition(result);
            result.Diagnostics.Metadata["rebirth.manualRecoveryRequired"] = IsManualRecoveryRequired(result);
            result.Diagnostics.Metadata["rebirth.operatorAlertLevel"] = ResolveOperatorAlertLevel(result);
            result.Diagnostics.Metadata["rebirth.operatorAlertCode"] = ResolveOperatorAlertCode(result);
            result.Diagnostics.Metadata["rebirth.operatorAlertMessage"] = BuildOperatorAlertMessage(result);
        }

        private static void EmitOperatorAlert(RebirthExecutionResult result)
        {
            var message = BuildOperatorAlertMessage(result);
            if (string.IsNullOrEmpty(message)) return;

            switch (ResolveOperatorAlertLevel(result))
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

        private static string ResolveDiagnosticsString(RestoreDiagnostics diagnostics, string key)
        {
            if (diagnostics == null || string.IsNullOrEmpty(key)) return string.Empty;
            return diagnostics.Metadata.TryGetValue(key, out var value)
                ? Convert.ToString(value) ?? string.Empty
                : string.Empty;
        }

        private static string ResolveFailureKind(RebirthExecutionResult result)
        {
            if (result == null) return "unknown";
            if (!string.IsNullOrEmpty(result.FailureKind)) return result.FailureKind;

            var attachOutcome = ResolveDiagnosticsString(result.Diagnostics, "attachOutcome");
            switch (attachOutcome)
            {
                case "target-unresolved":
                    return "attach-target-unresolved";
                case "deferred":
                case "deferred-via-fallback":
                    return "attach-deferred-not-accepted";
                case "attach-failed":
                case "attach-failed-via-fallback":
                    return "attach-failed";
                case "detached":
                    return "detached-restore-not-accepted";
            }

            if (result.FinalPhase == RestorePhase.None && (result.Error ?? string.Empty).IndexOf("unplug", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "pre-detach-failure";
            }

            if (result.FinalPhase == RestorePhase.Failed || (result.Error ?? string.Empty).IndexOf("restore", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "restore-failed";
            }

            return "operation-failed";
        }

        private static string ResolveFailurePhase(RebirthExecutionResult result)
        {
            if (result == null) return "unknown";
            if (result.FinalPhase == RestorePhase.None && string.Equals(result.FailureKind, "pre-detach-failure", StringComparison.Ordinal))
            {
                return "pre-detach";
            }

            switch (result.FinalPhase)
            {
                case RestorePhase.Instantiate:
                case RestorePhase.Hydrate:
                case RestorePhase.Connect:
                case RestorePhase.Failed:
                    return "restore";
                case RestorePhase.Attach:
                case RestorePhase.Finalize:
                case RestorePhase.Completed:
                    return "attach";
                default:
                    return "operation";
            }
        }

        private static string ResolveFailureMatrixKey(RebirthExecutionResult result)
        {
            if (result == null) return "unknown|unknown|unknown";
            var failureKind = ResolveFailureKind(result);
            var attachOutcome = ResolveDiagnosticsString(result.Diagnostics, "attachOutcome");
            var rollbackOutcome = DescribeAbortOutcome(result.AbortOutcome);
            return string.Concat(
                failureKind ?? "unknown",
                "|",
                string.IsNullOrEmpty(attachOutcome) ? "none" : attachOutcome,
                "|",
                rollbackOutcome ?? "unknown");
        }

        private static RebirthFailureAction ResolveFailureAction(RebirthExecutionResult result, bool detachedOldFromInventory, bool detachedOldFromSlot)
        {
            if (result == null) return RebirthFailureAction.PreserveOldTreeAndFail;

            if (string.Equals(ResolveFailureKind(result), "pre-detach-failure", StringComparison.Ordinal))
            {
                return RebirthFailureAction.FailBeforeDetach;
            }

            if (detachedOldFromInventory || detachedOldFromSlot)
            {
                return RebirthFailureAction.RollbackAndFail;
            }

            return RebirthFailureAction.PreserveOldTreeAndFail;
        }

        private static ErrorCode ResolveFailureCode(RebirthExecutionResult result)
        {
            var matrixPolicyKey = ResolveMatrixPolicyKey(result);
            if (string.Equals(matrixPolicyKey, "unresolved-rollback-sent-old-to-player", StringComparison.Ordinal)
                || string.Equals(matrixPolicyKey, "deferred-rollback-sent-old-to-player", StringComparison.Ordinal)
                || string.Equals(matrixPolicyKey, "attach-failed-rollback-sent-old-to-player", StringComparison.Ordinal))
            {
                return ErrorCode.Conflict;
            }

            switch (ResolveFailureKind(result))
            {
                case "attach-target-unresolved":
                    return ErrorCode.NotFound;
                case "attach-deferred-not-accepted":
                case "attach-failed":
                case "detached-restore-not-accepted":
                case "pre-detach-failure":
                    return ErrorCode.Conflict;
                case "restore-failed":
                    return result?.ErrorCode == ErrorCode.None ? ErrorCode.OperationFailed : result.ErrorCode;
                default:
                    return result?.ErrorCode == ErrorCode.None ? ErrorCode.OperationFailed : result.ErrorCode;
            }
        }

        private static string ResolvePolicyDecision(RebirthExecutionResult result)
        {
            if (result == null) return "fail";

            var matrixSpecific = ResolveMatrixSpecificPolicyDecision(result);
            if (!string.IsNullOrEmpty(matrixSpecific)) return matrixSpecific;

            switch (result.FailureAction)
            {
                case RebirthFailureAction.FailBeforeDetach:
                    return "fail-before-detach";
                case RebirthFailureAction.RollbackAndFail:
                    return ResolveFailureCode(result) == ErrorCode.NotFound
                        ? "rollback-and-fail-notfound"
                        : ResolveFailureCode(result) == ErrorCode.Conflict
                            ? "rollback-and-fail-conflict"
                            : "rollback-and-fail";
                case RebirthFailureAction.PreserveOldTreeAndFail:
                    return ResolveFailureCode(result) == ErrorCode.NotFound
                        ? "preserve-old-tree-and-fail-notfound"
                        : ResolveFailureCode(result) == ErrorCode.Conflict
                            ? "preserve-old-tree-and-fail-conflict"
                            : "preserve-old-tree-and-fail";
                default:
                    return "fail";
            }
        }

        private static string ResolveMatrixPolicyKey(RebirthExecutionResult result)
        {
            if (result == null) return "generic";
            return ResolveMatrixSpecificPolicyDecision(result) ?? "generic";
        }

        private static string ResolveRecoveryDisposition(RebirthExecutionResult result)
        {
            switch (ResolveMatrixPolicyKey(result))
            {
                case "unresolved-preserve-old-tree":
                case "deferred-preserve-old-tree":
                case "attach-failed-preserve-old-tree":
                    return "old-tree-preserved";
                case "unresolved-rollback-restored-index":
                case "deferred-rollback-restored-index":
                case "attach-failed-rollback-restored-index":
                case "unresolved-rollback-restored-slot":
                case "deferred-rollback-restored-slot":
                case "attach-failed-rollback-restored-slot":
                    return "rolled-back-in-place";
                case "unresolved-rollback-restored-merged":
                case "deferred-rollback-restored-merged":
                case "attach-failed-rollback-restored-merged":
                    return "rolled-back-relocated";
                case "unresolved-rollback-sent-old-to-player":
                case "deferred-rollback-sent-old-to-player":
                case "attach-failed-rollback-sent-old-to-player":
                    return "manual-recovery-required";
                default:
                    return result?.AbortOutcome == RebirthAbortOutcome.None ? "unknown" : "rolled-back";
            }
        }

        private static bool IsManualRecoveryRequired(RebirthExecutionResult result)
        {
            return string.Equals(ResolveRecoveryDisposition(result), "manual-recovery-required", StringComparison.Ordinal);
        }

        private static string ResolveOperatorAlertLevel(RebirthExecutionResult result)
        {
            switch (ResolveRecoveryDisposition(result))
            {
                case "manual-recovery-required":
                    return "error";
                case "rolled-back-relocated":
                case "old-tree-preserved":
                    return "warn";
                case "rolled-back-in-place":
                    return "info";
                default:
                    return "warn";
            }
        }

        private static string ResolveOperatorAlertCode(RebirthExecutionResult result)
        {
            switch (ResolveRecoveryDisposition(result))
            {
                case "manual-recovery-required":
                    return "rebirth.manual-recovery";
                case "rolled-back-relocated":
                    return "rebirth.rollback-relocated";
                case "rolled-back-in-place":
                    return "rebirth.rollback-in-place";
                case "old-tree-preserved":
                    return "rebirth.old-tree-preserved";
                default:
                    return "rebirth.failure";
            }
        }

        private static string BuildOperatorAlertMessage(RebirthExecutionResult result)
        {
            if (result == null || result.Succeeded)
            {
                return string.Empty;
            }

            var recoveryDisposition = ResolveRecoveryDisposition(result);
            var matrixPolicyKey = ResolveMatrixPolicyKey(result);
            var attachOutcome = ResolveDiagnosticsString(result.Diagnostics, "attachOutcome");
            var baseMessage = string.Concat(
                "[IMK.Rebirth.Alert] code=",
                ResolveOperatorAlertCode(result),
                " recovery=",
                string.IsNullOrEmpty(recoveryDisposition) ? "unknown" : recoveryDisposition,
                " matrix=",
                string.IsNullOrEmpty(matrixPolicyKey) ? "generic" : matrixPolicyKey,
                " attach=",
                string.IsNullOrEmpty(attachOutcome) ? "none" : attachOutcome);

            switch (recoveryDisposition)
            {
                case "manual-recovery-required":
                    return baseMessage + " action=inspect-player-inventory-or-slot-state";
                case "rolled-back-relocated":
                    return baseMessage + " action=verify-old-tree-relocated";
                case "rolled-back-in-place":
                    return baseMessage + " action=rollback-complete";
                case "old-tree-preserved":
                    return baseMessage + " action=old-tree-preserved";
                default:
                    return baseMessage + " action=review-rebirth-diagnostics";
            }
        }

        private static string ResolveMatrixSpecificPolicyDecision(RebirthExecutionResult result)
        {
            if (result == null) return null;

            var failureKind = ResolveFailureKind(result);
            var rollback = DescribeAbortOutcome(result.AbortOutcome);

            if (string.Equals(failureKind, "attach-target-unresolved", StringComparison.Ordinal))
            {
                switch (rollback)
                {
                    case "old-tree-still-in-place":
                        return "unresolved-preserve-old-tree";
                    case "restored-inventory-index":
                        return "unresolved-rollback-restored-index";
                    case "restored-inventory-merged":
                        return "unresolved-rollback-restored-merged";
                    case "restored-slot":
                        return "unresolved-rollback-restored-slot";
                    case "sent-old-to-player":
                        return "unresolved-rollback-sent-old-to-player";
                }
            }

            if (string.Equals(failureKind, "attach-deferred-not-accepted", StringComparison.Ordinal))
            {
                switch (rollback)
                {
                    case "old-tree-still-in-place":
                        return "deferred-preserve-old-tree";
                    case "restored-inventory-index":
                        return "deferred-rollback-restored-index";
                    case "restored-inventory-merged":
                        return "deferred-rollback-restored-merged";
                    case "restored-slot":
                        return "deferred-rollback-restored-slot";
                    case "sent-old-to-player":
                        return "deferred-rollback-sent-old-to-player";
                }
            }

            if (string.Equals(failureKind, "attach-failed", StringComparison.Ordinal))
            {
                switch (rollback)
                {
                    case "old-tree-still-in-place":
                        return "attach-failed-preserve-old-tree";
                    case "restored-inventory-index":
                        return "attach-failed-rollback-restored-index";
                    case "restored-inventory-merged":
                        return "attach-failed-rollback-restored-merged";
                    case "restored-slot":
                        return "attach-failed-rollback-restored-slot";
                    case "sent-old-to-player":
                        return "attach-failed-rollback-sent-old-to-player";
                }
            }

            return null;
        }

        private RestoreRequest CreateRebirthRestoreRequest(object oldItem, ItemMeta meta, bool keepLocation, RebirthIntent intent)
        {
            var targetMode = RestoreTargetMode.AttachToCharacter;
            object target = null;
            var preferredInventoryIndex = -1;
            var preferredSlotKey = default(string);

            if (keepLocation && _inv.IsInInventory(oldItem))
            {
                target = _inv.GetInventory(oldItem);
                preferredInventoryIndex = _inv.IndexOf(target, oldItem);
                targetMode = RestoreTargetMode.AttachToExplicitInventory;
            }
            else if (keepLocation && TryGetCurrentSlotPlacement(oldItem, out var slotOwner, out var slotKey))
            {
                target = slotOwner;
                preferredSlotKey = slotKey;
                targetMode = RestoreTargetMode.AttachToExplicitSlot;
            }

            return new RestoreRequest
            {
                Source = oldItem,
                SourceKind = RestoreSourceKind.Rebirth,
                Target = target,
                TargetMode = targetMode,
                Strategy = CloneStrategy.Unity,
                VariableMergeMode = VariableMergeMode.None,
                CopyTags = false,
                AllowDegraded = true,
                PublishEvents = false,
                RefreshUI = false,
                MarkDirty = false,
                DiagnosticsEnabled = false,
                CallerTag = "rebirth.replace",
                PreferredInventoryIndex = preferredInventoryIndex,
                PreferredCharacterSlotIndex = 0,
                PreferredSlotKey = preferredSlotKey,
                CustomInstantiate = () => CreateReplacementRoot(oldItem, meta),
                CustomHydrate = newItem => HydrateReplacementRoot(oldItem, newItem, meta, intent),
            };
        }

        private bool TryGetCurrentSlotPlacement(object oldItem, out object ownerItem, out string slotKey)
        {
            ownerItem = null;
            slotKey = null;
            try
            {
                var raw = UnwrapToItem(oldItem);
                if (!raw) return false;

                var slot = raw.GetType().GetProperty("PluggedIntoSlot", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(raw, null);
                if (slot == null) return false;

                ownerItem = raw.GetType().GetProperty("ParentItem", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(raw, null);
                slotKey = slot.GetType().GetProperty("Key", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(slot, null) as string;
                return ownerItem != null && !string.IsNullOrEmpty(slotKey);
            }
            catch
            {
                ownerItem = null;
                slotKey = null;
                return false;
            }
        }

        private RichResult<object> CreateReplacementRoot(object oldItem, ItemMeta meta)
        {
            int typeId = meta?.TypeId > 0 ? meta.TypeId : SafeTypeId(oldItem);
            var newItemObj = InstantiateReplacementRoot(oldItem, typeId);
            return newItemObj != null
                ? RichResult<object>.Success(newItemObj)
                : RichResult<object>.Fail(ErrorCode.OperationFailed, "instantiate failed");
        }

        private object InstantiateReplacementRoot(object oldItem, int typeId)
        {
            bool wasStub = false;
            try
            {
                var stubFlag = _item.GetVariable(oldItem, "IMK_MissingType");
                if (stubFlag is bool b && b) wasStub = true;
            }
            catch { }

            object newItemObj = null;
            if (wasStub)
            {
                var gen = IMKDuckov.Factory.TryGenerateByTypeId(typeId);
                if (gen.Ok) newItemObj = gen.Value;
            }
            if (newItemObj == null)
            {
                var instantiateSync = FindType("ItemStatsSystem.ItemAssetsCollection")?.GetMethod("InstantiateSync", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null, new[] { typeof(int) }, null);
                if (instantiateSync != null)
                {
                    try { newItemObj = instantiateSync.Invoke(null, new object[] { typeId }); } catch { }
                }
            }
            if (newItemObj == null)
            {
                var instRes = IMKDuckov.Factory.TryInstantiateByTypeId(typeId);
                if (instRes.Ok) newItemObj = instRes.Value;
            }
            return newItemObj;
        }

        private void HydrateReplacementRoot(object oldItem, object newItemObj, ItemMeta meta, RebirthIntent intent)
        {
            if (intent == RebirthIntent.SafeReplace)
            {
                _persist?.RecordMeta(newItemObj, meta, writeVariables: true);
            }
            else
            {
                ApplyCleanRebirthCoreFields(newItemObj, meta);
            }

            CopyPreservedStackState(oldItem, newItemObj);
            if (intent == RebirthIntent.SafeReplace)
            {
                CopyPreservedTags(oldItem, newItemObj);
                CopyPreservedVariables(oldItem, newItemObj, copyImkVariables: true, copyCustomVariables: true);
            }
            TrySet(newItemObj, EngineKeys.Property.Inspected, true);
        }

        private void ApplyCleanRebirthCoreFields(object newItemObj, ItemMeta meta)
        {
            if (newItemObj == null || meta == null)
            {
                return;
            }

            try
            {
                if (!string.IsNullOrEmpty(meta.NameKey))
                {
                    _item.SetDisplayNameRaw(newItemObj, meta.NameKey);
                    _item.SetName(newItemObj, meta.NameKey);
                }

                if (meta.TypeId > 0) _item.SetTypeId(newItemObj, meta.TypeId);
                if (meta.Quality > 0) _item.SetQuality(newItemObj, meta.Quality);
                if (meta.DisplayQuality > 0) _item.SetDisplayQuality(newItemObj, meta.DisplayQuality);
                if (meta.Value > 0) _item.SetValue(newItemObj, meta.Value);
            }
            catch { }
        }

        private static void CopyPreservedStackState(object oldItem, object newItem)
        {
            try
            {
                var stack = IMKDuckov.Read.TryReadStackInfo(oldItem);
                if (!stack.Ok || stack.Value == null) return;

                if (stack.Value.Max > 1)
                {
                    IMKDuckov.Write.TrySetMaxStack(newItem, stack.Value.Max);
                }

                if (stack.Value.Count > 0)
                {
                    IMKDuckov.Write.TrySetStackCount(newItem, stack.Value.Count);
                }
            }
            catch { }
        }

        private void CopyPreservedTags(object oldItem, object newItem)
        {
            try
            {
                var tags = _item.GetTags(oldItem);
                if (tags != null && tags.Length > 0) _item.SetTags(newItem, tags);
            }
            catch { }
        }

        private void CopyPreservedVariables(object oldItem, object newItem, bool copyImkVariables, bool copyCustomVariables)
        {
            try
            {
                foreach (var v in _item.GetVariables(oldItem) ?? System.Array.Empty<VariableEntry>())
                {
                    if (string.IsNullOrEmpty(v.Key)) continue;
                    var isImk = v.Key.StartsWith("IMK_", System.StringComparison.Ordinal);
                    var isCustom = v.Key.StartsWith("Custom", System.StringComparison.Ordinal);
                    if ((copyImkVariables && isImk) || (copyCustomVariables && isCustom))
                    {
                        _item.SetVariable(newItem, v.Key, v.Value, true);
                    }
                }
            }
            catch { }
        }

        private static void DestroyOldItem(object oldItem)
        {
            DestroyItem(oldItem);
        }

        private static void DestroyItem(object item)
        {
            try
            {
                var oldComp = UnwrapToItem(item);
                if (oldComp) UnityEngine.Object.DestroyImmediate(oldComp.gameObject);
                else if (item is Component c) UnityEngine.Object.DestroyImmediate(c.gameObject);
                else if (item is GameObject go) UnityEngine.Object.DestroyImmediate(go);
            }
            catch { }
        }

        /// <summary>安全获取类型 ID（失败返回 0）。</summary>
        private static int SafeTypeId(object obj)
        {
            try { return IMKDuckov.Item.GetTypeId(obj); } catch { return 0; }
        }

        /// <summary>若未提供 meta，则从旧物品（嵌入/变量/直接读取）推导一个。</summary>
        private ItemMeta EnsureMetaFromObject(ItemMeta meta, object old)
        {
            try
            {
                if (meta != null) return meta;
                try { if (IMKDuckov.Persistence != null && IMKDuckov.Persistence.TryExtractMeta(old, out var m) && m != null) return m; } catch { }
                return new ItemMeta
                {
                    NameKey = IMKDuckov.Item.GetDisplayNameRaw(old) ?? IMKDuckov.Item.GetName(old),
                    RemarkKey = null,
                    TypeId = IMKDuckov.Item.GetTypeId(old),
                    Quality = IMKDuckov.Item.GetQuality(old),
                    DisplayQuality = IMKDuckov.Item.GetDisplayQuality(old),
                    Value = IMKDuckov.Item.GetValue(old),
                    OwnerId = null
                };
            }
            catch { return meta; }
        }

        /// <summary>将任意包装对象解包为 Item 组件。</summary>
        private static Item UnwrapToItem(object obj)
        {
            if (obj is Item it) return it;
            try
            {
                if (obj is Component c) return c.GetComponent<Item>();
                if (obj is GameObject go) return go.GetComponent<Item>();
                var t = obj?.GetType(); if (t == null) return null;
                var p = t.GetProperty("Item", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (p != null) { var v = p.GetValue(obj, null) as Item; if (v) return v; }
                var f = t.GetField("item", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null) { var v = f.GetValue(obj) as Item; if (v) return v; }
            }
            catch { }
            return null;
        }

        private static void TrySet(object obj, string prop, object val) { try { obj.GetType().GetProperty(prop, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(obj, val, null); } catch { } }
        /// <summary>将物品发送给玩家（根据可用重载匹配调用）。</summary>
        private static void SendToPlayer(Item item)
        {
            try
            {
                var util = FindType("TeamSoda.Duckov.Core.ItemUtilities") ?? FindType("ItemUtilities");
                var m = util?.GetMethod(EngineKeys.Method.SendToPlayer, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null, new[] { typeof(Item), typeof(bool), typeof(bool) }, null)
                    ?? util?.GetMethod(EngineKeys.Method.SendToPlayer, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null, new[] { typeof(Item) }, null);
                if (m == null) return; var ps = m.GetParameters(); if (ps.Length == 3) m.Invoke(null, new object[] { item, false, true }); else m.Invoke(null, new object[] { item });
            }
            catch { }
        }
        /// <summary>刷新主角背包与仓库的 UI。</summary>
        private static void TryRefreshInventories()
        {
            try
            {
                var cmcT = FindType("CharacterMainControl") ?? FindType("TeamSoda.Duckov.Core.CharacterMainControl");
                var main = cmcT?.GetProperty(EngineKeys.Property.Main, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null, null);
                var inv = main?.GetType().GetProperty(EngineKeys.Property.CharacterItem)?.GetValue(main, null)?.GetType().GetProperty(EngineKeys.Property.Inventory)?.GetValue(main.GetType().GetProperty(EngineKeys.Property.CharacterItem)?.GetValue(main, null), null);
                if (inv != null)
                {
                    try { var p = inv.GetType().GetProperty(EngineKeys.Property.NeedInspection); p?.SetValue(inv, true, null); } catch { }
                    try { var m = inv.GetType().GetMethod(EngineKeys.Method.Refresh); m?.Invoke(inv, null); } catch { }
                }
                var psT = FindType("TeamSoda.Duckov.Core.PlayerStorage") ?? FindType("PlayerStorage");
                var st = psT?.GetProperty(EngineKeys.Property.Inventory, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null, null);
                if (st != null)
                {
                    try { var p = st.GetType().GetProperty(EngineKeys.Property.NeedInspection); p?.SetValue(st, true, null); } catch { }
                    try { var m = st.GetType().GetMethod(EngineKeys.Method.Refresh); m?.Invoke(st, null); } catch { }
                }
            }
            catch { }
        }
    }
}
