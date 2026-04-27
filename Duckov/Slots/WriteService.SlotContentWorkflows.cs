using System;
using System.Reflection;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 写入服务（槽位/内容流程）：
    /// 负责插槽、拔槽以及同宿主内部的槽位内容移动。
    /// </summary>
    internal sealed partial class WriteService : IWriteService
    {
        /// <summary>
        /// 将子物品插入到指定槽位。
        /// 该流程会校验槽位是否存在、是否允许插入，并在成功后触发通知与脏标记。
        /// </summary>
        /// <param name="ownerItem">槽位宿主物品。</param>
        /// <param name="slotKey">目标槽位键。</param>
        /// <param name="childItem">待插入的子物品。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryPlugIntoSlot(object ownerItem, string slotKey, object childItem)
        {
            try
            {
                if (ownerItem == null || childItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.args");
                if (string.IsNullOrEmpty(slotKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.key");
                var slots = GetSlotHost(ownerItem);
                if (slots == null) return RichResult.Fail(ErrorCode.NotSupported, "slot.owner.no_slots");
                var slot = ResolveSlot(slots, slotKey);
                if (slot == null) return RichResult.Fail(ErrorCode.NotFound, "slot.notfound");
                if (!CanPlug(slot, childItem)) return RichResult.Fail(ErrorCode.Conflict, "slot.incompatible");
                var (plug, hasOutPrev) = ResolvePlugMethod(slot);
                if (plug == null) return RichResult.Fail(ErrorCode.NotSupported, "slot.no_plug");
                bool result;
                if (hasOutPrev)
                {
                    var parameters = new object[] { childItem, null };
                    result = (bool)plug.Invoke(slot, parameters);
                }
                else
                {
                    var r = plug.Invoke(slot, new object[] { childItem });
                    result = r is bool b && b;
                }

                if (!result) return RichResult.Fail(ErrorCode.OperationFailed, "slot.plug.failed");
                NotifySlotAndChildChanged(ownerItem);
                MarkDirtyFromWriteScope(ownerItem, DirtyKind.Slots);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryPlugIntoSlot failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        /// <summary>
        /// 从指定槽位中拔出当前内容物。
        /// </summary>
        /// <param name="ownerItem">槽位宿主物品。</param>
        /// <param name="slotKey">目标槽位键。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryUnplugFromSlot(object ownerItem, string slotKey)
        {
            try
            {
                if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.owner");
                if (string.IsNullOrEmpty(slotKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.key");
                var slots = GetSlotHost(ownerItem);
                if (slots == null) return RichResult.Fail(ErrorCode.NotSupported, "slot.owner.no_slots");
                var slot = ResolveSlot(slots, slotKey);
                if (slot == null) return RichResult.Fail(ErrorCode.NotFound, "slot.notfound");
                var unplug = GetSlotInstancePlan(slot.GetType()).Unplug;
                if (unplug == null) return RichResult.Fail(ErrorCode.NotSupported, "slot.no_unplug");
                unplug.Invoke(slot, null);
                NotifySlotAndChildChanged(ownerItem);
                MarkDirtyFromWriteScope(ownerItem, DirtyKind.Slots);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryUnplugFromSlot failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        /// <summary>
        /// 在同一宿主物品上把内容物从一个槽位移动到另一个槽位。
        /// </summary>
        /// <param name="ownerItem">槽位宿主物品。</param>
        /// <param name="fromSlotKey">源槽位键。</param>
        /// <param name="toSlotKey">目标槽位键。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryMoveBetweenSlots(object ownerItem, string fromSlotKey, string toSlotKey)
        {
            try
            {
                if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.owner");
                if (string.IsNullOrEmpty(fromSlotKey) || string.IsNullOrEmpty(toSlotKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.key");
                var slots = GetSlotHost(ownerItem);
                if (slots == null) return RichResult.Fail(ErrorCode.NotSupported, "slot.owner.no_slots");
                var from = ResolveSlot(slots, fromSlotKey);
                var to = ResolveSlot(slots, toSlotKey);
                if (from == null || to == null) return RichResult.Fail(ErrorCode.NotFound, "slot.notfound");
                if (!TryGetSlotContent(from, out var content)) return RichResult.Fail(ErrorCode.NotFound, "slot.from.empty");
                if (!CanPlug(to, content)) return RichResult.Fail(ErrorCode.Conflict, "slot.to.incompatible");

                var unplug = GetSlotInstancePlan(from.GetType()).Unplug;
                if (unplug == null) return RichResult.Fail(ErrorCode.NotSupported, "slot.from.no_unplug");
                unplug.Invoke(from, null);

                var (plug, hasOutPrev) = ResolvePlugMethod(to);
                if (plug == null) return RichResult.Fail(ErrorCode.NotSupported, "slot.to.no_plug");
                bool ok;
                if (hasOutPrev)
                {
                    var parameters = new object[] { content, null };
                    ok = (bool)plug.Invoke(to, parameters);
                }
                else
                {
                    var r = plug.Invoke(to, new[] { content });
                    ok = r is bool b && b;
                }

                if (!ok) return RichResult.Fail(ErrorCode.OperationFailed, "slot.move.plug_failed");
                NotifySlotAndChildChanged(ownerItem);
                MarkDirtyFromWriteScope(ownerItem, DirtyKind.Slots);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryMoveBetweenSlots failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
    }
}