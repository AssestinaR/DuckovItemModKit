using System;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 背包放置服务：尝试把物品加入背包，支持合并与失败时的下一帧重试。
    /// </summary>
    internal sealed class DuckovInventoryPlacementService : IInventoryPlacementService
    {
        /// <summary>
        /// 尝试放置：优先 AddAndMerge，然后查询 IndexOf 确认；若失败可调度下一帧重试。
        /// </summary>
        /// <param name="inventory">目标背包。</param>
        /// <param name="item">待放置物品。</param>
        /// <param name="allowMerge">是否允许合并。</param>
        /// <param name="enableDeferredRetry">是否在失败时安排下一帧重试。</param>
        /// <returns>返回是否添加、索引以及是否安排了延迟重试。</returns>
        public (bool added, int index, bool deferredScheduled) TryPlace(object inventory, object item, bool allowMerge = true, bool enableDeferredRetry = true)
        {
            if (inventory == null || item == null) return (false, -1, false);
            bool added = false; int index = -1; bool deferred = false;
            try { added = IMKDuckov.Inventory.AddAndMerge(inventory, item); } catch { added = false; }
            try { index = IMKDuckov.Inventory.IndexOf(inventory, item); } catch { index = -1; }
            if (!added && index < 0 && enableDeferredRetry)
            {
                deferred = true;
                TryScheduleNextFrame(() => { try { IMKDuckov.Inventory.AddAndMerge(inventory, item); } catch { } });
            }
            return (added, index, deferred);
        }

        /// <summary>安排在下一帧执行一个动作（用于降低当前帧压力）。</summary>
        private static void TryScheduleNextFrame(Action a)
        {
            try
            {
                var go = new UnityEngine.GameObject("IMK_PlacementDeferred");
                go.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
                go.AddComponent<DeferredInvoker>().Init(a);
            }
            catch { a?.Invoke(); }
        }
        private sealed class DeferredInvoker : UnityEngine.MonoBehaviour
        {
            private Action _a; public void Init(Action a) { _a = a; }
            private System.Collections.IEnumerator Start() { yield return null; try { _a?.Invoke(); } catch { } try { UnityEngine.Object.DestroyImmediate(gameObject); } catch { } }
        }
    }
}
