using System;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
 internal static class Atomic
 {
 public static RichResult Run(IItemAdapter adapter, IWriteService writer, object item, Func<RichResult> action)
 {
 if (adapter == null || writer == null || item == null || action == null)
 return RichResult.Fail(ErrorCode.InvalidArgument, "null args");
 var token = writer.BeginTransaction(item);
 try
 {
 var r = action();
 if (!r.Ok) { writer.RollbackTransaction(item, token); return r; }
 writer.CommitTransaction(item, token);
 return r;
 }
 catch (Exception ex)
 {
 Log.Error("Atomic.Run failed", ex);
 writer.RollbackTransaction(item, token);
 return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
 }
 }
 }
}
