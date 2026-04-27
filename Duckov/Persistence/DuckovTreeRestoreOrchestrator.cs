using System;
using System.Collections.Generic;
using System.Diagnostics;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    internal sealed class DuckovTreeRestoreOrchestrator : ITreeRestoreOrchestrator
    {
        internal static readonly ITreeRestoreOrchestrator Shared = new DuckovTreeRestoreOrchestrator();

        private static void RecordPhaseFailure(RestoreDiagnostics diagnostics, RestorePhase phase, string action, Exception ex)
        {
            diagnostics?.Metadata["failurePhase"] = phase.ToString();
            diagnostics?.Metadata["failureAction"] = action ?? string.Empty;
            diagnostics?.Metadata[$"phase.{phase}.exception"] = ex.ToString();
            Log.Error($"[IMK.Restore] {phase} failed at {action}", ex);
        }

        private static void RecordPhaseAuxFailure(RestoreDiagnostics diagnostics, RestorePhase phase, string action, Exception ex)
        {
            diagnostics?.Metadata[$"phase.{phase}.degraded.{action}"] = ex.Message;
            Log.Warn($"[IMK.Restore] {phase} degraded at {action}: {ex.GetType().Name}: {ex.Message}");
        }

        private static RichResult<RestoreResult> FailExecution(RestoreRequest request, RestoreDiagnostics diagnostics, RestoreResult result, string strategyUsed, RestorePhase phase, string error, bool attached = false, bool deferred = false, bool targetResolved = false, int attachedIndex = -1, bool targetRequested = false, bool requestedTargetResolved = false, bool fallbackTargetUsed = false)
        {
            result.FinalPhase = RestorePhase.Failed;
            FinalizeDiagnostics(diagnostics, result, strategyUsed, request, attached, deferred, targetResolved, attachedIndex, targetRequested, requestedTargetResolved, fallbackTargetUsed);
            NotifyDiagnosticsFinalized(request, diagnostics, result);
            return RichResult<RestoreResult>.Fail(ErrorCode.OperationFailed, error ?? "restore failed");
        }

        public RichResult<RestoreResult> Execute(RestoreRequest request)
        {
            if (request == null) return RichResult<RestoreResult>.Fail(ErrorCode.InvalidArgument, "restore request null");
            if (request.Source == null) return RichResult<RestoreResult>.Fail(ErrorCode.InvalidArgument, "restore source null");

            var diagnostics = request.DiagnosticsEnabled ? new RestoreDiagnostics { StartedAtUtc = DateTime.UtcNow } : null;
            var result = new RestoreResult
            {
                FinalPhase = RestorePhase.None,
                Diagnostics = diagnostics,
            };

            object newItem = null;
            string usedStrategy = null;
            string error = null;

            if (request.PreparedRoot != null)
            {
                newItem = request.PreparedRoot;
                usedStrategy = "PreparedRoot";
            }

            string customError = null;
            if (newItem == null && TryInstantiateViaCustomFactory(request, diagnostics, out newItem, out customError))
            {
                usedStrategy = "CustomFactory";
            }
            else if (newItem == null && !string.IsNullOrEmpty(customError))
            {
                error = customError;
            }

            if (newItem == null && TryInstantiatePersistenceRoot(request, diagnostics, out newItem))
            {
                usedStrategy = "PersistenceMeta";
            }

            if (newItem == null && TryCloneViaTreeData(request, diagnostics, out newItem))
            {
                usedStrategy = "TreeData";
            }
            else if (newItem == null && request.Strategy == CloneStrategy.TreeData)
            {
                error = "TreeData clone failed";
            }

            if (newItem == null && TryCloneViaUnity(request, diagnostics, out newItem))
            {
                usedStrategy = "Unity";
                if (diagnostics != null && request.Strategy == CloneStrategy.Auto) diagnostics.FallbackUsed = true;
            }
            else if (newItem == null && request.Strategy == CloneStrategy.Unity)
            {
                error = "Unity clone failed";
            }

            if (newItem == null)
            {
                result.FinalPhase = RestorePhase.Failed;
                FinalizeDiagnostics(diagnostics, result, usedStrategy, request, false, false, false, -1, false, false, false);
                NotifyDiagnosticsFinalized(request, diagnostics, result);
                return RichResult<RestoreResult>.Fail(ErrorCode.OperationFailed, error ?? "restore failed");
            }

            result.FinalPhase = RestorePhase.Hydrate;
            string hydrateFailure = null;
            try
            {
                TrackPhase(diagnostics, RestorePhase.Hydrate, () =>
                {
                    if (request.CustomHydrate != null)
                    {
                        try { request.CustomHydrate(newItem); }
                        catch (Exception ex)
                        {
                            hydrateFailure = "custom hydrate failed";
                            RecordPhaseFailure(diagnostics, RestorePhase.Hydrate, "customHydrate", ex);
                        }
                    }

                    if (request.SourceKind == RestoreSourceKind.Persistence && request.Source is ItemMeta meta)
                    {
                        try { IMKDuckov.Persistence.RecordMeta(newItem, meta, writeVariables: true); }
                        catch (Exception ex)
                        {
                            hydrateFailure = hydrateFailure ?? "persistence meta record failed";
                            RecordPhaseFailure(diagnostics, RestorePhase.Hydrate, "recordMeta", ex);
                        }

                        try { IMKDuckov.Persistence.EnsureApplied(newItem); }
                        catch (Exception ex)
                        {
                            hydrateFailure = hydrateFailure ?? "persistence ensure apply failed";
                            RecordPhaseFailure(diagnostics, RestorePhase.Hydrate, "ensureApplied", ex);
                        }

                        return;
                    }

                    if (request.VariableMergeMode != VariableMergeMode.None)
                    {
                        try { IMKDuckov.VariableMerge.Merge(request.Source, newItem, request.VariableMergeMode, acceptKey: request.AcceptVariableKey); }
                        catch (Exception ex)
                        {
                            hydrateFailure = hydrateFailure ?? "variable merge failed";
                            RecordPhaseFailure(diagnostics, RestorePhase.Hydrate, "variableMerge", ex);
                        }
                    }
                    if (request.CopyTags)
                    {
                        try
                        {
                            var tags = IMKDuckov.Item.GetTags(request.Source) ?? Array.Empty<string>();
                            if (tags.Length > 0) IMKDuckov.Write.TryWriteTags(newItem, tags, merge: true);
                        }
                        catch (Exception ex)
                        {
                            hydrateFailure = hydrateFailure ?? "copy tags failed";
                            RecordPhaseFailure(diagnostics, RestorePhase.Hydrate, "copyTags", ex);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                RecordPhaseFailure(diagnostics, RestorePhase.Hydrate, "unhandled", ex);
                hydrateFailure = hydrateFailure ?? "hydrate phase failed";
            }

            if (!string.IsNullOrEmpty(hydrateFailure))
            {
                return FailExecution(request, diagnostics, result, usedStrategy, RestorePhase.Hydrate, hydrateFailure);
            }

            result.FinalPhase = RestorePhase.Attach;
            var attached = false;
            var attachedIndex = -1;
            var deferred = false;
            object inventory = null;
            var targetRequested = false;
            var requestedTargetResolved = false;
            var fallbackTargetUsed = false;
            var attachTargetResolved = false;
            string attachFailure = null;
            try
            {
                TrackPhase(diagnostics, RestorePhase.Attach, () =>
                {
                    if (request.TargetMode == RestoreTargetMode.AttachToCharacter)
                    {
                        targetRequested = true;
                        requestedTargetResolved = true;
                        attachTargetResolved = true;
                        try
                        {
                            attached = IMKDuckov.Slot.TryPlugToCharacter(newItem, request.PreferredCharacterSlotIndex);
                            attachedIndex = attached ? request.PreferredCharacterSlotIndex : -1;
                        }
                        catch (Exception ex)
                        {
                            attached = false;
                            attachedIndex = -1;
                            attachFailure = "attach to character failed";
                            RecordPhaseFailure(diagnostics, RestorePhase.Attach, "attachToCharacter", ex);
                        }
                        return;
                    }

                    if (request.TargetMode == RestoreTargetMode.AttachToExplicitSlot)
                    {
                        targetRequested = true;
                        requestedTargetResolved = request.Target != null && !string.IsNullOrEmpty(request.PreferredSlotKey);
                        attachTargetResolved = requestedTargetResolved;
                        try
                        {
                            var plug = IMKDuckov.Write.TryPlugIntoSlot(request.Target, request.PreferredSlotKey, newItem);
                            attached = plug.Ok;
                        }
                        catch (Exception ex)
                        {
                            attached = false;
                            attachFailure = "attach to explicit slot failed";
                            RecordPhaseFailure(diagnostics, RestorePhase.Attach, "attachToExplicitSlot", ex);
                        }
                        return;
                    }

                    var targetInfo = ResolveTargetInventoryInfo(request);
                    inventory = targetInfo.inventory;
                    targetRequested = targetInfo.targetRequested;
                    requestedTargetResolved = targetInfo.requestedTargetResolved;
                    fallbackTargetUsed = targetInfo.fallbackTargetUsed;
                    attachTargetResolved = inventory != null;
                    if (inventory == null) return;

                    try
                    {
                        if (request.PreferredInventoryIndex >= 0)
                        {
                            attached = IMKDuckov.Inventory.AddAt(inventory, newItem, request.PreferredInventoryIndex);
                            if (attached)
                            {
                                attachedIndex = request.PreferredInventoryIndex;
                            }
                        }

                        if (!attached)
                        {
                            var placement = IMKDuckov.InventoryPlacement.TryPlace(inventory, newItem, allowMerge: true, enableDeferredRetry: true);
                            attached = placement.added;
                            attachedIndex = placement.index;
                            deferred = placement.deferredScheduled;
                        }
                    }
                    catch (Exception ex)
                    {
                        attachFailure = "inventory attach failed";
                        RecordPhaseFailure(diagnostics, RestorePhase.Attach, "attachToInventory", ex);
                    }

                    if (request.RefreshUI)
                    {
                        try { IMKDuckov.UIRefresh.RefreshInventory(inventory); }
                        catch (Exception ex) { RecordPhaseAuxFailure(diagnostics, RestorePhase.Attach, "refreshUI", ex); }
                    }
                });
            }
            catch (Exception ex)
            {
                RecordPhaseFailure(diagnostics, RestorePhase.Attach, "unhandled", ex);
                attachFailure = attachFailure ?? "attach phase failed";
            }

            if (!string.IsNullOrEmpty(attachFailure))
            {
                return FailExecution(request, diagnostics, result, usedStrategy, RestorePhase.Attach, attachFailure, attached, deferred, attachTargetResolved, attachedIndex, targetRequested, requestedTargetResolved, fallbackTargetUsed);
            }

            result.FinalPhase = RestorePhase.Finalize;
            TrackPhase(diagnostics, RestorePhase.Finalize, () =>
            {
                try
                {
                    var oldHandle = ItemModKit.Adapters.Duckov.Locator.DuckovHandleFactory.CreateItemHandle(request.Source);
                    var newHandle = ItemModKit.Adapters.Duckov.Locator.DuckovHandleFactory.CreateItemHandle(newItem);
                    IMKDuckov.LogicalIds.Bind(oldHandle, newHandle);
                    IMKDuckov.RegisterHandle(newHandle);
                }
                catch (Exception ex) { RecordPhaseAuxFailure(diagnostics, RestorePhase.Finalize, "bindLogicalIds", ex); }
            });

            result.RootItem = newItem;
            result.TreeRestored = true;
            result.Attached = attached;
            result.DeferredScheduled = deferred;
            result.AttachedIndex = attachedIndex;
            result.StrategyUsed = usedStrategy;
            result.FinalPhase = RestorePhase.Completed;

            FinalizeDiagnostics(diagnostics, result, usedStrategy, request, attached, deferred, attachTargetResolved, attachedIndex, targetRequested, requestedTargetResolved, fallbackTargetUsed);
            NotifyDiagnosticsFinalized(request, diagnostics, result);
            return RichResult<RestoreResult>.Success(result);
        }

        private static bool TryCloneViaTreeData(RestoreRequest request, RestoreDiagnostics diagnostics, out object newItem)
        {
            newItem = null;
            if (request.SourceKind == RestoreSourceKind.Persistence) return false;
            if (request.Strategy != CloneStrategy.TreeData && request.Strategy != CloneStrategy.Auto) return false;

            object created = null;

            TrackPhase(diagnostics, RestorePhase.Instantiate, () =>
            {
                var clone = DuckovTreeDataService.TryInstantiateTreeFromSource(request.Source);
                if (clone.Ok && clone.Value != null) created = clone.Value;
            });

            newItem = created;
            return newItem != null;
        }

        private static bool TryInstantiateViaCustomFactory(RestoreRequest request, RestoreDiagnostics diagnostics, out object newItem, out string error)
        {
            newItem = null;
            error = null;
            if (request.CustomInstantiate == null) return false;

            object createdItem = null;
            string createdError = null;

            TrackPhase(diagnostics, RestorePhase.Instantiate, () =>
            {
                var created = request.CustomInstantiate();
                if (created.Ok && created.Value != null)
                {
                    createdItem = created.Value;
                }
                else
                {
                    createdError = created.Error ?? "custom instantiate failed";
                }
            });

            newItem = createdItem;
            error = createdError;
            return newItem != null;
        }

        private static bool TryCloneViaUnity(RestoreRequest request, RestoreDiagnostics diagnostics, out object newItem)
        {
            newItem = null;
            if (request.SourceKind == RestoreSourceKind.Persistence) return false;
            if (request.Strategy != CloneStrategy.Unity && request.Strategy != CloneStrategy.Auto) return false;

            object created = null;

            TrackPhase(diagnostics, RestorePhase.Instantiate, () =>
            {
                var clone = IMKDuckov.Factory.TryCloneItem(request.Source);
                if (clone.Ok && clone.Value != null) created = clone.Value;
            });

            newItem = created;
            return newItem != null;
        }

        private static bool TryInstantiatePersistenceRoot(RestoreRequest request, RestoreDiagnostics diagnostics, out object newItem)
        {
            newItem = null;
            if (request.SourceKind != RestoreSourceKind.Persistence) return false;
            if (!(request.Source is ItemMeta meta) || meta.TypeId <= 0) return false;
            if (request.Strategy != CloneStrategy.Unity && request.Strategy != CloneStrategy.Auto) return false;

            object created = null;

            TrackPhase(diagnostics, RestorePhase.Instantiate, () =>
            {
                var generated = IMKDuckov.Factory.TryGenerateByTypeId(meta.TypeId);
                if (generated.Ok && generated.Value != null)
                {
                    created = generated.Value;
                    return;
                }

                var instantiated = IMKDuckov.Factory.TryInstantiateByTypeId(meta.TypeId);
                if (instantiated.Ok && instantiated.Value != null)
                {
                    created = instantiated.Value;
                }
            });

            newItem = created;
            return newItem != null;
        }

        private static (object inventory, bool targetRequested, bool requestedTargetResolved, bool fallbackTargetUsed) ResolveTargetInventoryInfo(RestoreRequest request)
        {
            if (request.TargetMode == RestoreTargetMode.DetachedTree) return (null, false, false, false);
            if (request.Target != null && !(request.Target is string)) return (request.Target, false, true, false);

            var targetKey = request.Target as string ?? request.ResolvedTargetKey;
            var targetRequested = !string.IsNullOrEmpty(targetKey);
            var requested = IMKDuckov.InventoryResolver.Resolve(targetKey);
            if (requested != null) return (requested, targetRequested, true, false);

            var fallback = IMKDuckov.InventoryResolver.ResolveFallback();
            if (fallback != null) return (fallback, targetRequested, false, targetRequested);

            return (null, targetRequested, false, false);
        }

        private static void FinalizeDiagnostics(RestoreDiagnostics diagnostics, RestoreResult result, RestoreSourceKind sourceKind, string callerTag, bool attached, bool deferred, bool targetResolved, int attachedIndex)
        {
            if (diagnostics == null) return;
            diagnostics.CompletedAtUtc = DateTime.UtcNow;
            diagnostics.Metadata["sourceKind"] = sourceKind.ToString();
            diagnostics.Metadata["callerTag"] = callerTag ?? string.Empty;
            diagnostics.Metadata["attached"] = attached;
            diagnostics.Metadata["deferred"] = deferred;
            diagnostics.Metadata["targetResolved"] = targetResolved;
            diagnostics.Metadata["attachedIndex"] = attachedIndex;
            if (!string.IsNullOrEmpty(result.StrategyUsed)) diagnostics.StrategyUsed = result.StrategyUsed;
        }

        private static void FinalizeDiagnostics(RestoreDiagnostics diagnostics, RestoreResult result, string strategyUsed, RestoreRequest request, bool attached, bool deferred, bool targetResolved, int attachedIndex, bool targetRequested, bool requestedTargetResolved, bool fallbackTargetUsed)
        {
            if (result != null) result.StrategyUsed = strategyUsed;
            if (diagnostics != null)
            {
                diagnostics.StrategyUsed = strategyUsed;
                FinalizeDiagnostics(diagnostics, result, request.SourceKind, request.CallerTag, attached, deferred, targetResolved, attachedIndex);
                diagnostics.Metadata["targetRequested"] = targetRequested;
                diagnostics.Metadata["requestedTargetResolved"] = requestedTargetResolved;
                diagnostics.Metadata["fallbackTargetUsed"] = fallbackTargetUsed;
                diagnostics.Metadata["attachOutcome"] = ResolveAttachOutcome(request, attached, deferred, targetResolved, targetRequested, requestedTargetResolved, fallbackTargetUsed);
                if (request.DiagnosticsMetadata != null)
                {
                    foreach (var pair in request.DiagnosticsMetadata)
                    {
                        diagnostics.Metadata[pair.Key] = pair.Value;
                    }
                }
            }
        }

        private static void NotifyDiagnosticsFinalized(RestoreRequest request, RestoreDiagnostics diagnostics, RestoreResult result)
        {
            try { request?.DiagnosticsFinalized?.Invoke(diagnostics, result); }
            catch (Exception ex) { RecordPhaseAuxFailure(diagnostics, RestorePhase.Finalize, "diagnosticsFinalizedCallback", ex); }
        }

        private static string ResolveAttachOutcome(RestoreRequest request, bool attached, bool deferred, bool targetResolved, bool targetRequested, bool requestedTargetResolved, bool fallbackTargetUsed)
        {
            if (request == null) return "unknown";
            if (request.TargetMode == RestoreTargetMode.DetachedTree) return "detached";
            if (attached) return fallbackTargetUsed ? "attached-via-fallback" : "attached";
            if (deferred) return fallbackTargetUsed ? "deferred-via-fallback" : "deferred";
            if (targetRequested && !requestedTargetResolved && !fallbackTargetUsed) return "target-unresolved";
            if (targetRequested && !requestedTargetResolved && fallbackTargetUsed) return "attach-failed-via-fallback";
            if (!targetResolved) return "target-unresolved";
            return "attach-failed";
        }

        private static void TrackPhase(RestoreDiagnostics diagnostics, RestorePhase phase, Action action)
        {
            var stopwatch = diagnostics != null ? Stopwatch.StartNew() : null;
            action();
            if (stopwatch != null)
            {
                stopwatch.Stop();
                diagnostics.PhaseTimings[phase] = stopwatch.ElapsedMilliseconds;
            }
        }
    }
}