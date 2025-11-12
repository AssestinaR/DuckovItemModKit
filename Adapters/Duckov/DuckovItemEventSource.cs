using System;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 物品事件源：提供两种模式。外部事件模式由 Publish* 显式推送并在合并窗口后触发；
    /// 回退轮询模式则按时间与预算扫描物品集合，检测新增/移除/变化。
    /// </summary>
    public sealed class DuckovItemEventSource : IItemEventSource
    {
        /// <summary>物品新增事件。</summary>
        public event Action<object> OnItemAdded;
        /// <summary>物品移除事件。</summary>
        public event Action<object> OnItemRemoved;
        /// <summary>物品变化事件（属性/变量/标签等哈希变化）。</summary>
        public event Action<object> OnItemChanged;

        /// <summary>是否启用事件源。</summary>
        public bool Enabled { get; set; } = true;
        /// <summary>是否仅在 UI 活跃时扫描（轮询模式下）。</summary>
        public bool ScanOnlyWhenUIActive { get; set; } = true;
        /// <summary>轮询扫描的时间间隔（秒，非缩放时间）。</summary>
        public float ScanInterval { get; set; } = 0.3f;
        /// <summary>每次扫描尝试处理的最大物品条目（受预算限制后可能更少）。</summary>
        public int ChunkSize { get; set; } = 64;
        /// <summary>外部事件合并窗口秒数：窗口内累积，结束后统一触发。</summary>
        public float CoalesceWindow { get; set; } = 0.05f;
        /// <summary>外部模式空闲超时秒数：超过后转为轮询。</summary>
        public float ExternalIdleTimeout { get; set; } = 2.0f;

        private float _nextScanAt;
        private float _activeHintUntil;
        private int _enumerationCursor;
        private System.Collections.Generic.List<object> _enumBuffer = new System.Collections.Generic.List<object>(256);
        private int _externalModeRefs;
        private float _lastPublishAt;
        private readonly System.Collections.Generic.Dictionary<int, (float ts, string kind, object item)> _pending = new System.Collections.Generic.Dictionary<int, (float ts, string kind, object item)>();

        private readonly IItemQuery _query;
        private readonly IItemAdapter _item;
        private readonly System.Collections.Generic.HashSet<int> _knownIds = new System.Collections.Generic.HashSet<int>();
        private readonly System.Collections.Generic.Dictionary<int, int> _hash = new System.Collections.Generic.Dictionary<int, int>();
        private readonly System.Collections.Generic.Dictionary<int, object> _idObjCache = new System.Collections.Generic.Dictionary<int, object>();
        private System.WeakReference _uiOpRef, _uiDtRef;

        /// <summary>
        /// 构造函数：创建一个物品事件源并关联查询与物品访问接口。
        /// </summary>
        /// <param name="query">查询接口（用于枚举物品集合）。</param>
        /// <param name="item">物品适配器（用于读取核心字段与变量）。</param>
        public DuckovItemEventSource(IItemQuery query, IItemAdapter item) { _query = query; _item = item; }

        /// <summary>进入外部事件模式（引用计数，可嵌套）。</summary>
        public void BeginExternalMode() { if (++_externalModeRefs < 0) _externalModeRefs = 1; }
        /// <summary>退出外部事件模式（引用计数减一）。</summary>
        public void EndExternalMode() { if (--_externalModeRefs < 0) _externalModeRefs = 0; }
        private bool InExternalMode => _externalModeRefs > 0;
        private bool ExternalActive(float now)
        {
            if (!InExternalMode) return false;
            if (_pending.Count > 0) return true;
            if (now - _lastPublishAt <= ExternalIdleTimeout) return true;
            return false;
        }

        /// <summary>发布“新增”事件（外部模式下进入合并窗口）。</summary>
        public void PublishAdded(object item, ItemEventContext ctx) { Publish("add", item); }
        /// <summary>发布“移除”事件。</summary>
        public void PublishRemoved(object item, ItemEventContext ctx) { Publish("rem", item); }
        /// <summary>发布“改变”事件。</summary>
        public void PublishChanged(object item, ItemEventContext ctx) { Publish("chg", item); }
        /// <summary>发布“移动”事件（归类为改变）。</summary>
        public void PublishMoved(object item, int fromIndex, int toIndex, ItemEventContext ctx) { Publish("chg", item); }
        /// <summary>发布“合并”事件（归类为改变）。</summary>
        public void PublishMerged(object item, ItemEventContext ctx) { Publish("chg", item); }
        /// <summary>发布“拆分”事件（归类为改变）。</summary>
        public void PublishSplit(object item, ItemEventContext ctx) { Publish("chg", item); }

        /// <summary>
        /// 外部发布入口：记录待触发事件并更新脏标记。
        /// </summary>
        /// <param name="kind">事件类别：add/rem/chg。</param>
        /// <param name="item">目标物品。</param>
        private void Publish(string kind, object item)
        {
            try
            {
                if (!Enabled) return; if (item == null) return;
                int id = DuckovTypeUtils.GetStableId(item);
                float now = 0f; try { now = UnityEngine.Time.unscaledTime; } catch { }
                _pending[id] = (now, kind, item);
                _lastPublishAt = now;
                if (kind == "add") IMKDuckov.MarkDirty(item, DirtyKind.Core | DirtyKind.Tags | DirtyKind.Variables | DirtyKind.Modifiers | DirtyKind.Slots);
                else if (kind == "chg") IMKDuckov.MarkDirty(item, DirtyKind.All);
            }
            catch { }
        }

        /// <summary>提示 UI 活跃：延长扫描资格窗口。</summary>
        /// <param name="seconds">延长秒数。</param>
        public void HintActive(float seconds = 2f) { try { _activeHintUntil = UnityEngine.Time.unscaledTime + Math.Max(0.05f, seconds); } catch { } }

        /// <summary>
        /// 主循环：外部模式下合并并触发事件；否则按预算轮询物品集合并检测变化。
        /// 未订阅任何事件时会直接返回以节省开销。
        /// </summary>
        public void Tick()
        {
            try
            {
                if (!Enabled) return;
                if (OnItemAdded == null && OnItemRemoved == null && OnItemChanged == null) return; // 无订阅则跳过
                float now = 0f; try { now = UnityEngine.Time.unscaledTime; } catch { }
                if (ExternalActive(now))
                {
                    // flush pending within coalesce window
                    var toFire = new System.Collections.Generic.List<(string kind, object item)>();
                    var toRemove = new System.Collections.Generic.List<int>();
                    foreach (var kv in _pending)
                    {
                        if (now - kv.Value.ts >= CoalesceWindow) { toFire.Add((kv.Value.kind, kv.Value.item)); toRemove.Add(kv.Key); }
                    }
                    foreach (var k in toRemove) _pending.Remove(k);
                    foreach (var e in toFire)
                    {
                        try
                        {
                            if (e.kind == "add") OnItemAdded?.Invoke(e.item);
                            else if (e.kind == "rem") OnItemRemoved?.Invoke(e.item);
                            else OnItemChanged?.Invoke(e.item);
                        }
                        catch { }
                    }
                    return; // 跳过轮询
                }
                // 轮询逻辑
                if (now < _nextScanAt) return; // 节流
                _nextScanAt = now + Math.Max(0.25f, ScanInterval); // 默认稍慢
                if (ScanOnlyWhenUIActive && !IsUIActive() && now <= _activeHintUntil == false) return;

                EnsureEnumBuffer();
                var current = new System.Collections.Generic.HashSet<int>();
                int processed = 0; int start = _enumerationCursor;
                const int BudgetPerTick = 16; // 每帧最小预算，避免卡顿
                while (_enumerationCursor < _enumBuffer.Count && processed < System.Math.Min(ChunkSize, BudgetPerTick))
                {
                    var it = _enumBuffer[_enumerationCursor++]; processed++;
                    if (it == null) continue; int id = DuckovTypeUtils.GetStableId(it);
                    current.Add(id);
                    if (!_knownIds.Contains(id))
                    {
                        _knownIds.Add(id); _idObjCache[id] = it; _hash[id] = Hash(it);
                        OnItemAdded?.Invoke(it);
                        // mark dirty on add
                        IMKDuckov.MarkDirty(it, DirtyKind.Core | DirtyKind.Tags | DirtyKind.Variables | DirtyKind.Modifiers | DirtyKind.Slots);
                    }
                    else
                    {
                        int hv = Hash(it);
                        if (!_hash.TryGetValue(id, out var old) || old != hv) { _hash[id] = hv; OnItemChanged?.Invoke(it); IMKDuckov.MarkDirty(it, DirtyKind.All); }
                    }
                }
                if (_enumerationCursor >= _enumBuffer.Count)
                {
                    var toRemoveIds = new System.Collections.Generic.List<int>();
                    foreach (var id in _knownIds) if (!current.Contains(id)) { toRemoveIds.Add(id); }
                    foreach (var id in toRemoveIds)
                    {
                        _knownIds.Remove(id);
                        _hash.Remove(id);
                        if (_idObjCache.TryGetValue(id, out var obj)) { _idObjCache.Remove(id); OnItemRemoved?.Invoke(obj); }
                    }
                    _enumerationCursor = 0;
                }
            }
            catch { }
        }
        /// <summary>准备枚举缓冲：首次或一轮结束时重新填充。</summary>
        private void EnsureEnumBuffer()
        {
            try
            {
                if (_enumerationCursor == 0)
                {
                    _enumBuffer.Clear();
                    foreach (var it in _query.EnumerateAllInventories()) _enumBuffer.Add(it);
                }
            }
            catch { _enumBuffer.Clear(); }
        }
        /// <summary>计算物品变化哈希（质量/价值/类型/变量数/标签数）。</summary>
        private int Hash(object it)
        {
            try
            {
                int q = _item.GetQuality(it);
                int v = _item.GetValue(it);
                int t = _item.GetTypeId(it);
                int varCount = 0; try { var vs = _item.GetVariables(it); varCount = vs?.Length ?? 0; } catch { }
                int tagCount = 0; try { var ts = _item.GetTags(it); tagCount = ts?.Length ?? 0; } catch { }
                unchecked { return (((q * 397) ^ v) * 397) ^ t ^ (varCount << 1) ^ (tagCount << 2); }
            }
            catch { return 0; }
        }
        /// <summary>判断 UI 是否活跃（检测操作菜单与详情面板）。</summary>
        private bool IsUIActive()
        {
            try
            {
                object op = null, dt = null;
                if (_uiOpRef == null || !_uiOpRef.IsAlive) { var opT = DuckovTypeUtils.FindType("Duckov.UI.ItemOperationMenu"); var inst = opT != null ? UnityEngine.Object.FindObjectOfType(opT) : null; _uiOpRef = new System.WeakReference(inst); op = inst; } else op = _uiOpRef.Target;
                if (_uiDtRef == null || !_uiDtRef.IsAlive) { var dtT = DuckovTypeUtils.FindType("Duckov.UI.ItemDetailsDisplay"); var inst = dtT != null ? UnityEngine.Object.FindObjectOfType(dtT) : null; _uiDtRef = new System.WeakReference(inst); dt = inst; } else dt = _uiDtRef.Target;
                if (op != null && IsActive(op)) return true;
                if (dt != null && IsActive(dt)) return true;
            }
            catch { }
            return false;
        }
        /// <summary>组件是否激活（GameObject 活跃或 enabled=true）。</summary>
        private static bool IsActive(object comp)
        {
            try
            {
                var cT = comp.GetType();
                var goProp = cT.GetProperty("gameObject", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var go = goProp?.GetValue(comp, null) as UnityEngine.GameObject;
                if (go != null) return go.activeInHierarchy;
                var en = cT.GetProperty("enabled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (en != null) { var v = en.GetValue(comp, null); if (v is bool b) return b; }
            }
            catch { }
            return true; // 宽松：无法判断则认为活跃
        }
    }
}
