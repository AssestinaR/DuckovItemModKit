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

        public static void Reset()
        {
            Interlocked.Exchange(ref _items, 0);
            Interlocked.Exchange(ref _jsonBytes, 0);
            Interlocked.Exchange(ref _flushOps, 0);
            _lastItemMs = 0; _maxItemMs = 0; _totalItemMs = 0;
        }

        public static void RecordItem(int jsonBytes, double ms)
        {
            Interlocked.Increment(ref _items);
            Interlocked.Add(ref _jsonBytes, jsonBytes);
            _lastItemMs = ms;
            if (ms > _maxItemMs) _maxItemMs = ms;
            _totalItemMs += ms;
        }

        public static void RecordFlushOp() => Interlocked.Increment(ref _flushOps);

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
            public long Items;
            public long JsonBytes;
            public long FlushOps;
            public double LastItemMs;
            public double MaxItemMs;
            public double AvgItemMs;
        }
    }
}
