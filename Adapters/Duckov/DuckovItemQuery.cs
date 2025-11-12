using System;
using System.Collections;
using System.Collections.Generic;
using ItemModKit.Core;
using static ItemModKit.Adapters.Duckov.DuckovTypeUtils;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 物品查询适配器：提供按索引获取、遍历背包/仓库/所有玩家背包集合、读取武器槽等功能。
    /// 封装引擎反射访问，统一 Try* 语义。
    /// </summary>
    internal sealed class DuckovItemQuery : IItemQuery
    {
        public bool TryGetFromBackpack(int index1Based, out object item) { item = null; try { var inv = GetBackpack(); return TryGetAt(inv, index1Based, out item); } catch { return false; } }
        public bool TryGetFromStorage(int index1Based, out object item) { item = null; try { var inv = GetStorage(); return TryGetAt(inv, index1Based, out item); } catch { return false; } }
        public bool TryGetFromAnyInventory(int index1Based, out object item) { item = null; foreach (var inv in EnumerateInventories()) { if (TryGetAt(inv, index1Based, out item)) return true; } return false; }
        /// <summary>获取武器槽位内容（slotIndex 从 1 开始）。</summary>
        public bool TryGetWeaponSlot(int slotIndex1Based, out object item)
        {
            item = null;
            try
            {
                var cmcT = FindType("CharacterMainControl") ?? FindType("TeamSoda.Duckov.Core.CharacterMainControl");
                var main = cmcT?.GetProperty("Main", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null, null);
                var charItem = main?.GetType().GetProperty("CharacterItem", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.GetValue(main, null);
                var slots = charItem?.GetType().GetProperty("Slots", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(charItem, null) as IEnumerable;
                if (slots == null) return false; int idx = 1; foreach (var s in slots) { if (idx++ == slotIndex1Based) { item = GetProp<object>(s, "Content"); return item != null; } }
            }
            catch { }
            return false;
        }
        public IEnumerable<object> EnumerateBackpack() { var inv = GetBackpack(); return EnumerateInventory(inv); }
        public IEnumerable<object> EnumerateStorage() { var inv = GetStorage(); return EnumerateInventory(inv); }
        public IEnumerable<object> EnumerateAllInventories() { foreach (var inv in EnumerateInventories()) foreach (var it in EnumerateInventory(inv)) yield return it; }

        // Internal helpers
        private static object GetBackpack()
        {
            try
            {
                var cmcT = FindType("CharacterMainControl") ?? FindType("TeamSoda.Duckov.Core.CharacterMainControl");
                var main = cmcT?.GetProperty("Main", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null, null);
                return main?.GetType().GetProperty("CharacterItem", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.GetValue(main, null)?.GetType().GetProperty("Inventory")?.GetValue(main.GetType().GetProperty("CharacterItem")?.GetValue(main, null), null);
            }
            catch { return null; }
        }
        private static object GetStorage()
        {
            try
            {
                var t = FindType("TeamSoda.Duckov.Core.PlayerStorage") ?? FindType("PlayerStorage");
                return t?.GetProperty("Inventory", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null, null);
            }
            catch { return null; }
        }
        private static IEnumerable<object> EnumerateInventories()
        {
            var list = new List<object>(); var b = GetBackpack(); if (b != null) list.Add(b); var s = GetStorage(); if (s != null) list.Add(s);
            try
            {
                var util = FindType("TeamSoda.Duckov.Core.ItemUtilities") ?? FindType("ItemUtilities");
                var m = util?.GetMethod("GetPlayerInventories", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var col = m?.Invoke(null, null) as IEnumerable; if (col != null) foreach (var inv in col) if (inv != null && !list.Contains(inv)) list.Add(inv);
            }
            catch { }
            return list;
        }
        private static IEnumerable<object> EnumerateInventory(object inventory)
        {
            if (inventory == null) yield break; int cap = GetProp<int>(inventory, "Capacity"); for (int i = 0; i < cap; i++) yield return GetItem(inventory, i);
        }
        private static object GetItem(object inv, int index)
        {
            try { return inv?.GetType().GetMethod("get_Item", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(inv, new object[] { index }); } catch { return null; }
        }
        private static bool TryGetAt(object inv, int index1Based, out object item)
        {
            item = null; if (inv == null) return false; int idx0 = Math.Max(0, index1Based - 1); item = GetItem(inv, idx0); return item != null;
        }
    }
}
