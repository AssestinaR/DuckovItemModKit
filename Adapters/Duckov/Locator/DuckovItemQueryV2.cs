using System;
using System.Collections.Generic;
using System.Diagnostics;
using ItemModKit.Core.Locator;

namespace ItemModKit.Adapters.Duckov.Locator
{
    internal sealed class DuckovItemQueryV2 : IItemQuery
    {
        // Predicates
        private readonly List<Func<IItemHandle,bool>> _preds = new List<Func<IItemHandle,bool>>();
        // Indexes
        private readonly List<IItemHandle> _all = new List<IItemHandle>();
        private readonly Dictionary<int,List<IItemHandle>> _byType = new Dictionary<int,List<IItemHandle>>();
        private readonly Dictionary<object,List<IItemHandle>> _byInventory = new Dictionary<object,List<IItemHandle>>();
        private readonly Dictionary<string,List<IItemHandle>> _byTag = new Dictionary<string,List<IItemHandle>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<IItemHandle> _equipped = new List<IItemHandle>();
        // Caches
        private readonly Dictionary<IItemHandle,int> _depthCache = new Dictionary<IItemHandle,int>();
        private readonly Dictionary<IItemHandle,List<int?>> _ancestorChain = new Dictionary<IItemHandle,List<int?>>();
        private readonly Dictionary<string,List<IItemHandle>> _nameIndex = new Dictionary<string,List<IItemHandle>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string,List<IItemHandle>> _nameWordIndex = new Dictionary<string,List<IItemHandle>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string,List<IItemHandle>> _nameQueryCache = new Dictionary<string,List<IItemHandle>>(StringComparer.OrdinalIgnoreCase);
        // Diagnostics
        internal long LastQueryTicks; internal int LastResultCount; internal int LastSourceSize; internal int LastPredicateCount;
        internal long TotalQueryTicks; internal int QueryCount; internal long MaxQueryTicks;

        // Active filter state
        private int? _filterTypeId; private string[] _filterTagsAll; private string[] _filterTagsAny; private string _filterNameContainsPart; private bool _hasEquippedFilter; private (int min,int max)? _depthRange;

        private static bool IsEquippedRaw(object raw)
        {
            try { return raw?.GetType().GetProperty("PluggedIntoSlot", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)?.GetValue(raw, null) != null; } catch { return false; }
        }

        public void AddHandle(IItemHandle h)
        {
            if (h == null) return;
            RemoveHandle(h); // ensure no duplicates
            var raw = h.TryGetRaw();
            if (!_all.Contains(h)) _all.Add(h);
            int tid = TryTypeId(h); if (tid != 0) { if (!_byType.TryGetValue(tid, out var list)) { list = new List<IItemHandle>(); _byType[tid] = list; } list.Add(h); }
            var inv = TryInventory(h); if (inv != null) { if (!_byInventory.TryGetValue(inv, out var list)) { list = new List<IItemHandle>(); _byInventory[inv] = list; } list.Add(h); }
            var tags = h.Tags; if (tags != null) { foreach (var t in tags) { if (string.IsNullOrEmpty(t)) continue; if (!_byTag.TryGetValue(t, out var tlist)) { tlist = new List<IItemHandle>(); _byTag[t] = tlist; } tlist.Add(h); } }
            if (IsEquippedRaw(raw)) _equipped.Add(h);
            if (!_depthCache.ContainsKey(h)) _depthCache[h] = ComputeDepth(h);
            if (!_ancestorChain.ContainsKey(h)) _ancestorChain[h] = ComputeAncestorChain(h);
            var dn = h.DisplayName; if (!string.IsNullOrEmpty(dn)) { if (!_nameIndex.TryGetValue(dn, out var nlist)) { nlist = new List<IItemHandle>(); _nameIndex[dn] = nlist; } nlist.Add(h); IndexNameWords(dn, h); }
            _nameQueryCache.Clear();
        }
        public void RemoveHandle(IItemHandle h)
        {
            if (h == null) return;
            _all.Remove(h);
            int tid = TryTypeId(h); if (tid != 0 && _byType.TryGetValue(tid, out var list)) list.Remove(h);
            var inv = TryInventory(h); if (inv != null && _byInventory.TryGetValue(inv, out var ilist)) ilist.Remove(h);
            var tags = h.Tags; if (tags != null) { foreach (var t in tags) if (_byTag.TryGetValue(t, out var tl)) tl.Remove(h); }
            _equipped.Remove(h);
            _depthCache.Remove(h); _ancestorChain.Remove(h);
            foreach (var kv in _nameIndex) kv.Value.Remove(h);
            foreach (var kv in _nameWordIndex) kv.Value.Remove(h);
            _nameQueryCache.Clear();
        }
        public void UpdateHandle(IItemHandle h)
        {
            RemoveHandle(h); AddHandle(h);
        }
        public void UpdateInventoryEquipped(IItemHandle h)
        {
            if (h == null) return; var raw = h.TryGetRaw(); if (raw == null) return;
            var inv = TryInventory(h);
            // update inventory lists
            foreach (var kv in _byInventory) kv.Value.Remove(h);
            if (inv != null) { if (!_byInventory.TryGetValue(inv, out var list)) { list = new List<IItemHandle>(); _byInventory[inv] = list; } if (!list.Contains(h)) list.Add(h); }
            // update equipped list
            _equipped.Remove(h); if (IsEquippedRaw(raw)) _equipped.Add(h);
        }
        private int ComputeDepth(IItemHandle h)
        {
            int d = 0; var cur = h; int guard = 0;
            while (cur != null && guard++ < 64) { cur = IMKDuckov.Ownership.GetOwner(cur); if (cur != null) d++; }
            return d;
        }
        private List<int?> ComputeAncestorChain(IItemHandle h)
        {
            var list = new List<int?>(); var cur = h; int guard = 0;
            while (cur != null && guard++ < 64) { cur = IMKDuckov.Ownership.GetOwner(cur); if (cur != null) list.Add(cur.InstanceId); }
            return list;
        }
        private void IndexNameWords(string name, IItemHandle h)
        {
            try
            {
                var words = name.Split(new[] {' ','\t','-','_','/','.'}, StringSplitOptions.RemoveEmptyEntries);
                foreach (var w in words)
                {
                    var key = w.ToLowerInvariant(); if (key.Length == 0) continue;
                    if (!_nameWordIndex.TryGetValue(key, out var list)) { list = new List<IItemHandle>(); _nameWordIndex[key] = list; }
                    list.Add(h);
                }
            }
            catch { }
        }

