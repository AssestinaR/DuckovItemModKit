using System.Reflection;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// UI 刷新服务：对指定背包触发一次 Refresh，并可选设置 NeedInspection=true 以让 UI 重新渲染。
    /// </summary>
    internal sealed class DuckovUIRefreshService : IUIRefreshService
    {
        /// <summary>
        /// 刷新背包 UI。
        /// </summary>
        public void RefreshInventory(object inventory, bool markNeedInspection = true)
        {
            if (inventory == null) return;
            try
            {
                if (markNeedInspection)
                {
                    var p = inventory.GetType().GetProperty(EngineKeys.Property.NeedInspection, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                    p?.SetValue(inventory, true, null);
                }
            }
            catch { }
            try
            {
                var m = inventory.GetType().GetMethod(EngineKeys.Method.Refresh, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                m?.Invoke(inventory, null);
            }
            catch { }
        }
    }
}
