using System;
using System.Collections.Generic;
using static ItemModKit.Adapters.Duckov.DuckovTypeUtils;

namespace ItemModKit.Adapters.Duckov
{
 /// <summary>
 /// 简易互斥：按物品的稳定 ID 进行互斥锁定，避免跨 Mod 同时操作同一物品。
 /// </summary>
 public static class DuckovMutex
 {
 private static readonly Dictionary<int, string> s_locks = new Dictionary<int, string>();
 /// <summary>尝试以 ownerId 加锁（已被其他 owner 占有时返回 false）。</summary>
 public static bool TryLock(object item, string ownerId)
 {
 try
 {
 int id = GetStableId(item);
 lock (s_locks)
 {
 if (s_locks.TryGetValue(id, out var cur) && !string.IsNullOrEmpty(cur) && !string.Equals(cur, ownerId, StringComparison.Ordinal))
 return false;
 s_locks[id] = ownerId ?? string.Empty; return true;
 }
 }
 catch { return false; }
 }
 /// <summary>若当前 ownerId 持有锁，则释放。</summary>
 public static void Unlock(object item, string ownerId)
 {
 try
 {
 int id = GetStableId(item);
 lock (s_locks)
 {
 if (s_locks.TryGetValue(id, out var cur) && string.Equals(cur, ownerId, StringComparison.Ordinal)) s_locks.Remove(id);
 }
 }
 catch { }
 }
 }
}