        // Query builder
        public IItemQuery ByTypeId(int typeId) { _filterTypeId = typeId; _preds.Add(h => h != null && h.IsAlive && TryTypeId(h) == typeId); return this; }
        public IItemQuery InInventory(IInventoryHandle inventory) { if (inventory != null) _preds.Add(h => h != null && h.IsAlive && ReferenceEquals(TryInventory(h), inventory.Raw)); return this; }
        public IItemQuery InScope(IItemScope scope) { if (scope != null) _preds.Add(h => scope.Includes(h?.TryGetRaw(), TryInventory(h), h?.TryGetRaw())); return this; }
        public IItemQuery ByTags(params string[] tags)
        {
            if (tags != null && tags.Length > 0)
            {
                _filterTagsAll = tags; var norm = new List<string>(); foreach (var t in tags) if (!string.IsNullOrEmpty(t)) norm.Add(t);
                if (norm.Count > 0) _preds.Add(h => { if (h == null || !h.IsAlive) return false; foreach (var t in norm) { if (!_byTag.TryGetValue(t, out var list) || !list.Contains(h)) return false; } return true; });
            }
            return this;
        }
        public IItemQuery ByTagAny(params string[] tags)
        {
            if (tags != null && tags.Length > 0)
            {
                _filterTagsAny = tags; var norm = new List<string>(); foreach (var t in tags) if (!string.IsNullOrEmpty(t)) norm.Add(t);
                if (norm.Count > 0) _preds.Add(h => { if (h == null || !h.IsAlive) return false; foreach (var t in norm) { if (_byTag.TryGetValue(t, out var list) && list.Contains(h)) return true; } return false; });
            }
            return this;
        }
        public IItemQuery NameContains(string part)
        {
            if (!string.IsNullOrEmpty(part)) { _filterNameContainsPart = part; _preds.Add(h => { if (h == null || !h.IsAlive) return false; var n = h.DisplayName; return !string.IsNullOrEmpty(n) && n.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0; }); }
            return this;
        }
        public IItemQuery OwnedBy(IItemHandle ownerRoot)
        {
            if (ownerRoot != null) _preds.Add(h => { if (h == null || !h.IsAlive) return false; if (_ancestorChain.TryGetValue(h, out var chain)) return chain.Contains(ownerRoot.InstanceId); // fallback dynamic
                var cur = h; int guard = 0; while (cur != null && guard++ < 32) { if (cur.InstanceId == ownerRoot.InstanceId) return true; cur = IMKDuckov.Ownership.GetOwner(cur); } return false; });
            return this;
        }
        public IItemQuery Equipped(bool equipped = true)
        { _hasEquippedFilter = true; _preds.Add(h => { if (h == null || !h.IsAlive) return false; var raw = h.TryGetRaw(); return IsEquippedRaw(raw) == equipped; }); return this; }
        public IItemQuery Depth(int min, int max)
        {
            if (min < 0) min = 0; if (max < min) max = min; _depthRange = (min,max);
            _preds.Add(h => { if (h == null || !h.IsAlive) return false; if (!_depthCache.TryGetValue(h, out var d)) { d = ComputeDepth(h); _depthCache[h] = d; } return d >= min && d <= max; });
            return this;
        }

