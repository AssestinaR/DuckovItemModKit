using System;
using System.Collections.Generic;
using ItemModKit;
using ItemModKit.Adapters.Duckov;
using ItemModKit.Core.Locator;

namespace ItemModKit.Adapters.Duckov.Locator
{
    /// <summary>
    /// Item 定位器（无 Harmony 版本）。维护弱引用索引并提供句柄创建。
    /// </summary>
    public sealed class DuckovItemLocator : IItemLocator, IItemIndex
    {
        private readonly Dictionary<int, WeakReference> _byInstance = new Dictionary<int, WeakReference>();
        private readonly IInventoryClassifier _classifier;
        private IItemHandle _lastCreated;

        public DuckovItemLocator(IInventoryClassifier classifier) { _classifier = classifier; }

        public IItemHandle FromInstance(object raw)
        {
            if (raw == null) return null;
            int? iid = TryGetInstanceId(raw);
            return new ItemHandle(() => TryResolveByInstanceId(iid), iid, null);
        }

        public IItemHandle FromInstanceId(int instanceId)
        {
            return new ItemHandle(() => TryResolveByInstanceId(instanceId), instanceId, null);
        }

        public IItemHandle FromLogicalId(string id)
        {
            return new ItemHandle(() => null, null, id);
        }

        public IItemHandle FromUISelection()
        {
            try
            {
                var selType = DuckovTypeUtils.FindType("ItemModKit.Adapters.Duckov.DuckovUISelection");
                var cur = selType?.GetProperty("CurrentItem", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null, null);
                return FromInstance(cur);
            }
            catch { return null; }
        }

        public IItemHandle LastCreated() => _lastCreated;

        public IItemHandle[] Query(object predicate = null, IItemScope scope = null)
        {
            var list = new List<IItemHandle>();
            foreach (var kv in _byInstance)
            {
                var obj = kv.Value.Target;
                if (obj == null) continue;
                if (scope != null && !scope.Includes(obj, TryGetInventory(obj), TryGetOwner(obj))) continue;
                list.Add(new ItemHandle(() => kv.Value.Target, kv.Key, null));
            }
            return list.ToArray();
        }

        public void OnCreated(object raw)
        {
            int? iid = TryGetInstanceId(raw);
            if (iid != null)
            {
                _byInstance[iid.Value] = new WeakReference(raw);
                _lastCreated = new ItemHandle(() => TryResolveByInstanceId(iid.Value), iid, null);
            }
        }

        public void OnDestroyed(object raw)
        {
            int? iid = TryGetInstanceId(raw);
            if (iid != null) _byInstance.Remove(iid.Value);
        }

        public void OnMoved(object raw, object newContainer = null)
        {
            // Placeholder: Could update secondary indexes if implemented
        }

        public IItemHandle FindByInstanceId(int instanceId) => FromInstanceId(instanceId);
        public IItemHandle[] FindAllByTypeId(int typeId) { return Array.Empty<IItemHandle>(); }

        private object TryResolveByInstanceId(int? iid)
        {
            if (iid == null) return null;
            if (_byInstance.TryGetValue(iid.Value, out var wr))
            {
                if (wr.Target != null) return wr.Target;
            }
            return null;
        }

        private static int? TryGetInstanceId(object raw)
        {
            try
            {
                var m = raw.GetType().GetMethod("GetInstanceID", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (m != null)
                {
                    var v = m.Invoke(raw, null);
                    if (v is int i) return i;
                    try { return Convert.ToInt32(v); } catch { }
                }
            }
            catch { }
            return null;
        }

        private static object TryGetInventory(object item)
        {
            try { return item?.GetType().GetProperty("Inventory")?.GetValue(item, null); } catch { return null; }
        }
        private static object TryGetOwner(object item)
        {
            try { return item?.GetType().GetProperty("ParentItem")?.GetValue(item, null); } catch { return null; }
        }
    }
}
