using System;

namespace ItemModKit.Core
{
 public enum LogLevel { Debug=0, Info=1, Warn=2, Error=3 }

 public interface ILogger
 {
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
 public static ILogger Current { get => _current; }
 public static void Use(ILogger logger) { if (logger != null) _current = logger; }
 public static void Debug(string msg) { try { _current.Log(LogLevel.Debug, msg, null); } catch { } }
 public static void Info(string msg) { try { _current.Log(LogLevel.Info, msg, null); } catch { } }
 public static void Warn(string msg) { try { _current.Log(LogLevel.Warn, msg, null); } catch { } }
 public static void Error(string msg, Exception ex = null) { try { _current.Log(LogLevel.Error, msg, ex); } catch { } }
 }
}
