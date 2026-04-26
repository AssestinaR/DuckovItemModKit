using System;
using System.Collections.Generic;
using ItemModKit.Core.Locator;

namespace ItemModKit.Adapters.Duckov.Locator
{
    internal sealed class DuckovLogicalIdMap : ILogicalIdMap
    {
        private readonly Dictionary<string,IItemHandle> _byLogical = new Dictionary<string,IItemHandle>(StringComparer.Ordinal);
        private readonly Dictionary<int,string> _byInstance = new Dictionary<int,string>();
        public void Bind(IItemHandle oldItem, IItemHandle newItem)
        {
            if (newItem == null) return;
            var lid = newItem.LogicalId;
            if (string.IsNullOrEmpty(lid)) lid = newItem.InstanceId?.ToString();
            if (string.IsNullOrEmpty(lid)) return;
            _byLogical[lid] = newItem;
            if (newItem.InstanceId.HasValue) _byInstance[newItem.InstanceId.Value] = lid;
        }
        public IItemHandle Resolve(string logicalId)
        {
            if (string.IsNullOrEmpty(logicalId)) return null;
            _byLogical.TryGetValue(logicalId, out var h); return h;
        }
        public bool TryGetLogicalId(IItemHandle item, out string logicalId)
        {
            logicalId = null;
            if (item == null) return false;
            var lid = item.LogicalId;
            if (string.IsNullOrEmpty(lid) && item.InstanceId.HasValue && _byInstance.TryGetValue(item.InstanceId.Value, out lid))
            {
                item.RebindLogical(lid);
            }
            if (!string.IsNullOrEmpty(lid)) { logicalId = lid; return true; }
            return false;
        }
        public void Unbind(IItemHandle item)
        {
            if (item == null) return;
            string lid; if (!TryGetLogicalId(item, out lid)) return;
            if (!string.IsNullOrEmpty(lid)) _byLogical.Remove(lid);
            if (item.InstanceId.HasValue) _byInstance.Remove(item.InstanceId.Value);
        }
    }
}
