using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
 /// <summary>
 /// 查询扩展：对 IItemQuery 提供更简短的包装方法与遍历辅助。
 /// </summary>
 public static class ItemQueryExtensions
 {
 // Convenience wrappers (null-safe)
 public static bool Backpack(this IItemQuery q, int index1Based, out object item)
 { item = null; return q != null && q.TryGetFromBackpack(index1Based, out item); }
 public static bool Storage(this IItemQuery q, int index1Based, out object item)
 { item = null; return q != null && q.TryGetFromStorage(index1Based, out item); }
 public static bool AnyInventory(this IItemQuery q, int index1Based, out object item)
 { item = null; return q != null && q.TryGetFromAnyInventory(index1Based, out item); }
 public static bool WeaponSlot(this IItemQuery q, int slotIndex1Based, out object item)
 { item = null; return q != null && q.TryGetWeaponSlot(slotIndex1Based, out item); }

 public static IEnumerable<object> All(this IItemQuery q)
 { return q?.EnumerateAllInventories() ?? Array.Empty<object>(); }
 }
}
