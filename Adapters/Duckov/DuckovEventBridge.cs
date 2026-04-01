using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ItemStatsSystem;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 引擎事件桥：订阅场景加载与 Item 的关键生命周期事件，将其转换为 IMK 的统一物品事件。
    /// 用于降低频繁轮询导致的性能开销。
    /// </summary>
    internal static class DuckovEventBridge
    {
        private static readonly HashSet<int> Subscribed = new HashSet<int>();
        private static bool _initialized;

        /// <summary>
        /// 初始化（只执行一次）：订阅 <see cref="SceneManager.sceneLoaded"/> 并对当前活动场景进行首次扫描。
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            try
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                SafeScanAndSubscribe(); // 首次扫描
            }
            catch (Exception ex) { Log.Warn("DuckovEventBridge.Initialize 失败: " + ex.Message); }
        }

        /// <summary>
        /// 反初始化：取消订阅并清空已订阅集合。
        /// </summary>
        public static void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;
            try
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Subscribed.Clear();
            }
            catch { }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try { SafeScanAndSubscribe(); }
            catch (Exception ex) { Log.Warn("DuckovEventBridge.OnSceneLoaded: " + ex.Message); }
        }

        /// <summary>
        /// 扫描场景中的 <see cref="Item"/> 并为尚未订阅的实例挂接事件。
        /// </summary>
        private static void SafeScanAndSubscribe()
        {
            try
            {
                var items = UnityEngine.Object.FindObjectsOfType<Item>(true);
                if (items == null) return;
                for (int i = 0; i < items.Length; i++)
                {
                    var it = items[i];
                    if (it == null) continue;
                    TrySubscribe(it);
                }
            }
            catch (Exception ex) { Log.Warn("DuckovEventBridge.SafeScanAndSubscribe: " + ex.Message); }
        }

        /// <summary>
        /// 为单个物品挂接各种变化与销毁事件（重复调用会被忽略）。
        /// </summary>
        /// <param name="it">目标物品。</param>
        private static void TrySubscribe(Item it)
        {
            try
            {
                int id = it.GetInstanceID();
                if (Subscribed.Contains(id)) return;
                Subscribed.Add(id);
                it.onChildChanged += OnItemChanged;
                it.onInspectionStateChanged += OnItemChanged;
                it.onPluggedIntoSlot += OnItemChanged;
                it.onUnpluggedFromSlot += OnItemChanged;
                it.onItemTreeChanged += OnItemChanged;
                it.onDestroy += OnItemDestroyed;
                it.onParentChanged += OnItemParentChanged; // 新增：用于世界掉落捕获
            }
            catch (Exception ex) { Log.Warn("DuckovEventBridge.TrySubscribe: " + ex.Message); }
        }

        /// <summary>统一处理除销毁外的变化事件并发布到 IMK。</summary>
        private static void OnItemChanged(Item it)
        {
            try { if (it != null) IMKDuckov.ItemEvents?.PublishChanged(it, null); }
            catch { }
        }
        /// <summary>处理销毁事件并发布“移除”。</summary>
        private static void OnItemDestroyed(Item it)
        {
            try { if (it != null) IMKDuckov.ItemEvents?.PublishRemoved(it, null); }
            catch { }
        }

        /// <summary>
        /// 监听父对象变化：当物品从背包/槽位脱离且不再归属于任何 Inventory 或 Slot 时，直接登记为世界掉落，避免等待扫描。
        /// </summary>
        private static void OnItemParentChanged(Item it)
        {
            try
            {
                if (it == null) return;
                // 条件：不在 Inventory、无 PluggedIntoSlot、ActiveAgent 存在或其 GameObject 仍存活
                var inInv = it.InInventory != null;
                var inSlot = it.PluggedIntoSlot != null;
                if (inInv || inSlot) return; // 仍有归属
                if (it.gameObject == null) return;
                // 注册世界掉落（可能是刚刚通过 Drop 扩展方法创建）
                IMKDuckov.WorldDrops.RegisterExternalWorldItem(it);
                // 发布事件：来源 World
                IMKDuckov.ItemEvents.PublishChanged(it, new ItemEventContext
                {
                    Source = ItemEventSourceType.World,
                    Cause = ItemEventCause.Move,
                    Timestamp = GetNow()
                });
            }
            catch { }
        }

        private static float GetNow()
        {
            try { return UnityEngine.Time.unscaledTime; } catch { return 0f; }
        }
    }
}
