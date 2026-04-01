using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using ItemModKit.Core;
using Newtonsoft.Json.Linq;

namespace ItemModKit.Adapters.Duckov
{
    // Simple in-memory transaction manager for per-item batched changes.
    // For safety, we snapshot core fields/variables/constants/tags, and restore on rollback.
    internal sealed class DuckovTransactionManager
    {
        private sealed class Tx
        {
            public sealed class InventoryPlacement
            {
                public object Inventory;
                public int Index;
            }

            public ItemSnapshot Snapshot;
            public VariableEntry[] Constants;
            public DateTime StartedAt;
            public bool HasInspectionState;
            public bool Inspected;
            public Dictionary<string, object> SlotContents = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<int, InventoryPlacement> InventoryPlacements = new Dictionary<int, InventoryPlacement>();
            public Dictionary<string, object> ExtensionState = new Dictionary<string, object>(StringComparer.Ordinal);
        }
        private readonly ConcurrentDictionary<(int, string), Tx> _map = new ConcurrentDictionary<(int, string), Tx>();

        private static int GetItemId(object item) => DuckovTypeUtils.GetStableId(item);

        public string Begin(IItemAdapter adapter, object item)
        {
            var token = Guid.NewGuid().ToString("N");
            var id = GetItemId(item);
            var snap = ItemSnapshot.Capture(adapter, item);
            var tx = new Tx
            {
                Snapshot = snap,
                Constants = adapter.GetConstants(item) ?? Array.Empty<VariableEntry>(),
                StartedAt = DateTime.UtcNow
            };
            if (TryReadInspectedState(adapter, item, out var inspected))
            {
                tx.HasInspectionState = true;
                tx.Inspected = inspected;
            }

            CaptureSlotContents(item, tx);
            CaptureInventoryPlacements(item, tx);
            CaptureExtensionState(item, snap, tx);

            _map[(id, token)] = tx;
            return token;
        }
        public bool TryGet(object item, string token, out ItemSnapshot snap)
        {
            Tx tx; var ok = _map.TryGetValue((GetItemId(item), token), out tx);
            snap = ok ? tx.Snapshot : null; return ok;
        }
        public bool Commit(object item, string token)
        {
            return _map.TryRemove((GetItemId(item), token), out _);
        }
        public bool Rollback(IItemAdapter adapter, IWriteService writer, object item, string token)
        {
            Tx tx; if (!_map.TryRemove((GetItemId(item), token), out tx) || tx.Snapshot == null) return false;
            // Restore snapshot minimums: core fields, variables, constants, tags
            var snap = tx.Snapshot;
            writer.TryWriteCoreFields(item, new CoreFieldChanges { Name = snap.NameRaw, RawName = snap.NameRaw, TypeId = snap.TypeId, Quality = snap.Quality, DisplayQuality = snap.DisplayQuality, Value = snap.Value });
            // variables
            var snapshotVarKeys = new HashSet<string>(StringComparer.Ordinal);
            var vars = new List<KeyValuePair<string, object>>();
            foreach (var v in snap.Variables)
            {
                snapshotVarKeys.Add(v.Key);
                vars.Add(new KeyValuePair<string, object>(v.Key, v.Value));
            }

            var currentVars = adapter.GetVariables(item);
            if (currentVars != null)
            {
                foreach (var current in currentVars)
                {
                    if (!string.IsNullOrEmpty(current.Key) && !snapshotVarKeys.Contains(current.Key))
                    {
                        adapter.RemoveVariable(item, current.Key);
                    }
                }
            }

            writer.TryWriteVariables(item, (IEnumerable<KeyValuePair<string, object>>)vars, true);
            if (tx.HasInspectionState)
            {
                writer.TrySetInspected(item, tx.Inspected);
            }
            // tags
            writer.TryWriteTags(item, (IEnumerable<string>)(snap.Tags ?? Array.Empty<string>()), false);
            // constants
            var snapshotConstKeys = new HashSet<string>(StringComparer.Ordinal);
            var consts = new List<KeyValuePair<string, object>>();
            foreach (var c in tx.Constants ?? Array.Empty<VariableEntry>())
            {
                if (string.IsNullOrEmpty(c.Key))
                {
                    continue;
                }

                snapshotConstKeys.Add(c.Key);
                consts.Add(new KeyValuePair<string, object>(c.Key, c.Value));
            }

            var currentConsts = adapter.GetConstants(item);
            if (currentConsts != null)
            {
                foreach (var current in currentConsts)
                {
                    if (!string.IsNullOrEmpty(current.Key) && !snapshotConstKeys.Contains(current.Key))
                    {
                        if (string.Equals(current.Key, EngineKeys.Constant.MaxDurability, StringComparison.Ordinal))
                        {
                            writer.TrySetMaxDurability(item, 0f);
                        }

                        adapter.RemoveConstant(item, current.Key);
                    }
                }
            }

            if (consts.Count > 0)
            {
                writer.TryWriteConstants(item, consts, true);
            }

            RestoreSlotCollection(adapter, writer, item, snap, tx);
            RestoreExtensionState(item, tx);
            return true;
        }

