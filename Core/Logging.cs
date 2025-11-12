using System;

namespace ItemModKit.Core
{
    /// <summary>日志级别。</summary>
    public enum LogLevel 
    { 
        /// <summary>调试信息。</summary>
        Debug = 0, 
        /// <summary>一般信息。</summary>
        Info = 1, 
        /// <summary>警告。</summary>
        Warn = 2, 
        /// <summary>错误。</summary>
        Error = 3 
    }

    /// <summary>日志接口。</summary>
    public interface ILogger
    {
        /// <summary>记录一条日志。</summary>
        void Log(LogLevel level, string message, Exception ex = null);
    }

    internal sealed class NoopLogger : ILogger
    {
        public void Log(LogLevel level, string message, Exception ex = null) { }
    }

    /// <summary>
    /// Unity 输出后端（默认）：桥接到 UnityEngine.Debug。
    /// </summary>
    internal sealed class UnityLogger : ILogger
    {
        public void Log(LogLevel level, string message, Exception ex = null)
        {
            try
            {
                var msg = message ?? string.Empty;
                if (ex != null) msg += "\n" + ex;
                switch (level)
                {
                    case LogLevel.Error: UnityEngine.Debug.LogError(msg); break;
                    case LogLevel.Warn: UnityEngine.Debug.LogWarning(msg); break;
                    default: UnityEngine.Debug.Log(msg); break;
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// 日志门面：可替换输出后端，提供简单的 Debug/Info/Warn/Error 方法。
    /// </summary>
    public static class Log
    {
        private static ILogger _current = new UnityLogger(); // default to Unity output
        /// <summary>当前日志实现。</summary>
        public static ILogger Current { get => _current; }
        /// <summary>替换当前日志实现。</summary>
        public static void Use(ILogger logger) { if (logger != null) _current = logger; }
        /// <summary>输出 Debug 级日志。</summary>
        public static void Debug(string msg) { try { _current.Log(LogLevel.Debug, msg, null); } catch { } }
        /// <summary>输出 Info 级日志。</summary>
        public static void Info(string msg) { try { _current.Log(LogLevel.Info, msg, null); } catch { } }
        /// <summary>输出 Warn 级日志。</summary>
        public static void Warn(string msg) { try { _current.Log(LogLevel.Warn, msg, null); } catch { } }
        /// <summary>输出 Error 级日志（可带异常）。</summary>
        public static void Error(string msg, Exception ex = null) { try { _current.Log(LogLevel.Error, msg, ex); } catch { } }
    }
}
