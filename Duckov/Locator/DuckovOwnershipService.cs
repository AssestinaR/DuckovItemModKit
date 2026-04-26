using System;
using ItemModKit.Core.Locator;
using static ItemModKit.Adapters.Duckov.DuckovTypeUtils;

namespace ItemModKit.Adapters.Duckov.Locator
{
    internal sealed class DuckovOwnershipService : IOwnershipService
    {
        public IItemHandle GetOwner(IItemHandle item)
        {
            try
            {
                var raw = item?.TryGetRaw(); if (raw == null) return null;
                var prop = raw.GetType().GetProperty("ParentItem", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                var val = prop?.GetValue(raw, null);
                return DuckovHandleFactory.CreateItemHandle(val);
            }
            catch { return null; }
        }
        public IItemHandle GetCharacterRoot(IItemHandle item)
        {
            try
            {
                var cur = item;
                int guard = 0;
                while (cur != null && guard++ < 32)
                {
                    var parent = GetOwner(cur);
                    if (parent == null) return cur;
                    cur = parent;
                }
                return cur;
            }
            catch { return item; }
        }
        private readonly System.Collections.Generic.Dictionary<object,(InventoryKind kind,int cap)> _invCache = new System.Collections.Generic.Dictionary<object,(InventoryKind,int)>();
        public IInventoryHandle GetInventory(IItemHandle item)
        {
            try
            {
                var raw = item?.TryGetRaw(); if (raw == null) return null;
                var inv = raw.GetType().GetProperty("InInventory", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)?.GetValue(raw, null);
                if (inv == null) return null;
                if (!_invCache.TryGetValue(inv, out var cached))
                {
                    int cap = 0; try { cap = (int) (inv.GetType().GetProperty("Capacity", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)?.GetValue(inv, null) ?? 0); } catch { }
                    var kind = InventoryKind.Other;
                    try
                    {
                        var lmT = FindType("TeamSoda.Duckov.Core.LevelManager") ?? FindType("LevelManager");
                        var lootDict = lmT?.GetProperty("LootBoxInventories", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)?.GetValue(null, null) as System.Collections.IDictionary;
                        if (lootDict != null)
                        {
                            foreach (var v in lootDict.Values) if (ReferenceEquals(v, inv)) { kind = InventoryKind.LootBox; break; }
                        }
                        if (kind == InventoryKind.Other)
                        {
                            var psT = FindType("TeamSoda.Duckov.Core.PlayerStorage") ?? FindType("PlayerStorage");
                            var storageInv = psT?.GetProperty("Inventory", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)?.GetValue(null, null);
                            if (storageInv != null && ReferenceEquals(storageInv, inv)) kind = InventoryKind.Storage;
                            if (kind == InventoryKind.Other)
                            {
                                var cmcT = FindType("CharacterMainControl") ?? FindType("TeamSoda.Duckov.Core.CharacterMainControl");
                                var main = cmcT?.GetProperty("Main", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)?.GetValue(null, null);
                                var charItem = main?.GetType().GetProperty("CharacterItem", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance)?.GetValue(main, null);
                                var cinv = charItem?.GetType().GetProperty("Inventory", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance)?.GetValue(charItem, null);
                                if (cinv != null && ReferenceEquals(cinv, inv)) kind = InventoryKind.Player;
                            }
                        }
                    }
                    catch { }
                    cached = (kind, cap); _invCache[inv] = cached;
                }
                return new DuckovInventoryHandle(inv, cached.cap, cached.kind, item);
            }
            catch { return null; }
        }
        public ISlotHandle GetSlot(IItemHandle item)
        {
            try
            {
                var raw = item?.TryGetRaw(); if (raw == null) return null;
                var slot = raw.GetType().GetProperty("PluggedIntoSlot", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)?.GetValue(raw, null);
                if (slot == null) return null;
                string key = null; try { key = slot.GetType().GetProperty("Key", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)?.GetValue(slot, null) as string; } catch { }
                var content = slot.GetType().GetProperty("Content", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)?.GetValue(slot, null);
                return new DuckovSlotHandle(slot, key, GetOwner(item), DuckovHandleFactory.CreateItemHandle(content));
            }
            catch { return null; }
        }
    }
}
