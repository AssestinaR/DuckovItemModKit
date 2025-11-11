using System;
using System.Reflection;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    internal sealed partial class WriteService : IWriteService
    {
        // Convenience setters (stack/durability/inspection)
        public RichResult TrySetStackCount(object item, int count)
        {
            try { return TryWriteVariables(item, new[] { new System.Collections.Generic.KeyValuePair<string, object>(EngineKeys.Variable.Count, count) }, true); }
            catch (Exception ex) { Log.Error("TrySetStackCount failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        public RichResult TrySetMaxStack(object item, int max)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var setter = DuckovReflectionCache.GetSetter(item.GetType(), EngineKeys.Property.MaxStackCount, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (setter == null) return RichResult.Fail(ErrorCode.NotSupported, "MaxStackCount setter missing");
                setter(item, max);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TrySetMaxStack failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        public RichResult TrySetDurability(object item, float value)
        {
            try { return TryWriteVariables(item, new[] { new System.Collections.Generic.KeyValuePair<string, object>(EngineKeys.Variable.Durability, value) }, true); }
            catch (Exception ex) { Log.Error("TrySetDurability failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        public RichResult TrySetMaxDurability(object item, float value)
        {
            try { return TryWriteConstants(item, new[] { new System.Collections.Generic.KeyValuePair<string, object>(EngineKeys.Constant.MaxDurability, value) }, true); }
            catch (Exception ex) { Log.Error("TrySetMaxDurability failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        public RichResult TrySetDurabilityLoss(object item, float value)
        {
            try { return TryWriteVariables(item, new[] { new System.Collections.Generic.KeyValuePair<string, object>(EngineKeys.Variable.DurabilityLoss, value) }, true); }
            catch (Exception ex) { Log.Error("TrySetDurabilityLoss failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        public RichResult TrySetInspected(object item, bool inspected)
        {
            try { return TryWriteVariables(item, new[] { new System.Collections.Generic.KeyValuePair<string, object>(EngineKeys.Variable.Inspected, inspected) }, true); }
            catch (Exception ex) { Log.Error("TrySetInspected failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        public RichResult TrySetInspecting(object item, bool inspecting)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var setter = DuckovReflectionCache.GetSetter(item.GetType(), EngineKeys.Property.Inspecting, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (setter == null) return RichResult.Fail(ErrorCode.NotSupported, "Inspecting setter missing");
                setter(item, inspecting);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TrySetInspecting failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        // Child inventory add/remove
        public RichResult TryAddToChildInventory(object item, object childItem, int? index1Based = null, bool allowMerge = true)
        {
            try
            {
                if (item == null || childItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "null args");
                var inv = DuckovReflectionCache.GetGetter(item.GetType(), "Inventory", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item);
                if (inv == null) return RichResult.Fail(ErrorCode.OperationFailed, "no inventory");
                var addAndMerge = DuckovReflectionCache.GetMethod(inv.GetType(), "AddAndMerge", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (addAndMerge != null && allowMerge)
                {
                    var r = addAndMerge.Invoke(inv, new object[] { childItem, 0 });
                    bool ok = r is bool b ? b : true; return ok ? RichResult.Success() : RichResult.Fail(ErrorCode.OperationFailed, "add failed");
                }
                if (index1Based.HasValue)
                {
                    var addAt = DuckovReflectionCache.GetMethod(inv.GetType(), "AddAt", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                    if (addAt != null)
                    {
                        var r = addAt.Invoke(inv, new object[] { childItem, Math.Max(0, index1Based.Value - 1) });
                        bool ok = r is bool b2 ? b2 : true; return ok ? RichResult.Success() : RichResult.Fail(ErrorCode.OperationFailed, "addAt failed");
                    }
                }
                var addItem = DuckovReflectionCache.GetMethod(inv.GetType(), "AddItem", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (addItem != null)
                {
                    var r = addItem.Invoke(inv, new object[] { childItem, false });
                    bool ok = r is bool b3 ? b3 : true; return ok ? RichResult.Success() : RichResult.Fail(ErrorCode.OperationFailed, "addItem failed");
                }
                return RichResult.Fail(ErrorCode.NotSupported, "no suitable method");
            }
            catch (Exception ex) { Log.Error("TryAddToChildInventory failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        public RichResult TryRemoveFromChildInventory(object item, object childItem)
        {
            try
            {
                if (item == null || childItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "null args");
                var inv = DuckovReflectionCache.GetGetter(item.GetType(), "Inventory", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item);
                if (inv == null) return RichResult.Fail(ErrorCode.OperationFailed, "no inventory");
                var m = DuckovReflectionCache.GetMethod(inv.GetType(), "RemoveItem", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (m != null) { m.Invoke(inv, new object[] { childItem }); return RichResult.Success(); }
                return RichResult.Fail(ErrorCode.NotSupported, "no remove method");
            }
            catch (Exception ex) { Log.Error("TryRemoveFromChildInventory failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        public RichResult TryMoveInChildInventory(object item, int fromIndex, int toIndex)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item null");
                var inv = DuckovReflectionCache.GetGetter(item.GetType(), "Inventory", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item);
                if (inv == null) return RichResult.Fail(ErrorCode.NotSupported, "no inventory");
                var getItem = DuckovReflectionCache.GetMethod(inv.GetType(), "get_Item", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (getItem == null) return RichResult.Fail(ErrorCode.NotSupported, "indexer not found");
                var from = getItem.Invoke(inv, new object[]{ fromIndex });
                if (from == null) return RichResult.Fail(ErrorCode.NotFound, "from null");
                var remove = DuckovReflectionCache.GetMethod(inv.GetType(), "RemoveItem", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                var addAt = DuckovReflectionCache.GetMethod(inv.GetType(), "AddAt", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (remove == null || addAt == null) return RichResult.Fail(ErrorCode.NotSupported, "remove/addAt missing");
                remove.Invoke(inv, new[]{ from });
                addAt.Invoke(inv, new[]{ from, toIndex });
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryMoveInChildInventory failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        public RichResult TryClearChildInventory(object item)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item null");
                var inv = DuckovReflectionCache.GetGetter(item.GetType(), "Inventory", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item);
                if (inv == null) return RichResult.Fail(ErrorCode.NotSupported, "no inventory");
                var getCap = DuckovReflectionCache.GetGetter(inv.GetType(), "Capacity", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                var remove = DuckovReflectionCache.GetMethod(inv.GetType(), "RemoveItem", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                int cap = Convert.ToInt32(getCap?.Invoke(inv) ?? 0);
                for (int i=0;i<cap;i++)
                {
                    var it = DuckovReflectionCache.GetMethod(inv.GetType(), "get_Item", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(inv, new object[]{ i });
                    if (it != null) remove?.Invoke(inv, new[]{ it });
                }
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryClearChildInventory failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        public RichResult TrySetVariableMeta(object ownerItem, string key, bool? display = null, string displayName = null, string description = null)
        {
            try
            {
                if (ownerItem == null || string.IsNullOrEmpty(key)) return RichResult.Fail(ErrorCode.InvalidArgument, "args");
                var vars = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Variables", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(ownerItem);
                if (vars == null) return RichResult.Fail(ErrorCode.NotSupported, "no Variables");
                var getEntry = DuckovReflectionCache.GetMethod(vars.GetType(), "GetEntry", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                var entry = getEntry?.Invoke(vars, new object[]{ key });
                if (entry == null) return RichResult.Fail(ErrorCode.NotFound, "entry not found");
                if (display.HasValue) DuckovReflectionCache.GetSetter(entry.GetType(), "Display", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(entry, display.Value);
                if (!string.IsNullOrEmpty(displayName)) DuckovReflectionCache.GetSetter(entry.GetType(), "DisplayName", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(entry, displayName);
                if (!string.IsNullOrEmpty(description)) DuckovReflectionCache.GetSetter(entry.GetType(), "Description", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(entry, description);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TrySetVariableMeta failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        public RichResult TrySetConstantMeta(object ownerItem, string key, bool? display = null, string displayName = null, string description = null)
        {
            try
            {
                if (ownerItem == null || string.IsNullOrEmpty(key)) return RichResult.Fail(ErrorCode.InvalidArgument, "args");
                var cons = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Constants", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(ownerItem);
                if (cons == null) return RichResult.Fail(ErrorCode.NotSupported, "no Constants");
                var getEntry = DuckovReflectionCache.GetMethod(cons.GetType(), "GetEntry", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                var entry = getEntry?.Invoke(cons, new object[]{ key });
                if (entry == null) return RichResult.Fail(ErrorCode.NotFound, "entry not found");
                if (display.HasValue) DuckovReflectionCache.GetSetter(entry.GetType(), "Display", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(entry, display.Value);
                if (!string.IsNullOrEmpty(displayName)) DuckovReflectionCache.GetSetter(entry.GetType(), "DisplayName", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(entry, displayName);
                if (!string.IsNullOrEmpty(description)) DuckovReflectionCache.GetSetter(entry.GetType(), "Description", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(entry, description);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TrySetConstantMeta failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
    }
}
