using System;
using UnityEngine;
using System.Collections.Generic;
using ItemModKit.Core.Locator;

namespace ItemModKit.Adapters.Duckov.Locator
{
    /// <summary>
    /// Inventory 分类器（无 Harmony 版本）。使用 DuckovTypeUtils + 反射扫描静态管理器属性。
    /// 提供基础分类：玩家背包 / 存储 / 战利品箱 / 其它。World 通过外部事件捕获，不在此直接分类。
    /// </summary>
    public sealed class DuckovInventoryClassifier : IInventoryClassifier
    {
        private static Type s_levelManagerType;
        private static Type s_playerStorageType;
        private static Type s_characterMainType;

        private static void EnsureTypes()
        {
            if (s_levelManagerType == null)
            {
                s_levelManagerType = DuckovTypeUtils.FindType("TeamSoda.Duckov.Core.LevelManager") ?? DuckovTypeUtils.FindType("LevelManager");
            }
            if (s_playerStorageType == null)
            {
                s_playerStorageType = DuckovTypeUtils.FindType("TeamSoda.Duckov.Core.PlayerStorage") ?? DuckovTypeUtils.FindType("PlayerStorage");
            }
            if (s_characterMainType == null)
            {
                s_characterMainType = DuckovTypeUtils.FindType("CharacterMainControl") ?? DuckovTypeUtils.FindType("TeamSoda.Duckov.Core.CharacterMainControl");
            }
        }

        public InventoryKind ClassifyInventory(object inv)
        {
            if (inv == null) return InventoryKind.Unknown;
            try { if (IsPlayerInventory(inv)) return InventoryKind.Player; } catch { }
            try { if (IsStorage(inv)) return InventoryKind.Storage; } catch { }
            try { if (IsLootBox(inv)) return InventoryKind.LootBox; } catch { }
            return InventoryKind.Other;
        }

        public bool IsLootBox(object inv)
        {
            try
            {
                EnsureTypes();
                if (inv == null || s_levelManagerType == null) return false;
                var dictProp = s_levelManagerType.GetProperty("LootBoxInventories", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var parentProp = s_levelManagerType.GetProperty("LootBoxInventoriesParent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var dict = dictProp?.GetValue(null, null) as System.Collections.IDictionary;
                if (dict != null)
                {
                    foreach (var v in dict.Values) if (ReferenceEquals(v, inv)) return true;
                }
                var parent = parentProp?.GetValue(null, null) as Transform;
                var comp = inv as Component;
                if (parent && comp && comp.transform.parent == parent) return true;
                if (comp && comp.gameObject.name.StartsWith("Inventory_", StringComparison.Ordinal)) return true;
            }
            catch { }
            return false;
        }

        public bool IsPlayerInventory(object inv)
        {
            try
            {
                EnsureTypes();
                if (inv == null) return false;
                // PlayerStorage inventory
                if (s_playerStorageType != null)
                {
                    var psInv = s_playerStorageType.GetProperty("Inventory", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null, null);
                    if (psInv != null && ReferenceEquals(psInv, inv)) return true;
                }
                // Character main inventory
                if (s_characterMainType != null)
                {
                    var main = s_characterMainType.GetProperty("Main", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null, null);
                    if (main != null)
                    {
                        var charItem = main.GetType().GetProperty("CharacterItem")?.GetValue(main, null);
                        var cinv = charItem?.GetType().GetProperty("Inventory")?.GetValue(charItem, null);
                        if (cinv != null && ReferenceEquals(cinv, inv)) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public bool IsStorage(object inv)
        {
            try
            {
                EnsureTypes();
                if (inv == null || s_playerStorageType == null) return false;
                var psInv = s_playerStorageType.GetProperty("Inventory", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null, null);
                if (psInv != null && ReferenceEquals(psInv, inv)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// 枚举所有已知战利品箱 Inventory（辅助下游快速批量处理）。
        /// </summary>
        public IEnumerable<object> EnumerateLootBoxes()
        {
            var list = new List<object>();
            try
            {
                EnsureTypes();
                if (s_levelManagerType == null) return list;
                var dictProp = s_levelManagerType.GetProperty("LootBoxInventories", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var dict = dictProp?.GetValue(null, null) as System.Collections.IDictionary;
                if (dict != null)
                {
                    foreach (var v in dict.Values) if (v != null) list.Add(v);
                }
            }
            catch { }
            return list;
        }
    }
}
