using UnityEngine;
using System;

namespace ItemModKit
{
    /// <summary>
    /// IMK 入口脚本。加载后负责：初始化事件桥、生命周期落盘。
    /// 核心不再尝试启动任何 Samples 侧的 UI/工具。
    /// </summary>
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        /// <summary>
        /// Unity Awake：初始化事件桥。
        /// </summary>
        void Awake()
        {
            // 初始化事件桥
            try { Adapters.Duckov.DuckovEventBridge.Initialize(); } catch { }
        }

        /// <summary>
        /// Unity OnDestroy：场景卸载时落盘并释放事件桥。
        /// </summary>
        void OnDestroy()
        {
            try { Adapters.Duckov.IMKDuckov.FlushAllDirty("scene unload"); } catch { }
            try { Adapters.Duckov.DuckovEventBridge.Dispose(); } catch { }
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
                    if (Time.frameCount % 300 == 0)
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
    }
}
