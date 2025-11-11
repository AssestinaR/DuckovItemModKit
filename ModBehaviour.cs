using UnityEngine;
using System;

namespace ItemModKit
{
 /// <summary>
 /// IMK 入口脚本。加载后负责：初始化事件桥、生命周期落盘、可选启动 IMK Studio 调试窗口。
 /// </summary>
 public class ModBehaviour : Duckov.Modding.ModBehaviour
 {
 /// <summary>IMK Studio 调试窗口的隐藏宿主对象（存在 Samples 才创建）。</summary>
 static GameObject s_Studio;
 /// <summary>保证尝试启动 Studio 逻辑只执行一次。</summary>
 static bool s_TriedLaunch;

 /// <summary>
 /// Unity Awake：初始化事件桥，尝试启动 IMK Studio。
 /// </summary>
 void Awake()
 {
 // 初始化事件桥
 try { Adapters.Duckov.DuckovEventBridge.Initialize(); } catch { }
 // 启动 Studio（若存在 Samples 包）
 TryLaunchStudio();
 }

 /// <summary>
 /// Unity OnDestroy：场景卸载时落盘并释放事件桥，清理 Studio。
 /// </summary>
 void OnDestroy()
 {
 try { Adapters.Duckov.IMKDuckov.FlushAllDirty("scene unload"); } catch { }
 try { Adapters.Duckov.DuckovEventBridge.Dispose(); } catch { }
 // 清理 Studio
 try { if (s_Studio != null) { GameObject.Destroy(s_Studio); s_Studio = null; } } catch { }
 }

 /// <summary>
 /// 程序退出前的最终落盘。
 /// </summary>
 void OnApplicationQuit()
 {
     try { Adapters.Duckov.IMKDuckov.FlushAllDirty("application quit"); } catch { }
 }

 /// <summary>
 /// 每帧更新：轻量轮询事件源；大部分逻辑依靠事件桥而无需频繁扫描。
 /// 每 300 帧对 UI 选中物品发一次激活提示，避免上下文超时。
 /// </summary>
 void Update()
 {
 try
 {
 var events = Adapters.Duckov.IMKDuckov.ItemEvents;
 if (events != null)
 {
 if (Time.frameCount %300 ==0)
 {
 try { if (Adapters.Duckov.IMKDuckov.UISelection.TryGetCurrentItem(out var _)) events.HintActive(2f); } catch { }
 }
 events.Tick();
 }
 var world = Adapters.Duckov.IMKDuckov.WorldDrops;
 if (world != null)
 {
 world.Tick();
 }
 }
 catch { }
 }

 /// <summary>
 /// 尝试创建 IMK Studio 主窗口：仅当 Samples 中类型可解析且当前场景没有已有实例。
 /// </summary>
 private static void TryLaunchStudio()
 {
     if (s_TriedLaunch) return; s_TriedLaunch = true;
     try
     {
         var t = Type.GetType("ItemModKit.Samples.UI.Window.MainWindow")
                 ?? FindTypeInLoadedAssemblies("ItemModKit.Samples.UI.Window.MainWindow");
         if (t == null) return;
         var existing = UnityEngine.Object.FindObjectsOfType(t);
         if (existing != null && existing.Length > 0) return;
         s_Studio = new GameObject("IMK Studio");
         s_Studio.hideFlags = HideFlags.HideAndDontSave;
         s_Studio.AddComponent(t);
     }
     catch { }
 }

 /// <summary>
 /// 在已加载的所有程序集里按完整类型名查找。
 /// </summary>
 /// <param name="typeName">完整类型名，例如 Namespace.Type。</param>
 /// <returns>找到的类型，或 null。</returns>
 private static Type FindTypeInLoadedAssemblies(string typeName)
 {
     try
     {
         var asms = AppDomain.CurrentDomain.GetAssemblies();
         for (int i = 0; i < asms.Length; i++)
         {
             var tt = asms[i].GetType(typeName, false);
             if (tt != null) return tt;
         }
     }
     catch { }
     return null;
 }
 }
}
