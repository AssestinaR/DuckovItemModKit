using ItemModKit.Core;
using System;

namespace ItemModKit.Core
{
    /// <summary>
    /// Stage 1 辅助工具：提供核心字段快照与回滚的小型包装器。
    /// 这样 UI 或调试层不需要重复拼装 IReadService / IWriteService 的样板调用。
    /// </summary>
    public static class SnapshotHelper
    {
        /// <summary>通过 IReadService 抓取当前核心字段快照。</summary>
        public static CoreFields CaptureCore(IReadService read, object item)
        {
            if (read == null || item == null) return new CoreFields();
            var res = read.TryReadCoreFields(item);
            return res.Ok && res.Value != null ? res.Value : new CoreFields();
        }

        /// <summary>使用 IWriteService 把核心字段回滚到先前快照；若调用方自行包裹事务，则可获得更稳定的回滚语义。</summary>
        public static RichResult RollbackCore(IWriteService write, object item, CoreFields original)
        {
            if (write == null) return RichResult.Fail(ErrorCode.InvalidArgument, "write null");
            if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item null");
            if (original == null) return RichResult.Fail(ErrorCode.InvalidArgument, "snapshot null");
            var changes = new CoreFieldChanges
            {
                Name = original.Name,
                RawName = original.RawName,
                TypeId = original.TypeId,
                Quality = original.Quality,
                DisplayQuality = original.DisplayQuality,
                Value = original.Value
            };
            // WriteService 自身已经会在失败路径上处理必要的回退语义。
            return write.TryWriteCoreFields(item, changes);
        }
    }
}
