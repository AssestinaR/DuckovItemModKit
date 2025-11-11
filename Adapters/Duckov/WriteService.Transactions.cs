using System;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
 /// <summary>
 /// 写入服务（事务）：Begin/Commit/Rollback；提交时统一发出脏标记（显式模式）。
 /// </summary>
 internal sealed partial class WriteService : IWriteService
 {
 private static readonly DuckovTransactionManager s_tx = new DuckovTransactionManager();

 /// <summary>开始事务，返回令牌。</summary>
 public string BeginTransaction(object ownerItem)
 {
 try
 {
 if (ownerItem == null) return null;
 return s_tx.Begin(_item, ownerItem);
 }
 catch (Exception ex) { Log.Error("BeginTransaction failed", ex); return null; }
 }
 /// <summary>提交事务：允许集中触发持久化所需的脏标记。</summary>
 public RichResult CommitTransaction(object ownerItem, string token)
 {
 try
 {
 if (ownerItem == null || string.IsNullOrEmpty(token)) return RichResult.Fail(ErrorCode.InvalidArgument, "args");
 var ok = s_tx.Commit(ownerItem, token);
 if (!ok) return RichResult.Fail(ErrorCode.NotFound, "tx not found");
 // 显式模式下：仅在提交时允许从写入服务发起脏标记
 using (IMKDuckov.AllowDirtyFromWriteService())
 {
     IMKDuckov.MarkDirty(ownerItem, DirtyKind.All);
 }
 return RichResult.Success();
 }
 catch (Exception ex) { Log.Error("CommitTransaction failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
 }
 /// <summary>回滚事务。</summary>
 public RichResult RollbackTransaction(object ownerItem, string token)
 {
 try
 {
 if (ownerItem == null || string.IsNullOrEmpty(token)) return RichResult.Fail(ErrorCode.InvalidArgument, "args");
 return s_tx.Rollback(_item, this, ownerItem, token) ? RichResult.Success() : RichResult.Fail(ErrorCode.NotFound, "tx not found");
 }
 catch (Exception ex) { Log.Error("RollbackTransaction failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
 }
 }
}
