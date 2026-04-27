using UnityEngine;
using System;
using HarmonyLib;

namespace ItemModKit
{
    /// <summary>
    /// IMK 入口脚本。加载后负责：初始化事件桥、生命周期落盘。
    /// 核心不再尝试启动任何 Samples 侧的 UI/工具。
    /// </summary>
    public class ModBehaviour : global::Duckov.Modding.ModBehaviour
    {
        private static bool s_patchesInstalled;

        private static void ReportLifecycleFailureOnce(string operation, Exception ex)
        {
            if (string.IsNullOrEmpty(operation) || ex == null) return;
            Core.Log.Warn($"[IMK.ModBehaviour] {operation} degraded: {ex.GetType().Name}: {ex.Message}");
        }

        /// <summary>
        /// Unity Awake：初始化事件桥。
        /// </summary>
        void Awake()
        {
            // 初始化事件桥
            try { Adapters.Duckov.DuckovEventBridge.Initialize(); } catch (Exception ex) { ReportLifecycleFailureOnce("Awake.InitializeEventBridge", ex); }
            try { Adapters.Duckov.DuckovPersistenceLifecycleBridge.Initialize(); } catch (Exception ex) { ReportLifecycleFailureOnce("Awake.InitializePersistenceLifecycleBridge", ex); }
            if (!s_patchesInstalled)
            {
                try
                {
                    new Harmony("mod.itemmodkit.persistence").PatchAll();
                    s_patchesInstalled = true;
                }
                catch (Exception ex) { ReportLifecycleFailureOnce("Awake.PatchAll", ex); }
            }
        }

        /// <summary>
        /// Unity OnDestroy：场景卸载时落盘并释放事件桥。
        /// </summary>
        void OnDestroy()
        {
            try { Adapters.Duckov.IMKDuckov.FlushAllDirty("scene unload"); } catch (Exception ex) { ReportLifecycleFailureOnce("OnDestroy.FlushAllDirty", ex); }
            try { Adapters.Duckov.DuckovPersistenceLifecycleBridge.Dispose(); } catch (Exception ex) { ReportLifecycleFailureOnce("OnDestroy.DisposePersistenceLifecycleBridge", ex); }
            try { Adapters.Duckov.DuckovEventBridge.Dispose(); } catch (Exception ex) { ReportLifecycleFailureOnce("OnDestroy.DisposeEventBridge", ex); }
        }

        /// <summary>
        /// 程序退出前的最终落盘。
        /// </summary>
        void OnApplicationQuit()
        {
            try { Adapters.Duckov.IMKDuckov.FlushAllDirty("application quit"); } catch (Exception ex) { ReportLifecycleFailureOnce("OnApplicationQuit.FlushAllDirty", ex); }
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
                    if (Time.frameCount % 300 == 0)
                    {
                        try { if (Adapters.Duckov.IMKDuckov.TryGetCurrentSelectedHandle() != null) events.HintActive(2f); } catch (Exception ex) { ReportLifecycleFailureOnce("Update.HintActive", ex); }
                    }
                    events.Tick();
                }
                var world = Adapters.Duckov.IMKDuckov.WorldDrops;
                if (world != null)
                {
                    world.Tick();
                }

                var persistence = Adapters.Duckov.IMKDuckov.PersistenceScheduler;
                if (persistence != null)
                {
                    persistence.Tick(null);
                }
            }
            catch (Exception ex) { ReportLifecycleFailureOnce("Update", ex); }
        }
    }
}
