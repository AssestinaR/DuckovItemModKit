using System;
using System.Collections.Generic;
using System.Reflection;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 写入服务（槽位/流程支持）：
    /// 负责槽位流程共用的宿主刷新、脏标记、内容迁移和批量键标准化。
    /// </summary>
    internal sealed partial class WriteService : IWriteService
    {
        /// <summary>
        /// 尝试刷新宿主物品所在背包的 UI 表现。
        /// 该操作主要用于槽位结构或内容变化后的界面同步。
        /// </summary>
        /// <param name="ownerItem">槽位宿主物品。</param>
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

        /// <summary>
        /// 在写服务授权范围内标记脏状态，并按需立即触发持久化 flush。
        /// </summary>
        /// <param name="ownerItem">被标脏的宿主物品。</param>
        /// <param name="kind">脏类型掩码。</param>
        /// <param name="forceFlush">是否立即执行强制刷盘。</param>
        private static void MarkDirtyFromWriteScope(object ownerItem, DirtyKind kind, bool forceFlush = true)
        {
            if (ownerItem == null || kind == DirtyKind.None)
            {
                return;
            }

            using (IMKDuckov.AllowDirtyFromWriteService())
            {
                IMKDuckov.MarkDirty(ownerItem, kind, immediate: forceFlush);
            }

            if (forceFlush)
            {
                IMKDuckov.FlushDirty(ownerItem, force: true);
            }
        }

        /// <summary>
        /// 在移除槽位前安全迁出其内容物。
        /// 优先尝试放回玩家背包，其次尝试放入仓库；若两者都失败，则回插原槽位。
        /// </summary>
        /// <param name="ownerItem">槽位宿主物品。</param>
        /// <param name="slotKey">待移除槽位的键。</param>
        /// <returns>迁移成功返回成功结果；失败时返回冲突或操作失败错误。</returns>
        private RichResult TryRelocateSlotContentForRemoval(object ownerItem, string slotKey)
        {
            try
            {
                var slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (slots == null)
                {
                    return RichResult.Success();
                }

                var slot = ResolveSlot(slots, slotKey);
                if (slot == null || !TryGetSlotContent(slot, out var content) || content == null)
                {
                    return RichResult.Success();
                }

                var unplugged = TryUnplugSlot(slot) ?? content;
                var moved = IMKDuckov.Mover.TrySendToPlayerInventory(unplugged, dontMerge: false);
                if (!moved.Ok)
                {
                    moved = IMKDuckov.Mover.TrySendToWarehouse(unplugged, directToBuffer: false);
                }

                if (!moved.Ok)
                {
                    var restore = TryPlugIntoSlot(ownerItem, slotKey, unplugged);
                    if (!restore.Ok)
                    {
                        return RichResult.Fail(ErrorCode.OperationFailed, "slot.remove.relocate_failed_restore_failed:" + (moved.Error ?? moved.Code.ToString()));
                    }

                    return RichResult.Fail(ErrorCode.Conflict, "slot.remove.relocate_failed:" + (moved.Error ?? moved.Code.ToString()));
                }

                return RichResult.Success();
            }
            catch (Exception ex)
            {
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>
        /// 标准化槽位键列表。
        /// 该过程会移除空白项、去掉首尾空格并按大小写不敏感规则去重。
        /// </summary>
        /// <param name="slotKeys">原始槽位键序列。</param>
        /// <returns>标准化后的槽位键列表。</returns>
        private static List<string> NormalizeSlotKeys(IEnumerable<string> slotKeys)
        {
            var normalized = new List<string>();
            if (slotKeys == null)
            {
                return normalized;
            }

            foreach (var rawKey in slotKeys)
            {
                var key = rawKey?.Trim();
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (!normalized.Exists(existing => string.Equals(existing, key, StringComparison.OrdinalIgnoreCase)))
                {
                    normalized.Add(key);
                }
            }

            return normalized;
        }
    }
}