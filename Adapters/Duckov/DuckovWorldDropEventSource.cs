using System;
using System.Collections.Generic;
using static ItemModKit.Adapters.Duckov.DuckovTypeUtils;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 世界掉落事件源：扫描场景中不在任何背包内的物品，触发“环境掉落”事件。
    /// （当前无法精准区分敌人掉落与环境掉落，统一走 Environment 事件。）
    /// </summary>
    public sealed class DuckovWorldDropEventSource : IWorldDropEventSource
    {
        /// <summary>敌人掉落（暂未区分，保留接口）。</summary>
        public event Action<object> OnEnemyDrop; // not classified yet
        /// <summary>环境掉落。</summary>
        public event Action<object> OnEnvironmentDrop;

        /// <summary>是否启用事件源。</summary>
        public bool Enabled { get; set; } = true;
        /// <summary>扫描间隔秒。</summary>
        public float ScanInterval { get; set; } = 1.5f; // slower default, event bridge preferred
        /// <summary>单次扫描的最大处理数量。</summary>
        public int ChunkSize { get; set; } = 96;

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
                    // world candidate: not in any inventory
                    bool inInv = false; try { inInv = _inv.IsInInventory(obj); } catch { inInv = false; }
                    if (inInv) continue;
                    int id = GetStableId(obj);
                    if (!_known.Contains(id))
                    {
                        _known.Add(id); _id2Obj[id] = obj;
                        // currently we cannot reliably distinguish enemy vs environment, route to environment
                        OnEnvironmentDrop?.Invoke(obj);
                    }
                }
                if (_cursor >= _buffer.Count)
                {
                    // clean up removed world items
                    var toRemove = new List<int>();
                    foreach (var id in _known)
                    {
                        if (!_id2Obj.TryGetValue(id, out var o) || o == null) { toRemove.Add(id); continue; }
                        // if it entered inventory, consider removed from world
                        bool inInv = false; try { inInv = _inv.IsInInventory(o); } catch { inInv = false; }
                        if (inInv) toRemove.Add(id);
                    }
                    foreach (var id in toRemove) { _known.Remove(id); _id2Obj.Remove(id); }
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
