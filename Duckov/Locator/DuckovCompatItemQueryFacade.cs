using System.Collections.Generic;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    internal sealed class DuckovCompatItemQueryFacade : IItemQuery
    {
        public bool TryGetFromBackpack(int index1Based, out object item)
        {
            return IMKDuckov.TryGetInventoryItem(IMKDuckov.GetCharacterInventory(), index1Based, out item);
        }

        public bool TryGetFromStorage(int index1Based, out object item)
        {
            return IMKDuckov.TryGetInventoryItem(IMKDuckov.GetStorageInventory(), index1Based, out item);
        }

        public bool TryGetFromAnyInventory(int index1Based, out object item)
        {
            item = null;
            foreach (var inventory in EnumerateInventories())
            {
                if (IMKDuckov.TryGetInventoryItem(inventory, index1Based, out item)) return true;
            }
            return false;
        }

        public bool TryGetWeaponSlot(int slotIndex1Based, out object item)
        {
            return IMKDuckov.TryGetWeaponSlotItem(slotIndex1Based, out item);
        }

        public IEnumerable<object> EnumerateBackpack()
        {
            return EnumerateInventory(IMKDuckov.GetCharacterInventory());
        }

        public IEnumerable<object> EnumerateStorage()
        {
            return EnumerateInventory(IMKDuckov.GetStorageInventory());
        }

        public IEnumerable<object> EnumerateAllInventories()
        {
            return IMKDuckov.EnumerateAllKnownItems();
        }

        private static IEnumerable<object> EnumerateInventories()
        {
            var seen = new HashSet<object>();
            var backpack = IMKDuckov.GetCharacterInventory();
            if (backpack != null) seen.Add(backpack);

            var storage = IMKDuckov.GetStorageInventory();
            if (storage != null) seen.Add(storage);

            foreach (var item in IMKDuckov.EnumerateAllKnownItems())
            {
                object inventory = null;
                try { inventory = IMKDuckov.Inventory.GetInventory(item); }
                catch { inventory = null; }
                if (inventory != null) seen.Add(inventory);
            }

            return seen;
        }

        private static IEnumerable<object> EnumerateInventory(object inventory)
        {
            foreach (var item in IMKDuckov.EnumerateAllKnownItems())
            {
                object containingInventory = null;
                try { containingInventory = IMKDuckov.Inventory.GetInventory(item); }
                catch { containingInventory = null; }
                if (ReferenceEquals(containingInventory, inventory) && item != null) yield return item;
            }
        }
    }
}