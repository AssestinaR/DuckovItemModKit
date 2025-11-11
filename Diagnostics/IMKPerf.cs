using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.IO;
using UnityEngine;

namespace ItemModKit.Diagnostics
{
    public static class IMKPerf
    {
        private static readonly double s_tickMs = 1000.0 / Stopwatch.Frequency;
        private static volatile bool s_enabled = (Environment.GetEnvironmentVariable("IMK_PROF") == "1") || UnityEngine.Debug.isDebugBuild;
        private static volatile int s_sample = Math.Max(1, ParseIntEnv("IMK_PROF_SAMPLE", 1));
        private static double s_minMs = Math.Max(0, ParseDoubleEnv("IMK_PROF_MINMS", 2.0));
        private static int s_seq;

        // Allow runtime enable/disable and threshold tuning
        public static void Enable(bool enabled = true, int? sample = null, double? minMs = null)
        {
            s_enabled = enabled;
            if (sample.HasValue && sample.Value >= 1) s_sample = sample.Value;
            if (minMs.HasValue && minMs.Value >= 0) s_minMs = minMs.Value;
            UnityEngine.Debug.Log($"[IMK-PERF] Enabled={s_enabled} sample={s_sample} minMs={s_minMs}");
        }

        public static ScopeToken Scope(string name, string area = null,
            [CallerMemberName] string member = null,
            [CallerFilePath] string file = null,
            [CallerLineNumber] int line = 0)
        {
            if (!s_enabled) return default;
            int n = System.Threading.Interlocked.Increment(ref s_seq);
            if ((n % s_sample) != 0) return default;
            return new ScopeToken(name, area, member, file, line, s_minMs);
        }

        private static int ParseIntEnv(string key, int def)
        {
            try { var v = Environment.GetEnvironmentVariable(key); if (string.IsNullOrEmpty(v)) return def; if (int.TryParse(v, out var i)) return i; } catch { }
            return def;
        }
        private static double ParseDoubleEnv(string key, double def)
        {
            try { var v = Environment.GetEnvironmentVariable(key); if (string.IsNullOrEmpty(v)) return def; if (double.TryParse(v, out var d)) return d; } catch { }
            return def;
        }

        public readonly struct ScopeToken : IDisposable
        {
            private readonly string _name;
            private readonly string _area;
            private readonly string _member;
            private readonly string _file;
            private readonly int _line;
            private readonly long _t0;
            private readonly long _mem0;
            private readonly int _g0, _g1, _g2;
            private readonly int _frame;
            private readonly double _minMs;
            private readonly bool _active;

            public ScopeToken(string name, string area, string member, string file, int line, double minMs)
            {
                _name = name; _area = area; _member = member; _file = file; _line = line; _minMs = minMs;
                _t0 = Stopwatch.GetTimestamp();
                _mem0 = GC.GetTotalMemory(false);
                _g0 = GC.CollectionCount(0); _g1 = GC.CollectionCount(1); _g2 = GC.CollectionCount(2);
                _frame = Time.frameCount;
                _active = true;
            }

            public void Dispose()
            {
                if (!_active) return;
                var t1 = Stopwatch.GetTimestamp();
                double dur = (t1 - _t0) * s_tickMs;
                if (dur < _minMs) return;
                long memDelta = 0;
                try { memDelta = GC.GetTotalMemory(false) - _mem0; } catch { }
                int d0 = 0, d1 = 0, d2 = 0;
                try { d0 = GC.CollectionCount(0) - _g0; d1 = GC.CollectionCount(1) - _g1; d2 = GC.CollectionCount(2) - _g2; } catch { }
                try
                {
                    var ts = DateTime.Now.ToString("O");
                    // JSON line (machine friendly)
                    var json = $"{{\"ts\":\"{ts}\",\"kind\":\"span\",\"name\":\"{Escape(_name)}\",\"durMs\":{dur:0.###},\"area\":\"{Escape(_area)}\",\"frame\":{_frame},\"mem\":{memDelta},\"gc0\":{d0},\"gc1\":{d1},\"gc2\":{d2},\"file\":\"{Escape(_file)}\",\"member\":\"{Escape(_member)}\",\"line\":{_line}}}";
                    UnityEngine.Debug.Log(json);
                    // Plain line (human friendly)
                    var fileOnly = SafePath(_file);
                    UnityEngine.Debug.Log($"[IMK-PERF] {(_area ?? "")}:{_name} {dur:0.###}ms mem¦¤={memDelta} gc={d0}/{d1}/{d2} at {_member} ({fileOnly}:{_line})");
                }
                catch { }
            }

            private static string Escape(string s)
            {
                return (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
            }
            private static string SafePath(string p)
            {
                try { return Path.GetFileName(p) ?? ""; } catch { return ""; }
            }
        }
    }
}
