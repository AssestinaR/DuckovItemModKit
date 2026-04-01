using System;
using System.Collections.Generic;
using static ItemModKit.Adapters.Duckov.DuckovTypeUtils;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 世界掉落事件源：定期扫描未被任何背包/槽位持有的物品并触发 Environment 事件。
    /// 现在支持外部直接注册世界物品（无需等待扫描），用于事件桥即时捕获。
    /// </summary>
    public sealed class DuckovWorldDropEventSource : IWorldDropEventSource
    {
        /// <summary>玩家掉落（尚未分类出具体来源）事件。</summary>
        public event Action<object> OnEnemyDrop; // not classified yet
        /// <summary>环境掉落事件（在世界中存在的可拾取物品）。</summary>
        public event Action<object> OnEnvironmentDrop;

        /// <summary>是否启用事件源。</summary>
        public bool Enabled { get; set; } = true;
        /// <summary>扫描间隔秒。</summary>
        public float ScanInterval { get; set; } = 1.5f; // slower default, event bridge preferred
        /// <summary>单次扫描的最大处理数量。</summary>
        public int ChunkSize { get; set; } = 96;

        // existing fields
        private readonly IItemAdapter _item;
        private readonly IInventoryAdapter _inv;
        private readonly HashSet<int> _known = new HashSet<int>();
        private readonly Dictionary<int, object> _id2Obj = new Dictionary<int, object>();
        private readonly List<object> _buffer = new List<object>(512);
        private float _nextScanAt;
        private int _cursor;

        /// <summary>
        /// 构造函数：创建一个世界掉落事件源。
        /// </summary>
        /// <param name="item">物品访问适配器。</param>
        /// <param name="inv">背包/容器适配器。</param>
        public DuckovWorldDropEventSource(IItemAdapter item, IInventoryAdapter inv)
        {
            _item = item; _inv = inv;
        }

        /// <summary>
        /// 外部直接登记一个已被判定为“世界掉落”的物品。如未登记过，会加入缓存并立即触发 OnEnvironmentDrop。
        /// 调用方需保证物品当前不在任何背包或槽位中。
        /// </summary>
        /// <param name="item">原始物品对象实例。</param>
        public void RegisterExternalWorldItem(object item)
        {
            try
            {
                if (item == null) return;
                int id = GetStableId(item);
                if (_known.Contains(id)) return; // 已知，忽略重复
                // 基本合法性校验：不在任何 Inventory 中
                bool inInv = false; try { inInv = _inv.IsInInventory(item); } catch { inInv = false; }
                if (inInv) return; // 安全起见，避免误报
                _known.Add(id);
                _id2Obj[id] = item;
                try
                {
                    var h = Adapters.Duckov.Locator.DuckovHandleFactory.CreateItemHandle(item);
                    IMKDuckov.RegisterHandle(h);
                }
                catch { }
                OnEnvironmentDrop?.Invoke(item);
            }
            catch { }
        }

        /// <summary>
        /// 每帧调用：按时间片扫描场景物品，不在任何背包内的视为“世界掉落”。
        /// 首次发现触发 OnEnvironmentDrop；进入背包后从跟踪集合移除。
        /// </summary>
        public void Tick()
        {
            try
            {
                if (!Enabled) return;
                if (OnEnemyDrop == null && OnEnvironmentDrop == null) return; // need-based
                float now = 0f; try { now = UnityEngine.Time.unscaledTime; } catch { }
                if (now < _nextScanAt) return;
                _nextScanAt = now + Math.Max(0.5f, ScanInterval);
                EnsureBuffer();
                int processed = 0;
                const int BudgetPerTick = 24; // smaller budget per frame
                while (_cursor < _buffer.Count && processed < System.Math.Min(ChunkSize, BudgetPerTick))
                {
                    var obj = _buffer[_cursor++]; processed++;
                    if (obj == null) continue;
                    bool inInv = false; try { inInv = _inv.IsInInventory(obj); } catch { inInv = false; }
                    if (inInv) continue;
                    int id = GetStableId(obj);
                    if (!_known.Contains(id))
                    {
                        _known.Add(id); _id2Obj[id] = obj;
                        try { var h = Adapters.Duckov.Locator.DuckovHandleFactory.CreateItemHandle(obj); IMKDuckov.RegisterHandle(h); } catch { }
                        OnEnvironmentDrop?.Invoke(obj);
                    }
                }
                if (_cursor >= _buffer.Count)
                {
                    var toRemove = new List<int>();
                    foreach (var id in _known)
                    {
                        if (!_id2Obj.TryGetValue(id, out var o) || o == null) { toRemove.Add(id); continue; }
                        bool inInv = false; try { inInv = _inv.IsInInventory(o); } catch { inInv = false; }
                        if (inInv) toRemove.Add(id);
                    }
                    foreach (var id in toRemove)
                    {
                        _known.Remove(id);
                        if (_id2Obj.TryGetValue(id, out var o2))
                        {
                            try { var h = Adapters.Duckov.Locator.DuckovHandleFactory.CreateItemHandle(o2); IMKDuckov.LogicalIds.Unbind(h); } catch { }
                        }
                        _id2Obj.Remove(id);
                    }
                    _cursor = 0;
                }
            }
            catch { }
        }

        /// <summary>准备缓存：在新一轮扫描开始时填充所有 Item 对象。</summary>
        private void EnsureBuffer()
        {
            try
            {
                if (_cursor == 0)
                {
                    _buffer.Clear();
                    var itemT = FindType("ItemStatsSystem.Item") ?? FindType("Item");
                    if (itemT == null) return;
                    var arr = UnityEngine.Object.FindObjectsOfType(itemT, true);
                    if (arr is Array a)
                    {
                        for (int i = 0; i < a.Length; i++) { var o = a.GetValue(i); if (o != null) _buffer.Add(o); }
                    }
                }
            }
            catch { _buffer.Clear(); }
        }
    }
}
