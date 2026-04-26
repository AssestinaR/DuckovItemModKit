using System;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 读取服务（Slots）：提供槽位相关的只读查询。
    /// </summary>
    internal sealed partial class ReadService
    {
        /// <summary>读取槽位列表。</summary>
        public RichResult<SlotEntry[]> TryReadSlots(object item)
        {
            try
            {
                if (item == null) return RichResult<SlotEntry[]>.Fail(ErrorCode.InvalidArgument, "item is null");
                return RichResult<SlotEntry[]>.Success(_item.GetSlots(item));
            }
            catch (Exception ex)
            {
                Log.Error("TryReadSlots failed", ex);
                return RichResult<SlotEntry[]>.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }
    }
}