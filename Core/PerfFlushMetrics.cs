using System;
using System.Threading;

namespace ItemModKit.Core
{
    /// <summary>
    /// 刷新指标：记录每个物品写入耗时、累计 JSON 字节数等，用于诊断调优。
    /// </summary>
    internal static class PerfFlushMetrics
    {
        private static long _items;
        private static long _jsonBytes;
        private static long _flushOps;
        private static double _lastItemMs;
        private static double _maxItemMs;
        private static double _totalItemMs;

        /// <summary>重置统计。</summary>
        public static void Reset()
        {
            Interlocked.Exchange(ref _items, 0);
            Interlocked.Exchange(ref _jsonBytes, 0);
            Interlocked.Exchange(ref _flushOps, 0);
            _lastItemMs = 0; _maxItemMs = 0; _totalItemMs = 0;
        }

        /// <summary>记录单个物品写入样本。</summary>
        /// <param name="jsonBytes">嵌入 JSON 的字节数。</param>
        /// <param name="ms">写入耗时（毫秒）。</param>
        public static void RecordItem(int jsonBytes, double ms)
        {
            Interlocked.Increment(ref _items);
            Interlocked.Add(ref _jsonBytes, jsonBytes);
            _lastItemMs = ms;
            if (ms > _maxItemMs) _maxItemMs = ms;
            _totalItemMs += ms;
        }

        /// <summary>记录一次 Flush 操作发生。</summary>
        public static void RecordFlushOp() => Interlocked.Increment(ref _flushOps);

        /// <summary>生成当前统计快照。</summary>
        public static PerfSnapshot Snapshot()
        {
            return new PerfSnapshot
            {
                Items = Interlocked.Read(ref _items),
                JsonBytes = Interlocked.Read(ref _jsonBytes),
                FlushOps = Interlocked.Read(ref _flushOps),
                LastItemMs = _lastItemMs,
                MaxItemMs = _maxItemMs,
                AvgItemMs = _items > 0 ? _totalItemMs / _items : 0
            };
        }

        internal sealed class PerfSnapshot
        {
            /// <summary>记录的物品样本数。</summary>
            public long Items;
            /// <summary>累计 JSON 字节数。</summary>
            public long JsonBytes;
            /// <summary>累计 Flush 操作数。</summary>
            public long FlushOps;
            /// <summary>最近一次物品写入耗时。</summary>
            public double LastItemMs;
            /// <summary>单个物品写入最大耗时。</summary>
            public double MaxItemMs;
            /// <summary>平均每个物品写入耗时。</summary>
            public double AvgItemMs;
        }
    }
}
