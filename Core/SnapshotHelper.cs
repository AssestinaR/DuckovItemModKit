using ItemModKit.Core;
using System;

namespace ItemModKit.Core
{
    /// <summary>
    /// Stage 1 contract helper: core snapshot / rollback utilities without altering public service interfaces.
    /// Provides minimal wrappers so UI layers do not duplicate reflection logic.
    /// </summary>
    public static class SnapshotHelper
    {
        /// <summary>Capture current core fields via IReadService (Name/RawName/TypeId/Quality/DisplayQuality/Value).</summary>
        public static CoreFields CaptureCore(IReadService read, object item)
        {
            if (read == null || item == null) return new CoreFields();
            var res = read.TryReadCoreFields(item);
            return res.Ok && res.Value != null ? res.Value : new CoreFields();
        }
        /// <summary>Rollback core fields to a previous snapshot using IWriteService (transaction-safe if caller wraps).</summary>
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
            // WriteService already handles rollback on failure internally.
            return write.TryWriteCoreFields(item, changes);
        }
    }
}
