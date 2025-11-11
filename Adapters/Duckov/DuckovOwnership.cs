using System;
using System.Reflection;

namespace ItemModKit.Adapters.Duckov
{
 /// <summary>
 /// 归属标记（OwnerId）辅助：
 /// - Use(ownerId) 作用域设置当前 OwnerId；
 /// - CurrentOrInfer() 在未显式设置时，从调用栈/程序集名推断一个。
 /// </summary>
 internal static class DuckovOwnership
 {
 [ThreadStatic] private static string _override;
 /// <summary>临时覆盖当前 OwnerId，用于标记后续创建/修改。</summary>
 public static IDisposable Use(string ownerId) => new Scope(ownerId);
 /// <summary>返回当前 OwnerId；若没有则从 ModBehaviour 所在程序集推断。</summary>
 public static string CurrentOrInfer()
 {
 if (!string.IsNullOrEmpty(_override)) return _override;
 // 尝试从当前 ModBehaviour 程序集名推断 OwnerId
 try
 {
 // 查找调用栈中第一个 ModBehaviour 派生类型所在线程
 foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
 {
 try
 {
 var types = asm.GetTypes();
 for (int i=0;i<types.Length;i++)
 {
 var t = types[i];
 if (t == null) continue;
 try
 {
 if (t.FullName == "Duckov.Modding.ModBehaviour") continue;
 if (IsSubclassOf(t, "Duckov.Modding.ModBehaviour")) return asm.GetName().Name;
 }
 catch { }
 }
 }
 catch { }
 }
 }
 catch { }
 try { return Assembly.GetCallingAssembly()?.GetName()?.Name ?? "Unknown"; } catch { return "Unknown"; }
 }
 private static bool IsSubclassOf(Type t, string baseFullName)
 {
 try
 {
 var b = t; while (b != null) { if (string.Equals(b.FullName, baseFullName, StringComparison.Ordinal)) return true; b = b.BaseType; }
 }
 catch { }
 return false;
 }
 private sealed class Scope : IDisposable
 {
 private readonly string _prev;
 public Scope(string id) { _prev = _override; _override = id; }
 public void Dispose() { _override = _prev; }
 }
 }
}