        private static void CaptureExtensionState(object item, ItemSnapshot snapshot, Tx tx)
        {
            if (item == null || tx == null)
            {
                return;
            }

            try
            {
                ItemStateExtensions.Contribute(item, snapshot, DirtyKind.Stats | DirtyKind.Modifiers | DirtyKind.Effects, tx.ExtensionState);
            }
            catch
            {
            }
        }

        private static void RestoreExtensionState(object item, Tx tx)
        {
            if (item == null || tx == null || tx.ExtensionState == null || tx.ExtensionState.Count == 0)
            {
                return;
            }

            try
            {
                var meta = new ItemMeta
                {
                    EmbeddedJson = JObject.FromObject(tx.ExtensionState).ToString()
                };
                ItemStateExtensions.TryApply(item, meta);
            }
            catch
            {
            }
        }

        private static void RestoreSlotCollection(IItemAdapter adapter, IWriteService writer, object item, ItemSnapshot snap, Tx tx)
        {
            try
            {
                var snapshotSlots = snap?.Slots ?? Array.Empty<SlotEntry>();
                var currentSlots = adapter.GetSlots(item) ?? Array.Empty<SlotEntry>();
                var snapshotByKey = new Dictionary<string, SlotEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var slot in snapshotSlots)
                {
                    if (!string.IsNullOrEmpty(slot.Key))
                    {
                        snapshotByKey[slot.Key] = slot;
                    }
                }

                foreach (var current in currentSlots)
                {
                    if (string.IsNullOrEmpty(current.Key) || snapshotByKey.ContainsKey(current.Key))
                    {
                        continue;
                    }

                    writer.TryRemoveSlot(item, current.Key);
                }

                currentSlots = adapter.GetSlots(item) ?? Array.Empty<SlotEntry>();
                var currentByKey = new Dictionary<string, SlotEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var slot in currentSlots)
                {
                    if (!string.IsNullOrEmpty(slot.Key))
                    {
                        currentByKey[slot.Key] = slot;
                    }
                }

