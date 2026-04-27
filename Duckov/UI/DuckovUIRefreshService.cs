using System;
using System.Collections.Concurrent;
using System.Reflection;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// UI 刷新服务：对指定背包触发一次 Refresh，并可选设置 NeedInspection=true 以让 UI 重新渲染。
    /// </summary>
    internal sealed class DuckovUIRefreshService : IUIRefreshService
    {
        private static readonly ConcurrentDictionary<string, byte> s_reportedUiRefreshFailures = new ConcurrentDictionary<string, byte>();

        private static void ReportRefreshFailureOnce(string operation, Exception ex)
        {
            if (string.IsNullOrEmpty(operation) || ex == null) return;
            if (!s_reportedUiRefreshFailures.TryAdd(operation, 0)) return;
            Log.Warn($"[IMK.UIRefresh] {operation} degraded: {ex.GetType().Name}: {ex.Message}");
        }

        /// <summary>
        /// 刷新背包 UI。
        /// </summary>
        /// <param name="inventory">目标背包对象。</param>
        /// <param name="markNeedInspection">是否设置 NeedInspection 为 true。</param>
        public void RefreshInventory(object inventory, bool markNeedInspection = true)
        {
            if (inventory == null) return;
            try
            {
                if (markNeedInspection)
                {
                    var p = inventory.GetType().GetProperty(EngineKeys.Property.NeedInspection, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    p?.SetValue(inventory, true, null);
                }
            }
            catch (Exception ex) { ReportRefreshFailureOnce("RefreshInventory.markNeedInspection", ex); }
            try
            {
                var m = inventory.GetType().GetMethod(EngineKeys.Method.Refresh, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                m?.Invoke(inventory, null);
            }
            catch (Exception ex) { ReportRefreshFailureOnce("RefreshInventory.invokeRefresh", ex); }
        }
    }
}
