using ItemModKit.Core;
using ItemStatsSystem;
using System.Reflection;
using UnityEngine;
using static ItemModKit.Adapters.Duckov.DuckovTypeUtils;

namespace ItemModKit.Adapters.Duckov
{
    internal sealed class DuckovInventoryAdapter : IInventoryAdapter
    {
        public bool IsInInventory(object item)
        {
            try
            {
                var it = UnwrapItem(item);
                if (it == null) return false;
                return GetProp<object>(it, "InInventory") != null;
            }
            catch { return false; }
        }

        public object GetInventory(object item)
        {
            try { var it = UnwrapItem(item); return GetProp<object>(it, "InInventory"); } catch { return null; }
        }

        public int GetCapacity(object inventory)
        {
            try { return GetProp<int>(inventory, "Capacity"); } catch { return 0; }
        }

        public object GetItemAt(object inventory, int index)
        {
            try
            {
                var m = inventory?.GetType().GetMethod("get_Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return m?.Invoke(inventory, new object[] { index });
            }
            catch { return null; }
        }

        public int IndexOf(object inventory, object item)
        {
            try
            {
                var itTarget = UnwrapItem(item) as Item;
                if (!itTarget) return -1;
                int cap = GetCapacity(inventory);
                for (int i = 0; i < cap; i++)
                {
                    var it = GetItemAt(inventory, i);
                    var itCanon = UnwrapItem(it) as Item;
                    if (!itCanon) continue;
                    if (ReferenceEquals(itCanon, itTarget)) return i;
                }
            }
            catch { }
            return -1;
        }

        public bool AddAt(object inventory, object item, int index)
        {
            try
            {
                var m = inventory?.GetType().GetMethod("AddAt", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (m != null)
                {
                    var r = m.Invoke(inventory, new object[] { UnwrapItem(item) ?? item, index });
                    return r is bool b ? b : true;
                }
                return AddAndMerge(inventory, item);
            }
            catch { return false; }
        }

        public bool AddAndMerge(object inventory, object item)
        {
            try
            {
                var it = UnwrapItem(item) ?? item;
                var m = inventory?.GetType().GetMethod("AddAndMerge", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (m != null)
                {
                    var r = m.Invoke(inventory, new object[] { it, 0 });
                    return r is bool b ? b : true;
                }
                m = inventory?.GetType().GetMethod("AddItem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (m != null)
                {
                    var ps = m.GetParameters();
                    object[] args = (ps.Length == 2) ? new object[] { it, false } : new object[] { it };
                    var r = m.Invoke(inventory, args);
                    return r is bool b ? b : true;
                }
            }
            catch { }
            return false;
        }

        public void Detach(object item)
        {
            try { (UnwrapItem(item) as Item)?.Detach(); }
            catch
            {
                try
                {
                    var u = UnwrapItem(item);
                    u?.GetType().GetMethod("Detach")?.Invoke(u, null);
                }
                catch { }
            }
        }

        private static object UnwrapItem(object obj)
        {
            if (obj is Item it) return it;
            if (obj is Component c)
            {
                try { var got = c.GetComponent<Item>(); if (got) return got; } catch { }
                return c;
            }
            if (obj is GameObject go)
            {
                try { var got = go.GetComponent<Item>(); if (got) return got; } catch { }
                return go;
            }
            try
            {
                var t = obj?.GetType(); if (t == null) return null;
                var p = t.GetProperty("Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var v = p?.GetValue(obj, null) as Item; if (v) return v;
                var f = t.GetField("item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                v = v ?? (f?.GetValue(obj) as Item); if (v) return v;
            }
            catch { }
            return obj;
        }
    }
}
