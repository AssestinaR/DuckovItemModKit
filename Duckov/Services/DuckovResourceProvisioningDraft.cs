using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using ItemModKit.Core;
using Newtonsoft.Json;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// Duckov 侧 durability/use-count 补建草案执行器。
    /// 第一版聚焦于状态初始化、草案变量持久化、脏标记与 load 回放。
    /// </summary>
    public static class DuckovResourceProvisioningDraft
    {
        /// <summary>默认持久化键。</summary>
        public const string DefaultPersistenceVariableKey = "IMK_Meta.ResourceProvisionDraft";

        private sealed class PersistedResourceProvisioningData
        {
            public int SchemaVersion { get; set; } = 1;
            public string Mode { get; set; }
            public float Current { get; set; }
            public float Maximum { get; set; }
            public float Loss { get; set; }
        }

        public static RichResult<EnsureResourceProvisionResult> EnsureProvision(EnsureResourceProvisionRequest request)
        {
            if (request == null)
            {
                return RichResult<EnsureResourceProvisionResult>.Fail(ErrorCode.InvalidArgument, "ensure-resource.request-null");
            }

            if (request.OwnerItem == null)
            {
                return RichResult<EnsureResourceProvisionResult>.Fail(ErrorCode.InvalidArgument, "ensure-resource.owner-null");
            }

            if (request.Definition == null)
            {
                return RichResult<EnsureResourceProvisionResult>.Fail(ErrorCode.InvalidArgument, "ensure-resource.definition-null");
            }

            if (request.Definition.Maximum <= 0f)
            {
                return RichResult<EnsureResourceProvisionResult>.Fail(ErrorCode.InvalidArgument, "ensure-resource.maximum-invalid");
            }

            if (request.Definition.Current < 0f || request.Definition.Current > request.Definition.Maximum)
            {
                return RichResult<EnsureResourceProvisionResult>.Fail(ErrorCode.InvalidArgument, "ensure-resource.current-out-of-range");
            }

            var diagnostics = new EnsureResourceProvisionDiagnostics
            {
                StartedAtUtc = DateTime.UtcNow,
            };
            var timings = new Dictionary<ResourceProvisioningPhase, Stopwatch>();

            try
            {
                StartPhase(ResourceProvisioningPhase.ResolveOwner, timings);
                var ownerItem = request.OwnerItem;
                CompletePhase(ResourceProvisioningPhase.ResolveOwner, diagnostics, timings);

                StartPhase(ResourceProvisioningPhase.MergeDefinition, timings);
                var hasExistingState = HasExistingState(ownerItem, request.Definition.Mode);
                diagnostics.Metadata["ExistingStateDetected"] = hasExistingState;
                diagnostics.Metadata["CallerTag"] = request.CallerTag ?? string.Empty;
                diagnostics.Metadata["PersistenceVariableKey"] = GetEffectiveKey(request);
                diagnostics.Metadata["Mode"] = request.Definition.Mode.ToString();
                if (hasExistingState && !request.Definition.OverwriteExisting)
                {
                    diagnostics.CompletedAtUtc = DateTime.UtcNow;
                    return RichResult<EnsureResourceProvisionResult>.Fail(ErrorCode.Conflict, "ensure-resource.state-already-exists");
                }

                CompletePhase(ResourceProvisioningPhase.MergeDefinition, diagnostics, timings);

                StartPhase(ResourceProvisioningPhase.ApplyRuntimeState, timings);
                var apply = ApplyDefinition(ownerItem, request.Definition);
                diagnostics.RuntimeStateApplied = apply.Ok;
                if (!apply.Ok)
                {
                    diagnostics.CompletedAtUtc = DateTime.UtcNow;
                    return RichResult<EnsureResourceProvisionResult>.Fail(apply.Code, string.IsNullOrEmpty(apply.Error) ? "ensure-resource.apply-failed" : apply.Error);
                }

                CompletePhase(ResourceProvisioningPhase.ApplyRuntimeState, diagnostics, timings);

                StartPhase(ResourceProvisioningPhase.PersistMetadata, timings);
                diagnostics.MetadataPersisted = !request.PersistDefinitionToVariables || TryPersistDefinition(ownerItem, request);
                if (!diagnostics.MetadataPersisted)
                {
                    diagnostics.CompletedAtUtc = DateTime.UtcNow;
                    return RichResult<EnsureResourceProvisionResult>.Fail(ErrorCode.OperationFailed, "ensure-resource.persist-failed");
                }

                CompletePhase(ResourceProvisioningPhase.PersistMetadata, diagnostics, timings);

                StartPhase(ResourceProvisioningPhase.RefreshRuntime, timings);
                var dirtyMarked = false;
                var persistenceFlushed = false;
                if (request.MarkDirty)
                {
                    using (IMKDuckov.AllowDirtyFromWriteService())
                    {
                        IMKDuckov.MarkDirty(ownerItem, DirtyKind.Variables, immediate: request.ForceFlushPersistence);
                        dirtyMarked = true;
                        if (request.ForceFlushPersistence)
                        {
                            IMKDuckov.FlushDirty(ownerItem, force: true);
                            persistenceFlushed = true;
                        }
                    }
                }

                if (request.RefreshUI)
                {
                    TryRefreshOwnerInventory(ownerItem);
                }

                if (request.PublishEvents)
                {
                    IMKDuckov.PublishItemChanged(ownerItem);
                }

                CompletePhase(ResourceProvisioningPhase.RefreshRuntime, diagnostics, timings);
                diagnostics.CompletedAtUtc = DateTime.UtcNow;

                return RichResult<EnsureResourceProvisionResult>.Success(new EnsureResourceProvisionResult
                {
                    FinalPhase = ResourceProvisioningPhase.Completed,
                    OwnerItem = ownerItem,
                    Mode = request.Definition.Mode,
                    RuntimeStateApplied = diagnostics.RuntimeStateApplied,
                    MetadataPersisted = diagnostics.MetadataPersisted,
                    DirtyMarked = dirtyMarked,
                    PersistenceFlushed = persistenceFlushed,
                    Diagnostics = diagnostics,
                });
            }
            catch (Exception ex)
            {
                diagnostics.Metadata["Exception"] = ex.ToString();
                diagnostics.CompletedAtUtc = DateTime.UtcNow;
                return RichResult<EnsureResourceProvisionResult>.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        public static bool HasPersistedDefinition(object ownerItem, string variableKey = null)
        {
            if (ownerItem == null)
            {
                return false;
            }

            try
            {
                var raw = IMKDuckov.Item.GetVariable(ownerItem, string.IsNullOrEmpty(variableKey) ? DefaultPersistenceVariableKey : variableKey) as string;
                return !string.IsNullOrEmpty(raw);
            }
            catch
            {
                return false;
            }
        }

        public static bool TryApplyPersistedDefinition(object ownerItem, string variableKey = null)
        {
            if (ownerItem == null)
            {
                return false;
            }

            var persisted = ReadPersistedDefinition(ownerItem, string.IsNullOrEmpty(variableKey) ? DefaultPersistenceVariableKey : variableKey);
            if (persisted == null)
            {
                return false;
            }

            if (!Enum.TryParse<ResourceProvisioningMode>(persisted.Mode ?? string.Empty, ignoreCase: true, out var mode))
            {
                return false;
            }

            var apply = EnsureProvision(new EnsureResourceProvisionRequest
            {
                OwnerItem = ownerItem,
                Definition = new ResourceProvisionDefinition
                {
                    Mode = mode,
                    Current = persisted.Current,
                    Maximum = persisted.Maximum,
                    Loss = persisted.Loss,
                    OverwriteExisting = true,
                },
                PersistDefinitionToVariables = false,
                PersistenceVariableKey = string.IsNullOrEmpty(variableKey) ? DefaultPersistenceVariableKey : variableKey,
                RefreshUI = false,
                PublishEvents = false,
                MarkDirty = false,
                ForceFlushPersistence = false,
                CallerTag = "ItemModKit.Persistence.ApplyPersistedResourceProvisioning",
            });
            return apply.Ok;
        }

        private static bool HasExistingState(object ownerItem, ResourceProvisioningMode mode)
        {
            if (mode == ResourceProvisioningMode.Durability)
            {
                var durability = IMKDuckov.Read.TryReadDurabilityInfo(ownerItem);
                return durability.Ok && durability.Value != null && (durability.Value.Max > 0f || durability.Value.Current > 0f || durability.Value.Loss > 0f);
            }

            var stack = IMKDuckov.Read.TryReadStackInfo(ownerItem);
            return stack.Ok && stack.Value != null && (stack.Value.Max > 1 || stack.Value.Count > 1);
        }

        private static RichResult ApplyDefinition(object ownerItem, ResourceProvisionDefinition definition)
        {
            if (definition.Mode == ResourceProvisioningMode.Durability)
            {
                var max = IMKDuckov.Write.TrySetMaxDurability(ownerItem, definition.Maximum);
                if (!max.Ok)
                {
                    return max;
                }

                var current = IMKDuckov.Write.TrySetDurability(ownerItem, definition.Current);
                if (!current.Ok)
                {
                    return current;
                }

                return IMKDuckov.Write.TrySetDurabilityLoss(ownerItem, definition.Loss);
            }

            var maxStack = IMKDuckov.Write.TrySetMaxStack(ownerItem, Math.Max(1, (int)Math.Round(definition.Maximum)));
            if (!maxStack.Ok)
            {
                return maxStack;
            }

            return IMKDuckov.Write.TrySetStackCount(ownerItem, Math.Max(0, (int)Math.Round(definition.Current)));
        }

        private static bool TryPersistDefinition(object ownerItem, EnsureResourceProvisionRequest request)
        {
            try
            {
                var payload = new PersistedResourceProvisioningData
                {
                    Mode = request.Definition.Mode.ToString(),
                    Current = request.Definition.Current,
                    Maximum = request.Definition.Maximum,
                    Loss = request.Definition.Loss,
                };

                var json = JsonConvert.SerializeObject(payload);
                return IMKDuckov.Write.TryWriteVariables(ownerItem, new[]
                {
                    new KeyValuePair<string, object>(GetEffectiveKey(request), json)
                }, overwrite: true).Ok;
            }
            catch
            {
                return false;
            }
        }

        private static PersistedResourceProvisioningData ReadPersistedDefinition(object ownerItem, string variableKey)
        {
            try
            {
                var raw = IMKDuckov.Item.GetVariable(ownerItem, variableKey) as string;
                if (string.IsNullOrEmpty(raw))
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<PersistedResourceProvisioningData>(raw);
            }
            catch
            {
                return null;
            }
        }

        private static string GetEffectiveKey(EnsureResourceProvisionRequest request)
        {
            return string.IsNullOrEmpty(request.PersistenceVariableKey)
                ? DefaultPersistenceVariableKey
                : request.PersistenceVariableKey;
        }

        private static void StartPhase(ResourceProvisioningPhase phase, Dictionary<ResourceProvisioningPhase, Stopwatch> timings)
        {
            var stopwatch = Stopwatch.StartNew();
            timings[phase] = stopwatch;
        }

        private static void CompletePhase(ResourceProvisioningPhase phase, EnsureResourceProvisionDiagnostics diagnostics, Dictionary<ResourceProvisioningPhase, Stopwatch> timings)
        {
            if (!timings.TryGetValue(phase, out var stopwatch))
            {
                return;
            }

            stopwatch.Stop();
            diagnostics.PhaseTimings[phase] = stopwatch.ElapsedMilliseconds;
        }

        private static void TryRefreshOwnerInventory(object ownerItem)
        {
            try
            {
                var inventory = ownerItem.GetType().GetProperty("InInventory", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(ownerItem, null);
                if (inventory != null)
                {
                    IMKDuckov.UIRefresh.RefreshInventory(inventory, markNeedInspection: true);
                }
            }
            catch
            {
            }
        }
    }
}