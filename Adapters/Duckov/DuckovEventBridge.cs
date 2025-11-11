using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ItemStatsSystem;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
 /// <summary>
 /// 事件桥：把引擎侧的场景/物品事件转发到 IMK 的 ItemEvents，减少轮询开销。
 /// - 订阅 SceneManager.sceneLoaded
 /// - 扫描场景中所有 Item 并挂接 onChildChanged/onItemTreeChanged/onDestroy 等
 /// - 统一转发为 PublishChanged/PublishRemoved
 /// </summary>
 internal static class DuckovEventBridge
 {
 private static readonly HashSet<int> Subscribed = new HashSet<int>();
 private static bool _initialized;

 /// <summary>
 /// 初始化（只生效一次），并对当前场景做一次扫描订阅。
 /// </summary>
 public static void Initialize()
 {
 if (_initialized) return;
 _initialized = true;
 try
 {
 SceneManager.sceneLoaded += OnSceneLoaded;
 // first pass for active scene
 SafeScanAndSubscribe();
 }
 catch (Exception ex) { Log.Warn("DuckovEventBridge.Initialize 失败: " + ex.Message); }
 }

 /// <summary>
 /// 反初始化：取消订阅并清空状态。
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
 /// 扫描场景中的 Item，并为尚未订阅过的对象挂接事件。
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
 /// 为指定 Item 挂接关心的事件（树变化、插槽、检查状态、销毁）。
 /// </summary>
 private static void TrySubscribe(Item it)
 {
 try
 {
 int id = it.GetInstanceID();
 if (Subscribed.Contains(id)) return;
 Subscribed.Add(id);
 // Item tree/slot/inspection changes
 it.onChildChanged += OnItemChanged;
 it.onInspectionStateChanged += OnItemChanged;
 it.onPluggedIntoSlot += OnItemChanged;
 it.onUnpluggedFromSlot += OnItemChanged;
 it.onItemTreeChanged += OnItemChanged;
 // Destruction
 it.onDestroy += OnItemDestroyed;
 }
 catch (Exception ex) { Log.Warn("DuckovEventBridge.TrySubscribe: " + ex.Message); }
 }

 private static void OnItemChanged(Item it)
 {
 try { if (it != null) IMKDuckov.ItemEvents?.PublishChanged(it, null); }
 catch { }
 }
 private static void OnItemDestroyed(Item it)
 {
 try { if (it != null) IMKDuckov.ItemEvents?.PublishRemoved(it, null); }
 catch { }
 }
 }
}
