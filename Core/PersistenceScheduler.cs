using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace ItemModKit.Core
{
    /// <summary>
    /// 持久化调度器：收集物品“脏”变更，按延迟与节流策略批量写回。
    /// - EnqueueDirty：入队标记（Immediate 可跳过延迟）
    /// - Tick：按时间窗口与 MaxPerTick 控制刷新节奏
    /// - Flush/FlushAll：立即刷新单个或全部
    /// - RequestFlushAllDeferred：将所有标记置为立即，由 Tick 分帧处理
    /// 额外：记录刷新耗时与 JSON 体积，支持嵌入扩展块的校验和与 Base64 编码
    /// </summary>
    internal sealed partial class PersistenceScheduler : IPersistenceScheduler
    {
        private sealed class Entry
        {
            public object Item;
            public DirtyKind Dirty;
            public float FirstDirtyAt;
            public float LastDirtyAt;
            public bool Immediate;
        }

        private readonly IItemAdapter _item;
        private readonly IItemPersistence _persist;
        private readonly Func<object, ItemSnapshot> _capture;
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly List<int> _flushOrder = new List<int>();

        /// <summary>同一物品最后一次变更后的延迟秒数，超过则触发写回。</summary>
        public float DelaySeconds { get; set; } = PersistenceSettings.Current.DelaySeconds;
        /// <summary>每次 Tick 最多处理的物品数。</summary>
        public int MaxPerTick { get; set; } = PersistenceSettings.Current.MaxPerTick;
        /// <summary>自第一次标脏起的最大延迟，超过则强制写回。</summary>
        public float MaxDelaySeconds { get; set; } = PersistenceSettings.Current.MaxDelaySeconds;

        private const int CurrentFormatVersion = 1;

        private long _processedTotal;
        /// <summary>当前队列中的物品数量。</summary>
        public int PendingCount => _entries.Count;
        /// <summary>启动以来累计处理的物品数量。</summary>
        public long ProcessedTotal => _processedTotal;

        public PersistenceScheduler(IItemAdapter item, IItemPersistence persist, Func<object, ItemSnapshot> capture)
        {
            _item = item; _persist = persist; _capture = capture;
        }

        /// <summary>
        /// 入队一个“脏”标记；若同一物品已在队列中则合并 DirtyKind 并刷新时间戳。
        /// </summary>
        public void EnqueueDirty(object item, DirtyKind kind, bool immediate = false)
        {
            if (item == null || kind == DirtyKind.None) return;
            try
            {
                int id = Adapters.Duckov.DuckovTypeUtils.GetStableId(item);
                if (!_entries.TryGetValue(id, out var e))
                {
                    e = new Entry { Item = item, Dirty = kind, FirstDirtyAt = Now(), LastDirtyAt = Now(), Immediate = immediate };
                    _entries[id] = e; _flushOrder.Add(id);
                }
                else
                {
                    e.Dirty |= kind;
                    e.LastDirtyAt = Now();
                    if (immediate) e.Immediate = true;
                }
            }
            catch { }
        }

        /// <summary>立即刷新某个物品（如果在队列中）。</summary>
        public void Flush(object item, bool force = false)
        {
            if (item == null) return;
            try
            {
                int id = Adapters.Duckov.DuckovTypeUtils.GetStableId(item);
                if (!_entries.TryGetValue(id, out var e)) return;
                InternalFlush(id, e, force);
            }
            catch { }
        }

        /// <summary>立即刷新所有队列中的物品。</summary>
        public void FlushAll(string reason = null)
        {
            try
            {
                var ids = new List<int>(_flushOrder);
                foreach (var id in ids)
                {
                    if (_entries.TryGetValue(id, out var e)) InternalFlush(id, e, true);
                }
            }
            catch { }
        }

        /// <summary>
        /// 将所有入队项标记为 Immediate，后续由 Tick 在每帧中分批处理，避免一次性卡顿。
        /// </summary>
        public void RequestFlushAllDeferred()
        {
            try
            {
                for (int i = 0; i < _flushOrder.Count; i++)
                {
                    var id = _flushOrder[i];
                    if (_entries.TryGetValue(id, out var e)) e.Immediate = true;
                }
            }
            catch { }
        }

        /// <summary>
        /// 每帧调用：根据时间窗口与 Immediate 标记，按 MaxPerTick 的上限处理刷新。
        /// </summary>
        public void Tick(float? now = null)
        {
            try
            {
                var t0 = UnityEngine.Time.realtimeSinceStartup;
                var ts = now ?? Now();
                int processed = 0;
                for (int i = 0; i < _flushOrder.Count && processed < MaxPerTick; i++)
                {
                    var id = _flushOrder[i];
                    if (!_entries.TryGetValue(id, out var e)) continue;
                    float age = ts - e.LastDirtyAt;
                    float sinceFirst = ts - e.FirstDirtyAt;
                    if (e.Immediate || age >= DelaySeconds || sinceFirst >= MaxDelaySeconds)
                    {
                        InternalFlush(id, e, false);
                        processed++;
                    }
                }
                var t1 = UnityEngine.Time.realtimeSinceStartup; try { PerfCounters.SchedulerTicks++; PerfCounters.SchedulerTickTotalMs += (t1 - t0) * 1000.0; } catch { }
            }
            catch { }
        }

        private static readonly Dictionary<string, object> s_extraReuse = new Dictionary<string, object>();

        private void InternalFlush(int id, Entry e, bool force)
        {
            try
            {
                if (e == null || e.Item == null) { _entries.Remove(id); _flushOrder.Remove(id); return; }
                var snap = _capture(e.Item);
                var meta = Persistence.BuildMetaFromSnapshot(snap);
                // metrics start
                var tStart = UnityEngine.Time.realtimeSinceStartup;
                meta.OwnerId = Adapters.Duckov.DuckovOwnership.CurrentOrInfer();
                meta.FormatVersion = CurrentFormatVersion;
                s_extraReuse.Clear();
                bool wantExtra = PersistenceSettings.Current.EmbedExtra;
                bool anyExtra = false;
                if (wantExtra && e.Dirty.HasFlag(DirtyKind.Variables)) { s_extraReuse["variables"] = snap.Variables; anyExtra = true; }
                if (wantExtra && e.Dirty.HasFlag(DirtyKind.Tags)) { s_extraReuse["tags"] = snap.Tags; anyExtra = true; }
                if (wantExtra && e.Dirty.HasFlag(DirtyKind.Modifiers)) { s_extraReuse["modifiers"] = snap.Modifiers; anyExtra = true; }
                if (wantExtra && e.Dirty.HasFlag(DirtyKind.Slots)) { s_extraReuse["slots"] = snap.Slots; anyExtra = true; }
                if (wantExtra && anyExtra)
                {
                    try { ItemStateExtensions.Contribute(e.Item, snap, e.Dirty, s_extraReuse); } catch { }
                    try { ItemStateExtensions.Enrich(e.Item, meta, s_extraReuse); } catch { }
                }
                // embed only if requested
                if (wantExtra && s_extraReuse.Count > 0)
                {
                    try
                    {
                        var json = Newtonsoft.Json.JsonConvert.SerializeObject(s_extraReuse);
                        var bytes = Encoding.UTF8.GetByteCount(json);
                        if (bytes <= PersistenceSettings.Current.MaxBlobBytes)
                        {
                            if (PersistenceSettings.Current.EnableChecksum) meta.ExtraChecksum = ComputeChecksum(json);
                            if (PersistenceSettings.Current.UseBase64Encoding)
                            {
                                var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
                                meta.EmbeddedJson = b64;
                            }
                            else meta.EmbeddedJson = json;
                        }
                        else { meta.EmbeddedJson = null; meta.ExtraChecksum = null; }
                    }
                    catch { meta.EmbeddedJson = null; meta.ExtraChecksum = null; }
                }
                // write meta
                _persist.RecordMeta(e.Item, meta, writeVariables: PersistenceSettings.Current.WriteRedundantVariables);
                if (PersistenceSettings.Current.ReapplyAfterWrite)
                {
                    try { _item.ReapplyModifiers(e.Item); } catch { }
                }
                var tEnd = UnityEngine.Time.realtimeSinceStartup;
                try { PerfCounters.SchedulerFlushTotalMs += (tEnd - tStart) * 1000.0; } catch { }
                int jsonBytes = 0; try { if (!string.IsNullOrEmpty(meta.EmbeddedJson)) jsonBytes = meta.EmbeddedJson.Length; } catch { }
                try { PerfFlushMetrics.RecordItem(jsonBytes, (tEnd - tStart) * 1000.0); } catch { }
                try
                {
                    var msItem = (tEnd - tStart) * 1000.0;
                    if (msItem > 200.0)
                    {
                        int typeId = 0; string name = null; int extraKeys = s_extraReuse.Count;
                        try { typeId = _item.GetTypeId(e.Item); } catch { }
                        try { name = _item.GetDisplayNameRaw(e.Item) ?? _item.GetName(e.Item); } catch { }
                        Core.Log.Info($"[IMK.Flush.Item] {msItem:0.0}ms type={typeId} name={name ?? "?"} dirty={e.Dirty} jsonBytes={jsonBytes} extraKeys={extraKeys}");
                    }
                }
                catch { }
            }
            catch { }
            finally
            {
                try { PerfCounters.SchedulerFlushes++; } catch { }
                try { _processedTotal++; } catch { }
                _entries.Remove(id);
                _flushOrder.Remove(id);
            }
        }

        private static string ComputeChecksum(string json)
        {
            try
            {
                using (var crc32 = new Crc32())
                {
                    var data = Encoding.UTF8.GetBytes(json ?? string.Empty);
                    var hash = crc32.ComputeHash(data);
                    return BitConverter.ToString(hash).Replace("-", string.Empty);
                }
            }
            catch { return null; }
        }

        private static float Now()
        {
            try { return UnityEngine.Time.unscaledTime; } catch { return (float)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds; }
        }
    }

    // Minimal CRC32 implementation (polynomial 0xEDB88320)
    internal sealed class Crc32 : HashAlgorithm
    {
        private const uint Polynomial = 0xEDB88320u;
        private readonly uint[] _table = new uint[256];
        private uint _crc = 0xFFFFFFFFu;

        public Crc32()
        {
            HashSizeValue = 32;
            for (uint i = 0; i < _table.Length; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++)
                {
                    c = ((c & 1) != 0) ? (Polynomial ^ (c >> 1)) : (c >> 1);
                }
                _table[i] = c;
            }
        }

        public override void Initialize() { _crc = 0xFFFFFFFFu; }

        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
            for (int i = ibStart; i < ibStart + cbSize; i++)
            {
                byte index = (byte)((_crc & 0xFF) ^ array[i]);
                _crc = _table[index] ^ (_crc >> 8);
            }
        }

        protected override byte[] HashFinal()
        {
            _crc ^= 0xFFFFFFFFu;
            return BitConverter.GetBytes(_crc);
        }
    }
}
