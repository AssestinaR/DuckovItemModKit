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
        // 单个待刷条目，按稳定 ID 聚合脏标记与时间窗口。
        private sealed class Entry
        {
            // 目标运行时对象。
            public object Item;

            // 当前累积的脏类别位。
            public DirtyKind Dirty;

            // 首次标脏时间，用于最大延迟判定。
            public float FirstDirtyAt;

            // 最近一次标脏时间，用于普通延迟窗口判定。
            public float LastDirtyAt;

            // 是否要求尽快刷新。
            public bool Immediate;
        }

        // 运行时读取入口。
        private readonly IItemAdapter _item;

        // 持久化写入入口。
        private readonly IItemPersistence _persist;

        // 抓取快照的回调，由外部注入以降低调度器对快照实现的耦合。
        private readonly Func<object, ItemSnapshot> _capture;

        // 按稳定 ID 聚合的待刷条目表。
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();

        // 刷新顺序表，便于 Tick 按稳定顺序限流处理。
        private readonly List<int> _flushOrder = new List<int>();

        /// <summary>同一物品最后一次变更后的延迟秒数，超过则触发写回。</summary>
        public float DelaySeconds { get; set; } = PersistenceSettings.Current.DelaySeconds;
        /// <summary>每次 Tick 最多处理的物品数。</summary>
        public int MaxPerTick { get; set; } = PersistenceSettings.Current.MaxPerTick;
        /// <summary>自第一次标脏起的最大延迟，超过则强制写回。</summary>
        public float MaxDelaySeconds { get; set; } = PersistenceSettings.Current.MaxDelaySeconds;

        // 当前 EmbeddedJson/extra block 的格式版本。
        private const int CurrentFormatVersion = 1;

        private long _processedTotal;
        /// <summary>当前队列中的物品数量。</summary>
        public int PendingCount => _entries.Count;
        /// <summary>启动以来累计处理的物品数量。</summary>
        public long ProcessedTotal => _processedTotal;

        /// <summary>
        /// 构造持久化调度器。
        /// 调用方负责提供读取适配器、持久化写入器和快照抓取函数。
        /// </summary>
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

        /// <summary>立即刷新某个物品；如果目标不在队列中则直接忽略。</summary>
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
        /// 这不是同步 FlushAll，而是“尽快在后续若干帧内刷完”。
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
        /// now 为 null 时使用运行时时钟；测试或离线驱动场景可以显式传入时间戳。
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

        // 复用的 extra block 容器，降低高频刷新时的临时分配。
        private static readonly Dictionary<string, object> s_extraReuse = new Dictionary<string, object>();
        private const string InternalPersistenceVariablePrefix = "IMK_";

        /// <summary>
        /// 执行单条刷新。
        /// 这里会抓取快照、构建 ItemMeta、选择性嵌入 extra block，并最终写回持久化层。
        /// </summary>
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
                if (wantExtra && e.Dirty.HasFlag(DirtyKind.Variables))
                {
                    var filteredVariables = FilterExtraVariables(snap.Variables);
                    if (filteredVariables.Length > 0)
                    {
                        s_extraReuse["variables"] = filteredVariables;
                    }
                }
                if (wantExtra && e.Dirty.HasFlag(DirtyKind.Tags)) { s_extraReuse["tags"] = snap.Tags; }
                if (wantExtra && e.Dirty.HasFlag(DirtyKind.Modifiers)) { s_extraReuse["modifiers"] = snap.Modifiers; }
                if (wantExtra && e.Dirty.HasFlag(DirtyKind.Slots)) { s_extraReuse["slots"] = snap.Slots; }
                if (wantExtra)
                {
                    try { ItemStateExtensions.Contribute(e.Item, snap, e.Dirty, s_extraReuse); } catch { }
                    if (s_extraReuse.Count > 0)
                    {
                        try { ItemStateExtensions.Enrich(e.Item, meta, s_extraReuse); } catch { }
                    }
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

        private static VariableEntry[] FilterExtraVariables(VariableEntry[] variables)
        {
            if (variables == null || variables.Length == 0)
            {
                return Array.Empty<VariableEntry>();
            }

            var kept = new List<VariableEntry>(variables.Length);
            for (int index = 0; index < variables.Length; index++)
            {
                var entry = variables[index];
                if (string.IsNullOrEmpty(entry.Key))
                {
                    continue;
                }

                if (entry.Key.StartsWith(InternalPersistenceVariablePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                kept.Add(entry);
            }

            return kept.Count > 0 ? kept.ToArray() : Array.Empty<VariableEntry>();
        }

        /// <summary>为 EmbeddedJson 计算校验和；失败时返回 null。</summary>
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

        /// <summary>获取当前调度时钟；优先使用 Unity 的 unscaledTime，失败时回退到 UTC 秒数。</summary>
        private static float Now()
        {
            try { return UnityEngine.Time.unscaledTime; } catch { return (float)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds; }
        }
    }

    /// <summary>
    /// 最小 CRC32 实现。
    /// 仅用于为 EmbeddedJson 生成轻量校验和，不追求通用哈希库级别的可配置性。
    /// </summary>
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
