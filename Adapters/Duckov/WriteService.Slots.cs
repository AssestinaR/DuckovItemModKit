using System;
using System.Collections.Generic;
using System.Reflection;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 写入服务（槽位 Slots）：提供插拔、移动、增删槽位等操作，并发出变更通知与脏标记。
    /// </summary>
    internal sealed partial class WriteService : IWriteService
    {
        // Slots write implementations

        private static object ResolveSlotByKeyOrIndex(object slots, string slotKey)
        {
            if (slots == null || string.IsNullOrEmpty(slotKey)) return null;
            var t = slots.GetType();
            // GetSlot(string)
            var getSlotStr = DuckovReflectionCache.GetMethod(t, "GetSlot", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(string) });
            if (getSlotStr != null)
            {
                try { return getSlotStr.Invoke(slots, new object[] { slotKey }); } catch { }
            }
            // GetSlot(int)
            var getSlotInt = DuckovReflectionCache.GetMethod(t, "GetSlot", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(int) });
            if (getSlotInt != null)
            {
                if (int.TryParse(slotKey, out var idx))
                {
                    try { return getSlotInt.Invoke(slots, new object[] { idx }); } catch { }
                }
                int count = 0;
                try { var p = DuckovReflectionCache.GetProp(t, "Count", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); if (p != null) count = Convert.ToInt32(p.GetValue(slots, null)); } catch { count = 0; }
                if (count <= 0)
                {
                    try { var itemsP = DuckovReflectionCache.GetProp(t, "Items", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); var col = itemsP?.GetValue(slots, null) as System.Collections.ICollection; if (col != null) count = col.Count; } catch { }
                }
                for (int i = 0; i < count; i++)
                {
                    object s = null; try { s = getSlotInt.Invoke(slots, new object[] { i }); } catch { }
                    if (s == null) continue;
                    try
                    {
                        var keyP = DuckovReflectionCache.GetProp(s.GetType(), "Key", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        var keyV = keyP?.GetValue(s, null) as string;
                        if (!string.IsNullOrEmpty(keyV) && string.Equals(keyV, slotKey, StringComparison.OrdinalIgnoreCase)) return s;
                    }
                    catch { }
                }
            }
            return null;
        }

        /// <summary>向槽位插入子物品。</summary>
        public RichResult TryPlugIntoSlot(object ownerItem, string slotKey, object childItem)
        {
            try
            {
                if (ownerItem == null || childItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "null args");
                if (string.IsNullOrEmpty(slotKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "slotKey is null");
                var slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (slots == null) return RichResult.Fail(ErrorCode.NotSupported, "no Slots on owner");
                var slot = ResolveSlotByKeyOrIndex(slots, slotKey);
                if (slot == null) return RichResult.Fail(ErrorCode.NotFound, "slot not found");
                var plug = DuckovReflectionCache.GetMethod(slot.GetType(), "Plug", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                           ?? DuckovReflectionCache.GetMethod(slot.GetType(), "SetContent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (plug == null) return RichResult.Fail(ErrorCode.NotSupported, "Plug/SetContent not found");
                if (plug.Name == "SetContent") plug.Invoke(slot, new object[] { childItem }); else plug.Invoke(slot, new object[] { childItem });
                NotifySlotAndChildChanged(ownerItem);
                IMKDuckov.MarkDirty(ownerItem, DirtyKind.Slots);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryPlugIntoSlot failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>从槽位拔出子物品。</summary>
        public RichResult TryUnplugFromSlot(object ownerItem, string slotKey)
        {
            try
            {
                if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "owner null");
                if (string.IsNullOrEmpty(slotKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "slotKey null");
                var slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (slots == null) return RichResult.Fail(ErrorCode.NotSupported, "no Slots on owner");
                var slot = ResolveSlotByKeyOrIndex(slots, slotKey);
                if (slot == null) return RichResult.Fail(ErrorCode.NotFound, "slot not found");
                var unplug = DuckovReflectionCache.GetMethod(slot.GetType(), "Unplug", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                             ?? DuckovReflectionCache.GetMethod(slot.GetType(), "SetContent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (unplug == null) return RichResult.Fail(ErrorCode.NotSupported, "Unplug/SetContent not found");
                if (unplug.Name == "SetContent") unplug.Invoke(slot, new object[] { null }); else unplug.Invoke(slot, null);
                NotifySlotAndChildChanged(ownerItem);
                IMKDuckov.MarkDirty(ownerItem, DirtyKind.Slots);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryUnplugFromSlot failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>在同一物品上从一个槽位移动到另一个槽位。</summary>
        public RichResult TryMoveBetweenSlots(object ownerItem, string fromSlotKey, string toSlotKey)
        {
            try
            {
                if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "owner null");
                if (string.IsNullOrEmpty(fromSlotKey) || string.IsNullOrEmpty(toSlotKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "slotKey null");
                var slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (slots == null) return RichResult.Fail(ErrorCode.NotSupported, "no Slots on owner");
                var from = ResolveSlotByKeyOrIndex(slots, fromSlotKey);
                var to = ResolveSlotByKeyOrIndex(slots, toSlotKey);
                if (from == null || to == null) return RichResult.Fail(ErrorCode.NotFound, "slot not found");
                var contentProp = DuckovReflectionCache.GetProp(from.GetType(), "Content", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var content = contentProp?.GetValue(from, null);
                if (content == null) return RichResult.Fail(ErrorCode.NotFound, "no content in from-slot");
                var unplug = DuckovReflectionCache.GetMethod(from.GetType(), "Unplug", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                             ?? DuckovReflectionCache.GetMethod(from.GetType(), "SetContent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var plug = DuckovReflectionCache.GetMethod(to.GetType(), "Plug", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                           ?? DuckovReflectionCache.GetMethod(to.GetType(), "SetContent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (unplug == null || plug == null) return RichResult.Fail(ErrorCode.NotSupported, "Unplug/Plug not found");
                if (unplug.Name == "SetContent") unplug.Invoke(from, new object[] { null }); else unplug.Invoke(from, null);
                if (plug.Name == "SetContent") plug.Invoke(to, new object[] { content }); else plug.Invoke(to, new object[] { content });
                NotifySlotAndChildChanged(ownerItem);
                IMKDuckov.MarkDirty(ownerItem, DirtyKind.Slots);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryMoveBetweenSlots failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>新增一个槽位（自动去重 Key）。</summary>
        public RichResult TryAddSlot(object ownerItem, SlotCreateOptions options)
        {
            try
            {
                if (ownerItem == null || options == null) return RichResult.Fail(ErrorCode.InvalidArgument, "null args");
                var slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (slots == null) return RichResult.Fail(ErrorCode.NotSupported, "no Slots on owner");
                var slotType = DuckovTypeUtils.FindType("ItemStatsSystem.Slot") ?? DuckovTypeUtils.FindType("Slot");
                if (slotType == null) return RichResult.Fail(ErrorCode.NotSupported, "Slot type missing");
                var desired = string.IsNullOrEmpty(options.Key) ? "Socket" : options.Key;
                var finalKey = EnsureUniqueSlotKey(slots, desired);
                var slot = Activator.CreateInstance(slotType);
                if (slot == null) return RichResult.Fail(ErrorCode.OperationFailed, "create slot failed");
                DuckovTypeUtils.SetProp(slot, "Key", finalKey);
                DuckovTypeUtils.SetProp(slot, "DisplayName", string.IsNullOrEmpty(options.DisplayName) ? finalKey : options.DisplayName);
                if (options.SlotIcon != null) DuckovTypeUtils.SetProp(slot, "SlotIcon", options.SlotIcon);
                if (options.RequireTags != null) DuckovTypeUtils.SetProp(slot, "requireTags", options.RequireTags);
                if (options.ExcludeTags != null) DuckovTypeUtils.SetProp(slot, "excludeTags", options.ExcludeTags);
                if (options.ForbidItemsWithSameID.HasValue)
                {
                    var f = DuckovReflectionCache.GetField(slotType, "forbidItemsWithSameID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null) f.SetValue(slot, options.ForbidItemsWithSameID.Value);
                }
                var init = DuckovReflectionCache.GetMethod(slotType, "Initialize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                init?.Invoke(slot, new[] { slots });
                var add = DuckovReflectionCache.GetMethod(slots.GetType(), "Add", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (add == null) return RichResult.Fail(ErrorCode.NotSupported, "SlotCollection.Add not found");
                add.Invoke(slots, new[] { slot });
                NotifySlotAndChildChanged(ownerItem);
                IMKDuckov.MarkDirty(ownerItem, DirtyKind.Slots);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryAddSlot failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>移除一个槽位（如有内容先拔出）。</summary>
        public RichResult TryRemoveSlot(object ownerItem, string slotKey)
        {
            try
            {
                if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "owner null");
                if (string.IsNullOrEmpty(slotKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "slotKey null");
                var slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (slots == null) return RichResult.Fail(ErrorCode.NotSupported, "no Slots on owner");
                var slot = ResolveSlotByKeyOrIndex(slots, slotKey);
                if (slot == null) return RichResult.Fail(ErrorCode.NotFound, "slot not found");
                var contentProp = DuckovReflectionCache.GetProp(slot.GetType(), "Content", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var content = contentProp?.GetValue(slot, null);
                if (content != null)
                {
                    var unplug = DuckovReflectionCache.GetMethod(slot.GetType(), "Unplug", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                 ?? DuckovReflectionCache.GetMethod(slot.GetType(), "SetContent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (unplug != null)
                    {
                        if (unplug.Name == "SetContent") unplug.Invoke(slot, new object[] { null }); else unplug.Invoke(slot, null);
                    }
                }
                var rem = DuckovReflectionCache.GetMethod(slots.GetType(), "Remove", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (rem == null) return RichResult.Fail(ErrorCode.NotSupported, "SlotCollection.Remove not found");
                rem.Invoke(slots, new[] { slot });
                NotifySlotAndChildChanged(ownerItem);
                IMKDuckov.MarkDirty(ownerItem, DirtyKind.Slots);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryRemoveSlot failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
    }
}
