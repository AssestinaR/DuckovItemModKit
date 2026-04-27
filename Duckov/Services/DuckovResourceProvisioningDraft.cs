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
        /// <summary>
        /// 默认持久化键。
        /// 当调用方没有显式指定变量键时，资源补建草案会把 JSON 写到这个变量名下。
        /// </summary>
        public const string DefaultPersistenceVariableKey = "IMK_Meta.ResourceProvisionDraft";

        /// <summary>
        /// 资源补建草案的持久化载体。
        /// 这是写入变量 JSON 时使用的内部 DTO，用于在 load 回放时恢复相同的资源初始化定义。
        /// </summary>
        private sealed class PersistedResourceProvisioningData
        {
            /// <summary>当前内部持久化 schema 版本。</summary>
            public int SchemaVersion { get; set; } = 1;

            /// <summary>补建模式名称，对应 <see cref="ResourceProvisioningMode"/> 的字符串表示。</summary>
            public string Mode { get; set; }

            /// <summary>持久化的当前值。</summary>
            public float Current { get; set; }

            /// <summary>持久化的最大值。</summary>
            public float Maximum { get; set; }

            /// <summary>持久化的损耗值。</summary>
            public float Loss { get; set; }
        }

        /// <summary>
        /// 执行 durability / use-count 补建草案请求。
        /// </summary>
        /// <param name="request">资源补建请求对象，负责描述目标物品、目标资源状态以及是否刷新 UI / 写回持久化。</param>
        /// <returns>
        /// 成功时返回包含补建结果和 diagnostics 的 RichResult；
        /// 失败时返回错误码与错误信息，且 Value 为默认值。
        /// </returns>
        /// <remarks>
        /// 调用方最常见的使用方式是：先看 Ok，再看 Value.RuntimeStateApplied、Value.MetadataPersisted 和 Value.Diagnostics。
        /// </remarks>
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

        /// <summary>
        /// 判断目标物品上是否存在资源补建草案的持久化定义。
        /// </summary>
        /// <param name="ownerItem">目标物品。</param>
        /// <param name="variableKey">持久化变量键；为 null 时使用默认键。</param>
        /// <returns>存在非空草案 JSON 时返回 true；否则返回 false。</returns>
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

        /// <summary>
        /// 从资源补建草案变量回放 durability / use-count 定义。
        /// </summary>
        /// <param name="ownerItem">目标物品。</param>
        /// <param name="variableKey">持久化变量键；为 null 时使用默认键。</param>
        /// <returns>
        /// 成功读取、解析并应用持久化定义时返回 true；
        /// 若变量不存在、内容无效或应用失败，则返回 false。
        /// </returns>
        /// <remarks>
        /// 该入口更适合持久化恢复路径；若调用方需要更细的失败信息，应优先调用 EnsureProvision。
        /// </remarks>
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

        /// <summary>
        /// 判断目标物品当前是否已经具备同模式的资源状态。
        /// </summary>
        /// <param name="ownerItem">目标物品。</param>
        /// <param name="mode">要检查的资源模式。</param>
        /// <returns>检测到已有 durability 或 use-count 语义时返回 true；否则返回 false。</returns>
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

        /// <summary>
        /// 把资源定义真正应用到运行时物品对象上。
        /// </summary>
        /// <param name="ownerItem">目标物品。</param>
        /// <param name="definition">待应用的资源定义。</param>
        /// <returns>全部写入成功时返回成功结果；任一步失败时返回对应写服务错误。</returns>
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

        /// <summary>
        /// 把当前资源补建定义写入变量持久化草案。
        /// </summary>
        /// <param name="ownerItem">目标物品。</param>
        /// <param name="request">本次资源补建请求。</param>
        /// <returns>成功序列化并写入变量时返回 true；否则返回 false。</returns>
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

        /// <summary>
        /// 从变量中读取并反序列化资源补建草案。
        /// </summary>
        /// <param name="ownerItem">目标物品。</param>
        /// <param name="variableKey">持久化变量键。</param>
        /// <returns>成功解析时返回持久化 DTO；变量缺失、为空或解析失败时返回 null。</returns>
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

        /// <summary>
        /// 解析本次请求实际使用的持久化变量键。
        /// </summary>
        /// <param name="request">资源补建请求。</param>
        /// <returns>若请求未指定变量键则返回默认键；否则返回请求自定义键。</returns>
        private static string GetEffectiveKey(EnsureResourceProvisionRequest request)
        {
            return string.IsNullOrEmpty(request.PersistenceVariableKey)
                ? DefaultPersistenceVariableKey
                : request.PersistenceVariableKey;
        }

        /// <summary>
        /// 开始记录某个资源补建阶段的耗时。
        /// </summary>
        /// <param name="phase">当前阶段。</param>
        /// <param name="timings">阶段计时器字典。</param>
        private static void StartPhase(ResourceProvisioningPhase phase, Dictionary<ResourceProvisioningPhase, Stopwatch> timings)
        {
            var stopwatch = Stopwatch.StartNew();
            timings[phase] = stopwatch;
        }

        /// <summary>
        /// 结束某个资源补建阶段的耗时记录，并写入 diagnostics。
        /// </summary>
        /// <param name="phase">当前阶段。</param>
        /// <param name="diagnostics">资源补建共享诊断对象。</param>
        /// <param name="timings">阶段计时器字典。</param>
        private static void CompletePhase(ResourceProvisioningPhase phase, EnsureResourceProvisionDiagnostics diagnostics, Dictionary<ResourceProvisioningPhase, Stopwatch> timings)
        {
            if (!timings.TryGetValue(phase, out var stopwatch))
            {
                return;
            }

            stopwatch.Stop();
            diagnostics.PhaseTimings[phase] = stopwatch.ElapsedMilliseconds;
        }

        /// <summary>
        /// 尝试刷新目标物品所在背包的 UI 表现。
        /// </summary>
        /// <param name="ownerItem">目标物品。</param>
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