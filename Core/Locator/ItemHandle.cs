using System;

namespace ItemModKit.Core.Locator
{
    internal sealed class ItemHandle : IItemHandle
    {
        private readonly Func<object> _resolver; // late-bound runtime object
        public int? InstanceId { get; }
        public string LogicalId { get; private set; }

        public ItemHandle(Func<object> resolver, int? instanceId = null, string logicalId = null)
        {
            _resolver = resolver ?? (() => null);
            InstanceId = instanceId;
            LogicalId = logicalId ?? (instanceId?.ToString());
        }

        public bool IsAlive => TryGetRaw() != null;
        public object TryGetRaw() { try { return _resolver(); } catch { return null; } }
        public override string ToString() => $"ItemHandle(iid={InstanceId}, lid={LogicalId})";

        public void RebindLogical(string newId)
        {
            if (!string.IsNullOrEmpty(newId)) LogicalId = newId;
        }

        private int _typeId; private bool _typeInit;
        private string _name; private bool _nameInit;
        private string[] _tags; private bool _tagsInit;
        public ItemHandle(Func<object> resolver, int? instanceId = null, string logicalId = null, int preTypeId = 0, string preName = null, string[] preTags = null)
        {
            _resolver = resolver ?? (() => null);
            InstanceId = instanceId;
            LogicalId = logicalId ?? (instanceId?.ToString());
            if (preTypeId != 0) { _typeId = preTypeId; _typeInit = true; }
            if (!string.IsNullOrEmpty(preName)) { _name = preName; _nameInit = true; }
            if (preTags != null) { _tags = preTags; _tagsInit = true; }
        }
        public int TypeId { get { if (!_typeInit) { _typeInit = true; try { var raw = TryGetRaw(); _typeId = raw?.GetType().GetProperty("TypeID")?.GetValue(raw, null) is int v ? v : 0; } catch { _typeId = 0; } } return _typeId; } }
        public string DisplayName { get { if (!_nameInit) { _nameInit = true; try { var raw = TryGetRaw(); _name = raw?.GetType().GetProperty("DisplayName")?.GetValue(raw, null) as string; } catch { _name = null; } } return _name; } }
        public string[] Tags { get { if (!_tagsInit) { _tagsInit = true; try { var raw = TryGetRaw(); var tg = raw?.GetType().GetProperty("Tags")?.GetValue(raw, null) as System.Collections.IEnumerable; if (tg != null) { var list = new System.Collections.Generic.List<string>(); foreach (var x in tg) if (x != null) list.Add(x.ToString()); _tags = list.ToArray(); } else _tags = System.Array.Empty<string>(); } catch { _tags = System.Array.Empty<string>(); } } return _tags ?? System.Array.Empty<string>(); } }

        public void RefreshMetadata()
        {
            _typeInit = false; _nameInit = false; _tagsInit = false;
            var _ = TypeId; var __ = DisplayName; var ___ = Tags;
        }
    }
}