        public IItemHandle First()
        {
            var sw = Stopwatch.StartNew(); IEnumerable<IItemHandle> src = SelectSource(); foreach (var h in src) { if (Match(h)) { sw.Stop(); RecordDiag(sw.ElapsedTicks, 1, src); return h; } } sw.Stop(); RecordDiag(sw.ElapsedTicks, 0, src); return null;
        }
        public IItemHandle[] Take(int count)
        {
            var sw = Stopwatch.StartNew(); var res = new List<IItemHandle>(); var src = SelectSource(); foreach (var h in src) { if (Match(h)) { res.Add(h); if (res.Count >= count) break; } } sw.Stop(); RecordDiag(sw.ElapsedTicks, res.Count, src); return res.ToArray();
        }
        public IItemHandle[] All()
        {
            var sw = Stopwatch.StartNew(); var res = new List<IItemHandle>(); var src = SelectSource(); foreach (var h in src) if (Match(h)) res.Add(h); sw.Stop(); RecordDiag(sw.ElapsedTicks, res.Count, src); return res.ToArray();
        }

        private IEnumerable<IItemHandle> SelectSource()
        {
            if (_filterTypeId.HasValue && _filterTypeId.Value != 0 && _byType.TryGetValue(_filterTypeId.Value, out var tlist)) return tlist;
            if (_filterTagsAll != null && _filterTagsAll.Length > 0)
            {
                List<IItemHandle> intersection = null; foreach (var tag in _filterTagsAll) { if (string.IsNullOrEmpty(tag)) continue; if (!_byTag.TryGetValue(tag, out var tl) || tl.Count == 0) return Array.Empty<IItemHandle>(); intersection = intersection == null ? new List<IItemHandle>(tl) : intersection.FindAll(x => tl.Contains(x)); if (intersection.Count == 0) return Array.Empty<IItemHandle>(); }
                if (intersection != null) return intersection;
            }
            if (_filterTagsAny != null && _filterTagsAny.Length > 0)
            {
                var union = new HashSet<IItemHandle>(); foreach (var tag in _filterTagsAny) { if (string.IsNullOrEmpty(tag)) continue; if (_byTag.TryGetValue(tag, out var tl)) foreach (var h in tl) union.Add(h); } return union;
            }
            if (!string.IsNullOrEmpty(_filterNameContainsPart))
            {
                var partLower = _filterNameContainsPart.ToLowerInvariant();
                // direct word match first
                if (_nameWordIndex.TryGetValue(partLower, out var wl)) return wl;
                if (!_nameQueryCache.TryGetValue(_filterNameContainsPart, out var cached)) { cached = new List<IItemHandle>(); foreach (var h in _all) { var n = h.DisplayName; if (!string.IsNullOrEmpty(n) && n.ToLowerInvariant().Contains(partLower)) cached.Add(h); } _nameQueryCache[_filterNameContainsPart] = cached; } return cached;
            }
            if (_hasEquippedFilter) return _equipped;
            return _all;
        }

        private bool Match(IItemHandle h)
        { foreach (var p in _preds) if (!p(h)) return false; return true; }
        private static int TryTypeId(IItemHandle h) { try { var raw = h?.TryGetRaw(); return raw?.GetType().GetProperty("TypeID")?.GetValue(raw, null) is int v ? v : 0; } catch { return 0; } }
        private static object TryInventory(IItemHandle h) { try { var raw = h?.TryGetRaw(); return raw?.GetType().GetProperty("InInventory")?.GetValue(raw, null); } catch { return null; } }
        public void Clear() { _preds.Clear(); _all.Clear(); _byType.Clear(); _byInventory.Clear(); _byTag.Clear(); _equipped.Clear(); _depthCache.Clear(); _ancestorChain.Clear(); _nameIndex.Clear(); _nameQueryCache.Clear(); ResetFilterState(); }
        public IItemQuery ResetPredicates() { _preds.Clear(); ResetFilterState(); return this; }
        private void ResetFilterState() { _filterTypeId = null; _filterTagsAll = null; _filterTagsAny = null; _filterNameContainsPart = null; _hasEquippedFilter = false; _depthRange = null; }
        private void RecordDiag(long ticks, int resultCount, IEnumerable<IItemHandle> src)
        {
            LastQueryTicks = ticks; LastResultCount = resultCount; LastPredicateCount = _preds.Count;
            try { LastSourceSize = src is ICollection<IItemHandle> c ? c.Count : 0; } catch { LastSourceSize = 0; }
            TotalQueryTicks += ticks; QueryCount++; if (ticks > MaxQueryTicks) MaxQueryTicks = ticks;
            // warn if query exceeds 5ms
            try { if (ticks > 5 * System.TimeSpan.TicksPerMillisecond) ItemModKit.Core.Log.Warn($"[IMK.Query] slow query ticks={ticks} preds={LastPredicateCount} src={LastSourceSize} results={resultCount}"); } catch { }
        }
        public (double avgMs, double maxMs, int queries) SnapshotPerf()
        {
            var q = QueryCount; if (q == 0) return (0,0,0);
            double avgMs = TotalQueryTicks / (double)q / System.TimeSpan.TicksPerMillisecond;
            double maxMs = MaxQueryTicks / (double)System.TimeSpan.TicksPerMillisecond;
            return (avgMs, maxMs, q);
        }
    }
}
