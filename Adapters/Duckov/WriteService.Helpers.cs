using System;
using System.Collections.Generic;
using ItemModKit.Core;
using System.Reflection;

namespace ItemModKit.Adapters.Duckov
{
 /// <summary>
 /// 写入服务（辅助）：槽位树通知与生成唯一槽位键。
 /// </summary>
 internal sealed partial class WriteService : IWriteService
 {
 private void NotifySlotAndChildChanged(object owner)
 {
 try
 {
 var notifySlotTreeChanged = DuckovReflectionCache.GetMethod(owner.GetType(), "NotifySlotTreeChanged", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 var notifyChildChanged = DuckovReflectionCache.GetMethod(owner.GetType(), "NotifyChildChanged", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 notifySlotTreeChanged?.Invoke(owner, null);
 notifyChildChanged?.Invoke(owner, null);
 }
 catch { }
 }
 private string EnsureUniqueSlotKey(object slots, string desired)
 {
 try
 {
 var listField = DuckovReflectionCache.GetField(slots.GetType(), "list", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 var list = listField?.GetValue(slots) as System.Collections.IEnumerable;
 var set = new HashSet<string>(StringComparer.Ordinal);
 if (list != null)
 {
 foreach (var s in list)
 {
 var key = DuckovTypeUtils.GetMaybe(s, new[]{"Key","key"});
 if (key != null) set.Add(Convert.ToString(key));
 }
 }
 return KeyHelper.NextIncrementalKey(set, desired);
 }
 catch { return desired; }
 }
 }
}
