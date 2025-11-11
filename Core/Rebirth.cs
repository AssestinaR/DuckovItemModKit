using System;

namespace ItemModKit.Core
{
 /// <summary>
 /// 重生辅助：统一包装 IRebirthService.ReplaceRebirth 调用，处理入参校验与异常捕获。
 /// </summary>
 public static class Rebirth
 {
 /// <summary>
 /// 用指定元数据替换旧物品，支持 keepLocation 控制是否保持原位置。
 /// </summary>
 public static RichResult<object> Replace(IRebirthService svc, object oldItem, ItemMeta meta, bool keepLocation = true)
 {
 if (svc == null || oldItem == null || meta == null)
 {
 Log.Warn("Rebirth.Replace invalid args: svc/oldItem/meta must not be null");
 return RichResult<object>.Fail(ErrorCode.InvalidArgument, "invalid args");
 }
 try { return svc.ReplaceRebirth(oldItem, meta, keepLocation); }
 catch (Exception ex)
 {
 Log.Error("Rebirth.Replace threw", ex);
 return RichResult<object>.Fail(ErrorCode.OperationFailed, ex.Message);
 }
 }
 }
}