                foreach (var snapshot in snapshotSlots)
                {
                    if (string.IsNullOrEmpty(snapshot.Key))
                    {
                        continue;
                    }

                    if (!currentByKey.ContainsKey(snapshot.Key))
                    {
                        writer.TryAddSlot(item, new SlotCreateOptions
                        {
                            Key = snapshot.Key,
                            SlotIcon = snapshot.SlotIcon,
                            RequireTags = snapshot.RequireTagKeys,
                            ExcludeTags = snapshot.ExcludeTagKeys,
                            ForbidItemsWithSameID = snapshot.ForbidSameID,
                        });
                        continue;
                    }

                    var current = currentByKey[snapshot.Key];
                    if (snapshot.Occupied && !current.Occupied && tx != null && tx.SlotContents.TryGetValue(snapshot.Key, out var originalContent) && originalContent != null)
                    {
                        writer.TryPlugIntoSlot(item, snapshot.Key, originalContent);
                        continue;
                    }

                    if (!snapshot.Occupied && current.Occupied)
                    {
                        var currentContent = TryGetCurrentSlotContent(item, snapshot.Key);
                        writer.TryUnplugFromSlot(item, snapshot.Key);
                        RestoreInventoryPlacement(currentContent, tx);
                    }
                }
            }
            catch
            {
            }
        }

        private static bool TryReadInspectedState(IItemAdapter adapter, object item, out bool inspected)
        {
            inspected = false;
            if (item == null) return false;

            try
            {
                var value = adapter?.GetVariable(item, EngineKeys.Variable.Inspected);
                if (value is bool boolValue)
                {
                    inspected = boolValue;
                    return true;
                }

                if (value != null)
                {
                    inspected = Convert.ToBoolean(value);
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                var getter = item.GetType().GetProperty(EngineKeys.Property.Inspected, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetGetMethod(true);
                if (getter != null)
                {
                    var value = getter.Invoke(item, null);
                    if (value != null)
                    {
                        inspected = Convert.ToBoolean(value);
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static void CaptureSlotContents(object item, Tx tx)
        {
            if (item == null || tx == null)
            {
                return;
            }

            try
            {
                var slots = item.GetType().GetProperty("Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(item, null) as System.Collections.IEnumerable;
                if (slots == null)
                {
                    return;
                }

                foreach (var slot in slots)
                {
                    if (slot == null)
                    {
                        continue;
                    }

                    var key = slot.GetType().GetProperty("Key", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(slot, null) as string;
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }

                    var content = slot.GetType().GetProperty("Content", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(slot, null);
                    if (content != null)
                    {
                        tx.SlotContents[key] = content;
                    }
                }
            }
            catch
            {
            }
        }

        private static void CaptureInventoryPlacements(object item, Tx tx)
        {
            if (item == null || tx == null)
            {
                return;
            }

            try
            {
                CaptureInventoryPlacementsFromInventory(GetMemberValue(item, "Inventory"), tx);
                var parentItem = GetMemberValue(item, "ParentItem");
                CaptureInventoryPlacementsFromInventory(GetMemberValue(parentItem, "Inventory"), tx);
            }
            catch
            {
            }
        }

        private static void CaptureInventoryPlacementsFromInventory(object inventory, Tx tx)
        {
            if (inventory == null || tx == null)
            {
                return;
            }

            try
            {
                var capacity = IMKDuckov.Inventory.GetCapacity(inventory);
                for (var index = 0; index < capacity; index++)
                {
                    var child = IMKDuckov.Inventory.GetItemAt(inventory, index);
                    if (child == null)
                    {
                        continue;
                    }

                    var childId = GetItemId(child);
                    if (!tx.InventoryPlacements.ContainsKey(childId))
                    {
                        tx.InventoryPlacements[childId] = new Tx.InventoryPlacement
                        {
                            Inventory = inventory,
                            Index = index,
                        };
                    }
                }
            }
            catch
            {
            }
        }

        private static object TryGetCurrentSlotContent(object item, string slotKey)
        {
            try
            {
                var slots = GetMemberValue(item, "Slots") as System.Collections.IEnumerable;
                if (slots == null)
                {
                    return null;
                }

                foreach (var slot in slots)
                {
                    if (slot == null)
                    {
                        continue;
                    }

                    var key = GetMemberValue(slot, "Key") as string;
                    if (!string.Equals(key, slotKey, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    return GetMemberValue(slot, "Content");
                }
            }
            catch
            {
            }

            return null;
        }

        private static void RestoreInventoryPlacement(object item, Tx tx)
        {
            if (item == null || tx == null)
            {
                return;
            }

            try
            {
                if (!tx.InventoryPlacements.TryGetValue(GetItemId(item), out var placement) || placement?.Inventory == null)
                {
                    return;
                }

                var currentInventory = IMKDuckov.Inventory.GetInventory(item);
                if (ReferenceEquals(currentInventory, placement.Inventory))
                {
                    var currentIndex = IMKDuckov.Inventory.IndexOf(placement.Inventory, item);
                    if (currentIndex == placement.Index)
                    {
                        return;
                    }

                    if (currentIndex >= 0)
                    {
                        IMKDuckov.Mover.TryMoveInInventory(placement.Inventory, currentIndex, placement.Index);
                        return;
                    }
                }

                IMKDuckov.Mover.TryAddToInventory(item, placement.Inventory, placement.Index, allowMerge: false);
            }
            catch
            {
            }
        }

        private static object GetMemberValue(object instance, string memberName)
        {
            if (instance == null)
            {
                return null;
            }

            try
            {
                var property = instance.GetType().GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null)
                {
                    return property.GetValue(instance, null);
                }
            }
            catch
            {
            }

            try
            {
                var field = instance.GetType().GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return field?.GetValue(instance);
            }
            catch
            {
                return null;
            }
        }
    }
}
