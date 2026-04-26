using System;
using ItemModKit.Core.Locator;

namespace ItemModKit.Adapters.Duckov.Locator
{
    internal sealed class DuckovInventoryHandle : IInventoryHandle
    {
        public int InstanceId { get; }
        public int Capacity { get; }
        public InventoryKind Kind { get; }
        public IItemHandle OwnerItem { get; }
        public object Raw { get; }
        public DuckovInventoryHandle(object raw, int capacity, InventoryKind kind, IItemHandle owner)
        {
            Raw = raw; Capacity = capacity; Kind = kind; OwnerItem = owner;
            try { InstanceId = raw is UnityEngine.Object u ? u.GetInstanceID() : raw?.GetHashCode() ?? 0; } catch { InstanceId = 0; }
        }
    }
    internal sealed class DuckovSlotHandle : ISlotHandle
    {
        public string Key { get; }
        public IItemHandle Owner { get; }
        public bool Occupied { get; }
        public IItemHandle Content { get; }
        public object Raw { get; }
        public DuckovSlotHandle(object raw, string key, IItemHandle owner, IItemHandle content)
        {
            Raw = raw; Key = key; Owner = owner; Content = content; Occupied = content != null;
        }
    }
    internal static class DuckovHandleFactory
    {
        public static IItemHandle CreateItemHandle(object raw)
        {
            if (raw == null) return null;
            int? iid = null;
            try { if (raw is UnityEngine.Object u) iid = u.GetInstanceID(); } catch { }
            int typeId = 0; string name = null; string[] tags = null;
            try { typeId = IMKDuckov.Item.GetTypeId(raw); } catch { }
            try { name = IMKDuckov.Item.GetDisplayNameRaw(raw) ?? IMKDuckov.Item.GetName(raw); } catch { }
            try { tags = IMKDuckov.Item.GetTags(raw); } catch { }
            return new ItemHandle(() => raw, iid, null, typeId, name, tags);
        }
    }
}
