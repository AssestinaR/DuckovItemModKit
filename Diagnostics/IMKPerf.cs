using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.IO;
using UnityEngine;

namespace ItemModKit.Diagnostics
{
    /// <summary>
    /// 轻量性能探针：提供 Scope 包裹与日志输出，便于在开发期采样热点。
    /// </summary>
    public static class IMKPerf
    {
        private static readonly double s_tickMs = 1000.0 / Stopwatch.Frequency;
        private static volatile bool s_enabled = (Environment.GetEnvironmentVariable("IMK_PROF") == "1") || UnityEngine.Debug.isDebugBuild;
        private static volatile int s_sample = Math.Max(1, ParseIntEnv("IMK_PROF_SAMPLE", 1));
        private static double s_minMs = Math.Max(0, ParseDoubleEnv("IMK_PROF_MINMS", 2.0));
        private static int s_seq;

        /// <summary>
        /// 启用/禁用采样并可调整采样率与最小阈值。
        /// </summary>
        /// <param name="enabled">是否启用。</param>
        /// <param name="sample">采样间隔（每 N 次记录一次）。</param>
        /// <param name="minMs">最小记录阈值（毫秒）。</param>
        public static void Enable(bool enabled = true, int? sample = null, double? minMs = null)
        {
            s_enabled = enabled;
            if (sample.HasValue && sample.Value >= 1) s_sample = sample.Value;
            if (minMs.HasValue && minMs.Value >= 0) s_minMs = minMs.Value;
            UnityEngine.Debug.Log($"[IMK-PERF] Enabled={s_enabled} sample={s_sample} minMs={s_minMs}");
        }

        /// <summary>
        /// 创建一个性能作用域，满足采样与阈值条件时在 Dispose 时输出日志。
        /// </summary>
        /// <param name="name">区段名称。</param>
        /// <param name="area">区域/模块名。</param>
        /// <param name="member">调用成员（自动填写）。</param>
        /// <param name="file">调用文件（自动填写）。</param>
        /// <param name="line">调用行号（自动填写）。</param>
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

        /// <summary>
        /// 性能作用域令牌：在 Dispose 时根据阈值输出 JSON 行与可读行。
        /// </summary>
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

            /// <summary>
            /// 构造作用域令牌（内部使用）。
            /// </summary>
            public ScopeToken(string name, string area, string member, string file, int line, double minMs)
            {
                _name = name; _area = area; _member = member; _file = file; _line = line; _minMs = minMs;
                _t0 = Stopwatch.GetTimestamp();
                _mem0 = GC.GetTotalMemory(false);
                _g0 = GC.CollectionCount(0); _g1 = GC.CollectionCount(1); _g2 = GC.CollectionCount(2);
                _frame = Time.frameCount;
                _active = true;
            }

            /// <summary>结束作用域并按需输出日志。</summary>
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
                    // JSON line (machine friendly) - avoid interpolated braces
                    var json = "{\"ts\":\"" + ts + "\",\"kind\":\"span\",\"name\":\"" + Escape(_name) + "\",\"durMs\":" + dur.ToString("0.###") + ",\"area\":\"" + Escape(_area) + "\",\"frame\":" + _frame + ",\"mem\":" + memDelta + ",\"gc0\":" + d0 + ",\"gc1\":" + d1 + ",\"gc2\":" + d2 + ",\"file\":\"" + Escape(_file) + "\",\"member\":\"" + Escape(_member) + "\",\"line\":" + _line + "}";
                    UnityEngine.Debug.Log(json);
                    // Plain line (human friendly)
                    var fileOnly = SafePath(_file);
                    UnityEngine.Debug.Log($"[IMK-PERF] {(_area ?? "")}:{_name} {dur:0.###}ms memΔ={memDelta} gc={d0}/{d1}/{d2} at {_member} ({fileOnly}:{_line})");
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
