using System;
using System.Collections;
using System.Collections.Generic;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 物品查询适配器：提供按索引获取、遍历背包/仓库/所有玩家背包集合、读取武器槽等功能。
    /// 封装引擎反射访问，统一 Try* 语义。
    /// </summary>
    [Obsolete("新代码优先使用 IMKDuckov.Query 兼容查询门面或 IMKDuckov.QueryV2。", false)]
    internal sealed class DuckovItemQuery : IItemQuery
    {
        private static readonly DuckovCompatItemQueryFacade s_facade = new DuckovCompatItemQueryFacade();

        public bool TryGetFromBackpack(int index1Based, out object item) => s_facade.TryGetFromBackpack(index1Based, out item);

        public bool TryGetFromStorage(int index1Based, out object item) => s_facade.TryGetFromStorage(index1Based, out item);

        public bool TryGetFromAnyInventory(int index1Based, out object item) => s_facade.TryGetFromAnyInventory(index1Based, out item);

        public bool TryGetWeaponSlot(int slotIndex1Based, out object item) => s_facade.TryGetWeaponSlot(slotIndex1Based, out item);

        public IEnumerable<object> EnumerateBackpack() => s_facade.EnumerateBackpack();

        public IEnumerable<object> EnumerateStorage() => s_facade.EnumerateStorage();

        public IEnumerable<object> EnumerateAllInventories() => s_facade.EnumerateAllInventories();
    }
}
