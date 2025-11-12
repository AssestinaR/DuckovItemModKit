using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
    /// <summary>
    /// 查询扩展：为 <see cref="IItemQuery"/> 提供空安全的包装与集合遍历辅助方法。
    /// </summary>
    public static class ItemQueryExtensions
    {
        /// <summary>从背包按 1-based 索引获取物品（空安全）。</summary>
        /// <param name="q">查询接口。</param>
        /// <param name="index1Based">1-based 索引。</param>
        /// <param name="item">输出物品。</param>
        /// <returns>是否成功。</returns>
        public static bool Backpack(this IItemQuery q, int index1Based, out object item)
        { item = null; return q != null && q.TryGetFromBackpack(index1Based, out item); }
        /// <summary>从仓库按 1-based 索引获取物品（空安全）。</summary>
        public static bool Storage(this IItemQuery q, int index1Based, out object item)
        { item = null; return q != null && q.TryGetFromStorage(index1Based, out item); }
        /// <summary>从任意背包按 1-based 索引获取物品（空安全）。</summary>
        public static bool AnyInventory(this IItemQuery q, int index1Based, out object item)
        { item = null; return q != null && q.TryGetFromAnyInventory(index1Based, out item); }
        /// <summary>从武器槽按 1-based 索引获取物品（空安全）。</summary>
        public static bool WeaponSlot(this IItemQuery q, int slotIndex1Based, out object item)
        { item = null; return q != null && q.TryGetWeaponSlot(slotIndex1Based, out item); }

        /// <summary>枚举所有背包中的物品集合（空安全）。</summary>
        public static IEnumerable<object> All(this IItemQuery q)
        { return q?.EnumerateAllInventories() ?? Array.Empty<object>(); }
    }
}
