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

        private static object ResolveSlot(object slots, string slotKey)
        {
            if (slots == null || string.IsNullOrEmpty(slotKey)) return null;
            var t = slots.GetType();
            // Prefer GetSlot(string)
            var getSlotStr = DuckovReflectionCache.GetMethod(t, "GetSlot", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(string) });
            if (getSlotStr != null)
            {
                try { var v = getSlotStr.Invoke(slots, new object[] { slotKey }); if (v != null) return v; } catch { }
            }
            // Fallback enumeration
            try
            {
                var enumerable = slots as System.Collections.IEnumerable;
                if (enumerable != null)
                {
                    foreach (var s in enumerable)
                    {
                        if (s == null) continue;
                        var keyP = DuckovReflectionCache.GetProp(s.GetType(), "Key", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        var keyV = keyP?.GetValue(s, null) as string;
                        if (!string.IsNullOrEmpty(keyV) && string.Equals(keyV, slotKey, StringComparison.OrdinalIgnoreCase)) return s;
                    }
                }
            }
            catch { }
            return null;
        }

        private static bool TryGetSlotContent(object slot, out object content)
        {
            content = null;
            if (slot == null) return false;
            try
            {
                var cp = DuckovReflectionCache.GetProp(slot.GetType(), "Content", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                content = cp?.GetValue(slot, null);
                return content != null;
            }
            catch { return false; }
        }

        private static bool CanPlug(object slot, object childItem)
        {
            try
            {
                var m = DuckovReflectionCache.GetMethod(slot.GetType(), "CanPlug", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { childItem?.GetType() });
                if (m != null)
                {
                    var r = m.Invoke(slot, new[] { childItem });
                    if (r is bool b) return b;
                }
            }
            catch { }
            return true; // assume allowed if we cannot check
        }

        private static (MethodInfo plug, bool hasOutPrev) ResolvePlugMethod(object slot)
        {
            var st = slot.GetType();
            // Look for bool Plug(Item item, out Item previous)
            try
            {
                var methods = st.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var m in methods)
                {
                    if (m.Name != "Plug") continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 2 && ps[1].IsOut)
                    {
                        if (m.ReturnType == typeof(bool)) return (m, true);
                    }
                    if (ps.Length == 1 && m.ReturnType == typeof(bool)) return (m, false);
                }
            }
            catch { }
            return (null, false);
        }

        /// <summary>向槽位插入子物品。</summary>
        public RichResult TryPlugIntoSlot(object ownerItem, string slotKey, object childItem)
        {
            try
            {
                if (ownerItem == null || childItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.args");
                if (string.IsNullOrEmpty(slotKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.key");
                var slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (slots == null) return RichResult.Fail(ErrorCode.NotSupported, "slot.owner.no_slots");
                var slot = ResolveSlot(slots, slotKey);
                if (slot == null) return RichResult.Fail(ErrorCode.NotFound, "slot.notfound");
                if (!CanPlug(slot, childItem)) return RichResult.Fail(ErrorCode.Conflict, "slot.incompatible");
                var (plug, hasOutPrev) = ResolvePlugMethod(slot);
                if (plug == null) return RichResult.Fail(ErrorCode.NotSupported, "slot.no_plug");
                object prevOut = null;
                bool result;
                if (hasOutPrev)
                {
                    var parameters = new object[] { childItem, null };
                    result = (bool)plug.Invoke(slot, parameters);
                    prevOut = parameters[1];
                }
                else
                {
                    var r = plug.Invoke(slot, new object[] { childItem });
                    result = r is bool b && b;
                }
                if (!result) return RichResult.Fail(ErrorCode.OperationFailed, "slot.plug.failed");
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
                if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.owner");
                if (string.IsNullOrEmpty(slotKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.key");
                var slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (slots == null) return RichResult.Fail(ErrorCode.NotSupported, "slot.owner.no_slots");
                var slot = ResolveSlot(slots, slotKey);
                if (slot == null) return RichResult.Fail(ErrorCode.NotFound, "slot.notfound");
                var unplug = DuckovReflectionCache.GetMethod(slot.GetType(), "Unplug", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (unplug == null) return RichResult.Fail(ErrorCode.NotSupported, "slot.no_unplug");
                unplug.Invoke(slot, null);
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
                if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.owner");
                if (string.IsNullOrEmpty(fromSlotKey) || string.IsNullOrEmpty(toSlotKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.key");
                var slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (slots == null) return RichResult.Fail(ErrorCode.NotSupported, "slot.owner.no_slots");
                var from = ResolveSlot(slots, fromSlotKey);
                var to = ResolveSlot(slots, toSlotKey);
                if (from == null || to == null) return RichResult.Fail(ErrorCode.NotFound, "slot.notfound");
                if (!TryGetSlotContent(from, out var content)) return RichResult.Fail(ErrorCode.NotFound, "slot.from.empty");
                if (!CanPlug(to, content)) return RichResult.Fail(ErrorCode.Conflict, "slot.to.incompatible");
                // Unplug source
                var unplug = DuckovReflectionCache.GetMethod(from.GetType(), "Unplug", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (unplug == null) return RichResult.Fail(ErrorCode.NotSupported, "slot.from.no_unplug");
                unplug.Invoke(from, null);
                // Plug target
                var (plug, hasOutPrev) = ResolvePlugMethod(to); if (plug == null) return RichResult.Fail(ErrorCode.NotSupported, "slot.to.no_plug");
                bool ok;
                if (hasOutPrev)
                {
                    var parameters = new object[] { content, null }; ok = (bool)plug.Invoke(to, parameters); // ignore previous replaced item for move
                }
                else
                {
                    var r = plug.Invoke(to, new[] { content }); ok = r is bool b && b;
                }
                if (!ok) return RichResult.Fail(ErrorCode.OperationFailed, "slot.move.plug_failed");
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
                // DisplayName in game derives from requireTags[0], avoid forcing here.
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
                var slot = ResolveSlot(slots, slotKey);
                if (slot == null) return RichResult.Fail(ErrorCode.NotFound, "slot not found");
                var contentProp = DuckovReflectionCache.GetProp(slot.GetType(), "Content", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var content = contentProp?.GetValue(slot, null);
                if (content != null)
                {
                    var unplug = DuckovReflectionCache.GetMethod(slot.GetType(), "Unplug", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                 ;
                    if (unplug != null)
                    {
                        unplug.Invoke(slot, null);
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

        /// <summary>设置槽位图标（仅空槽或强制覆盖）。</summary>
        public RichResult TrySetSlotIcon(object ownerItem, string slotKey, object sprite)
        {
            try
            {
                if (ownerItem == null || string.IsNullOrEmpty(slotKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.args");
                var slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (slots == null) return RichResult.Fail(ErrorCode.NotSupported, "slot.owner.no_slots");
                var slot = ResolveSlot(slots, slotKey); if (slot == null) return RichResult.Fail(ErrorCode.NotFound, "slot.notfound");
                DuckovTypeUtils.SetProp(slot, "SlotIcon", sprite);
                NotifySlotAndChildChanged(ownerItem); IMKDuckov.MarkDirty(ownerItem, DirtyKind.Slots);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TrySetSlotIcon failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        /// <summary>设置槽位标签限制（仅当槽未占用）。</summary>
        public RichResult TrySetSlotTags(object ownerItem, string slotKey, string[] requireTagKeys, string[] excludeTagKeys)
        {
            try
            {
                if (ownerItem == null || string.IsNullOrEmpty(slotKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.args");
                var slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (slots == null) return RichResult.Fail(ErrorCode.NotSupported, "slot.owner.no_slots");
                var slot = ResolveSlot(slots, slotKey); if (slot == null) return RichResult.Fail(ErrorCode.NotFound, "slot.notfound");
                if (TryGetSlotContent(slot, out _)) return RichResult.Fail(ErrorCode.Conflict, "slot.occupied");
                DuckovTypeUtils.SetProp(slot, "requireTags", requireTagKeys);
                DuckovTypeUtils.SetProp(slot, "excludeTags", excludeTagKeys);
                NotifySlotAndChildChanged(ownerItem); IMKDuckov.MarkDirty(ownerItem, DirtyKind.Slots);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TrySetSlotTags failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
    }
}
