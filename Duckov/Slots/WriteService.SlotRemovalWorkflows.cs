using System;
using System.Collections.Generic;
using System.Reflection;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 写入服务（槽位/移除流程）：
    /// 负责动态槽位、原版槽位、批量槽位以及整套槽位系统的移除入口。
    /// </summary>
    internal sealed partial class WriteService : IWriteService
    {
        /// <summary>
        /// 移除宿主物品的整个槽位宿主组件。
        /// 该流程要求所有槽位都已为空，否则返回冲突错误。
        /// </summary>
        /// <param name="ownerItem">目标宿主物品。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryRemoveSlotHost(object ownerItem)
        {
            try
            {
                if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.owner");
                var slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (slots == null) return RichResult.Success();

                if (slots is System.Collections.IEnumerable enumerable)
                {
                    foreach (var slot in enumerable)
                    {
                        if (slot == null)
                        {
                            continue;
                        }

                        if (TryGetSlotContent(slot, out _))
                        {
                            return RichResult.Fail(ErrorCode.Conflict, "slot.host.remove_occupied");
                        }
                    }
                }

                try
                {
                    var clear = DuckovReflectionCache.GetMethod(slots.GetType(), "Clear", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    clear?.Invoke(slots, null);
                }
                catch
                {
                }

                InvalidateSlotCollectionCache(slots);

                var slotsField = DuckovReflectionCache.GetField(ownerItem.GetType(), "slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                slotsField?.SetValue(ownerItem, null);

                try
                {
                    IMKDuckov.Item.RemoveVariable(ownerItem, DuckovSlotProvisioningDraft.DefaultPersistenceVariableKey);
                }
                catch
                {
                }

                if (slots is UnityEngine.Object unityObject)
                {
                    try
                    {
                        UnityEngine.Object.DestroyImmediate(unityObject);
                    }
                    catch
                    {
                        try { UnityEngine.Object.Destroy(unityObject); } catch { }
                    }
                }

                NotifySlotAndChildChanged(ownerItem);
                TryRefreshOwnerInventory(ownerItem);
                IMKDuckov.PublishItemChanged(ownerItem);
                MarkDirtyFromWriteScope(ownerItem, DirtyKind.Slots | DirtyKind.Variables);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryRemoveSlotHost failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        /// <summary>
        /// 移除一个 IMK 动态新增槽位，并同步删除对应的持久化定义。
        /// </summary>
        /// <param name="ownerItem">槽位宿主物品。</param>
        /// <param name="slotKey">目标槽位键。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryRemoveDynamicSlot(object ownerItem, string slotKey)
        {
            try
            {
                if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.owner");
                if (string.IsNullOrWhiteSpace(slotKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.key");

                var payload = ReadSlotPersistenceDraftData(ownerItem) ?? new SlotPersistenceDraftData();
                var removedDefinition = payload.Slots.RemoveAll(entry => entry != null && string.Equals(entry.Key, slotKey, StringComparison.OrdinalIgnoreCase)) > 0;
                if (!removedDefinition)
                {
                    return RichResult.Fail(ErrorCode.NotFound, "slot.dynamic.notfound");
                }

                var relocate = TryRelocateSlotContentForRemoval(ownerItem, slotKey);
                if (!relocate.Ok)
                {
                    return relocate;
                }

                var slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (slots != null && ResolveSlot(slots, slotKey) != null)
                {
                    var remove = TryRemoveSlotCore(ownerItem, slotKey, markDirty: false);
                    if (!remove.Ok)
                    {
                        return remove;
                    }
                }

                payload.RemovedBuiltinSlotKeys.RemoveAll(key => string.Equals(key, slotKey, StringComparison.OrdinalIgnoreCase));
                payload.Mutations.RemoveAll(mutation => mutation != null && string.Equals(mutation.Key, slotKey, StringComparison.OrdinalIgnoreCase));
                if (!WriteSlotPersistenceDraftData(ownerItem, payload))
                {
                    return RichResult.Fail(ErrorCode.OperationFailed, "slot.dynamic.persist_remove_failed");
                }

                MarkDirtyFromWriteScope(ownerItem, DirtyKind.Slots | DirtyKind.Variables);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryRemoveDynamicSlot failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        /// <summary>
        /// 移除一个原版槽位，并在持久化草案中记录该槽位已被移除。
        /// </summary>
        /// <param name="ownerItem">槽位宿主物品。</param>
        /// <param name="slotKey">目标槽位键。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryRemoveBuiltinSlot(object ownerItem, string slotKey)
        {
            try
            {
                if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.owner");
                if (string.IsNullOrWhiteSpace(slotKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.key");

                var payload = ReadSlotPersistenceDraftData(ownerItem) ?? new SlotPersistenceDraftData();
                if (payload.Slots.Exists(entry => entry != null && string.Equals(entry.Key, slotKey, StringComparison.OrdinalIgnoreCase)))
                {
                    return RichResult.Fail(ErrorCode.Conflict, "slot.builtin.dynamic_conflict");
                }

                var slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (slots == null) return RichResult.Fail(ErrorCode.NotSupported, "slot.owner.no_slots");
                if (ResolveSlot(slots, slotKey) == null) return RichResult.Fail(ErrorCode.NotFound, "slot.builtin.notfound");

                var relocate = TryRelocateSlotContentForRemoval(ownerItem, slotKey);
                if (!relocate.Ok)
                {
                    return relocate;
                }

                var remove = TryRemoveSlotCore(ownerItem, slotKey, markDirty: false);
                if (!remove.Ok)
                {
                    return remove;
                }

                if (!payload.RemovedBuiltinSlotKeys.Exists(key => string.Equals(key, slotKey, StringComparison.OrdinalIgnoreCase)))
                {
                    payload.RemovedBuiltinSlotKeys.Add(slotKey);
                }

                payload.Mutations.RemoveAll(mutation => mutation != null && string.Equals(mutation.Key, slotKey, StringComparison.OrdinalIgnoreCase) && mutation.Kind == SlotPersistenceMutationKind.RemoveBuiltinSlot);
                if (!WriteSlotPersistenceDraftData(ownerItem, payload))
                {
                    return RichResult.Fail(ErrorCode.OperationFailed, "slot.builtin.persist_remove_failed");
                }

                MarkDirtyFromWriteScope(ownerItem, DirtyKind.Slots | DirtyKind.Variables);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryRemoveBuiltinSlot failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        /// <summary>
        /// 批量移除多个槽位。
        /// 该流程支持在单项失败后继续处理剩余槽位，但最终仍会汇总失败信息返回。
        /// </summary>
        /// <param name="ownerItem">槽位宿主物品。</param>
        /// <param name="slotKeys">待移除的槽位键集合。</param>
        /// <param name="continueOnError">单项失败后是否继续处理后续槽位。</param>
        /// <returns>全部成功时返回成功结果；否则返回汇总后的失败结果。</returns>
        public RichResult TryRemoveSlots(object ownerItem, string[] slotKeys, bool continueOnError = false)
        {
            try
            {
                if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.owner");

                var normalizedKeys = NormalizeSlotKeys(slotKeys);
                if (normalizedKeys.Count == 0)
                {
                    return RichResult.Fail(ErrorCode.InvalidArgument, "slot.batch.no_keys");
                }

                var failures = new List<string>();
                foreach (var slotKey in normalizedKeys)
                {
                    var remove = TryRemoveSlot(ownerItem, slotKey);
                    if (remove.Ok)
                    {
                        continue;
                    }

                    failures.Add(slotKey + ":" + (string.IsNullOrEmpty(remove.Error) ? remove.Code.ToString() : remove.Error));
                    if (!continueOnError)
                    {
                        return RichResult.Fail(remove.Code == ErrorCode.None ? ErrorCode.OperationFailed : remove.Code, "slot.batch.remove_failed:" + failures[0]);
                    }
                }

                if (failures.Count > 0)
                {
                    return RichResult.Fail(ErrorCode.OperationFailed, "slot.batch.remove_failed:" + string.Join("|", failures));
                }

                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryRemoveSlots failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        /// <summary>
        /// 移除整个槽位系统。
        /// 该流程会先批量移除全部槽位，再移除槽位宿主与相关持久化键。
        /// </summary>
        /// <param name="ownerItem">槽位宿主物品。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryRemoveSlotSystem(object ownerItem)
        {
            try
            {
                if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.owner");

                var slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (slots != null)
                {
                    var existingKeys = CollectExistingSlotKeys(slots);
                    if (existingKeys.Count > 0)
                    {
                        var batchRemove = TryRemoveSlots(ownerItem, existingKeys.ToArray(), continueOnError: false);
                        if (!batchRemove.Ok)
                        {
                            return batchRemove;
                        }
                    }
                }

                return TryRemoveSlotHost(ownerItem);
            }
            catch (Exception ex) { Log.Error("TryRemoveSlotSystem failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        /// <summary>
        /// 移除一个槽位。
        /// 如果槽位包含内容物，会先尝试安全迁出，再根据持久化来源分派到正确的删除流程。
        /// </summary>
        /// <param name="ownerItem">槽位宿主物品。</param>
        /// <param name="slotKey">目标槽位键。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryRemoveSlot(object ownerItem, string slotKey)
        {
            try
            {
                if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "owner null");
                if (string.IsNullOrEmpty(slotKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "slotKey null");

                var persistenceOrigin = TryGetPersistedSlotOrigin(ownerItem, slotKey);
                if (persistenceOrigin == SlotPersistenceOriginHint.Dynamic)
                {
                    return TryRemoveDynamicSlot(ownerItem, slotKey);
                }

                if (persistenceOrigin == SlotPersistenceOriginHint.Builtin)
                {
                    return TryRemoveBuiltinSlot(ownerItem, slotKey);
                }

                return TryRemoveSlotCore(ownerItem, slotKey, markDirty: true);
            }
            catch (Exception ex) { Log.Error("TryRemoveSlot failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        /// <summary>
        /// 执行纯运行时的原子删槽操作。
        /// 该流程只负责拔出内容物、从集合删除槽位并按需打脏，不处理持久化语义。
        /// </summary>
        /// <param name="ownerItem">槽位宿主物品。</param>
        /// <param name="slotKey">目标槽位键。</param>
        /// <param name="markDirty">是否在成功后标记槽位相关脏状态。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        private RichResult TryRemoveSlotCore(object ownerItem, string slotKey, bool markDirty)
        {
            if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "owner null");
            if (string.IsNullOrEmpty(slotKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "slotKey null");
            var slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
            if (slots == null) return RichResult.Fail(ErrorCode.NotSupported, "no Slots on owner");
            var slot = ResolveSlot(slots, slotKey);
            if (slot == null) return RichResult.Fail(ErrorCode.NotFound, "slot not found");
            var contentProp = DuckovReflectionCache.GetProp(slot.GetType(), "Content", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var content = contentProp?.GetValue(slot, null);
            if (content != null)
            {
                var unplug = DuckovReflectionCache.GetMethod(slot.GetType(), "Unplug", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (unplug != null)
                {
                    unplug.Invoke(slot, null);
                }
            }

            if (!TryRemoveSlotFromCollection(slots, slot))
            {
                return RichResult.Fail(ErrorCode.OperationFailed, "slot remove failed");
            }

            TryInvokeSlotChanged(slot);
            NotifySlotAndChildChanged(ownerItem);
            if (markDirty)
            {
                MarkDirtyFromWriteScope(ownerItem, DirtyKind.Slots);
            }

            return RichResult.Success();
        }

    }
}