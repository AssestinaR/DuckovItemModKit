using System;
using System.Collections.Generic;
using ItemModKit;
using ItemModKit.Adapters.Duckov;
using ItemModKit.Core.Locator;

namespace ItemModKit.Adapters.Duckov.Locator
{
    /// <summary>
    /// Item 定位器（无 Harmony 版本）。维护弱引用索引并提供句柄创建。
    /// </summary>
    public sealed class DuckovItemLocator : IItemLocator, IItemIndex
    {
        private readonly Dictionary<int, WeakReference> _byInstance = new Dictionary<int, WeakReference>();
        private readonly IInventoryClassifier _classifier;
        private IItemHandle _lastCreated;

        /// <summary>
        /// 创建一个 Duckov 物品定位器。
        /// </summary>
        /// <param name="classifier">Inventory 分类器；用于在查询时辅助判断作用域。</param>
        public DuckovItemLocator(IInventoryClassifier classifier) { _classifier = classifier; }

        /// <summary>
        /// 从裸物品实例创建一个按实例 ID 延迟解析的句柄。
        /// </summary>
        /// <param name="raw">运行时物品对象。</param>
        /// <returns>成功时返回新的句柄；若传入 null，则返回 null。</returns>
        public IItemHandle FromInstance(object raw)
        {
            if (raw == null) return null;
            int? iid = TryGetInstanceId(raw);
            return new ItemHandle(() => TryResolveByInstanceId(iid), iid, null);
        }

        /// <summary>
        /// 从实例 ID 创建一个延迟解析句柄。
        /// </summary>
        /// <param name="instanceId">目标实例 ID。</param>
        /// <returns>始终返回一个可尝试解析该实例 ID 的句柄对象。</returns>
        public IItemHandle FromInstanceId(int instanceId)
        {
            return new ItemHandle(() => TryResolveByInstanceId(instanceId), instanceId, null);
        }

        /// <summary>
        /// 从逻辑 ID 创建一个句柄。
        /// </summary>
        /// <param name="id">逻辑 ID。</param>
        /// <returns>返回一个仅携带逻辑 ID 的句柄；其裸实例解析回调默认为空。</returns>
        public IItemHandle FromLogicalId(string id)
        {
            return new ItemHandle(() => null, null, id);
        }

        /// <summary>
        /// 从当前 UI 选中项创建句柄。
        /// </summary>
        /// <returns>成功解析当前 UI 选中物品时返回对应句柄；否则返回 null。</returns>
        public IItemHandle FromUISelection()
        {
            try
            {
                object cur;
                if (!DuckovUISelectionResolver.TryGetCurrentItem(out cur) || cur == null) return null;
                return FromInstance(cur);
            }
            catch { return null; }
        }

        /// <summary>
        /// 获取最近一次创建或登记的句柄。
        /// </summary>
        /// <returns>若此前创建过句柄则返回最近一个；否则返回 null。</returns>
        public IItemHandle LastCreated() => _lastCreated;

        /// <summary>
        /// 查询当前索引中的全部句柄，可选按谓词或作用域过滤。
        /// </summary>
        /// <param name="predicate">当前未使用，保留给后续扩展的谓词参数。</param>
        /// <param name="scope">可选作用域过滤器；为 null 时返回当前索引中的全部存活项。</param>
        /// <returns>返回满足条件的句柄数组；若索引为空则返回空数组。</returns>
        public IItemHandle[] Query(object predicate = null, IItemScope scope = null)
        {
            var list = new List<IItemHandle>();
            foreach (var kv in _byInstance)
            {
                var obj = kv.Value.Target;
                if (obj == null) continue;
                if (scope != null && !scope.Includes(obj, TryGetInventory(obj), TryGetOwner(obj))) continue;
                list.Add(new ItemHandle(() => kv.Value.Target, kv.Key, null));
            }
            return list.ToArray();
        }

        /// <summary>
        /// 通知定位器某个物品已创建。
        /// </summary>
        /// <param name="raw">新创建的运行时物品对象。</param>
        public void OnCreated(object raw)
        {
            int? iid = TryGetInstanceId(raw);
            if (iid != null)
            {
                _byInstance[iid.Value] = new WeakReference(raw);
                _lastCreated = new ItemHandle(() => TryResolveByInstanceId(iid.Value), iid, null);
            }
        }

        /// <summary>
        /// 通知定位器某个物品已销毁。
        /// </summary>
        /// <param name="raw">已销毁的运行时物品对象。</param>
        public void OnDestroyed(object raw)
        {
            int? iid = TryGetInstanceId(raw);
            if (iid != null) _byInstance.Remove(iid.Value);
        }

        /// <summary>
        /// 通知定位器某个物品已移动到新容器。
        /// </summary>
        /// <param name="raw">发生移动的运行时物品对象。</param>
        /// <param name="newContainer">新的容器对象；当前实现暂未使用，主要为后续二级索引扩展预留。</param>
        public void OnMoved(object raw, object newContainer = null)
        {
            // Placeholder: Could update secondary indexes if implemented
        }

        /// <summary>
        /// 按实例 ID 查找句柄。
        /// </summary>
        /// <param name="instanceId">目标实例 ID。</param>
        /// <returns>返回一个可解析该实例 ID 的句柄对象。</returns>
        public IItemHandle FindByInstanceId(int instanceId) => FromInstanceId(instanceId);

        /// <summary>
        /// 按 TypeId 查找全部句柄。
        /// </summary>
        /// <param name="typeId">目标类型 ID。</param>
        /// <returns>当前实现未建立 TypeId 索引，因此始终返回空数组。</returns>
        public IItemHandle[] FindAllByTypeId(int typeId) { return Array.Empty<IItemHandle>(); }

        private object TryResolveByInstanceId(int? iid)
        {
            if (iid == null) return null;
            if (_byInstance.TryGetValue(iid.Value, out var wr))
            {
                if (wr.Target != null) return wr.Target;
            }
            return null;
        }

        private static int? TryGetInstanceId(object raw)
        {
            try
            {
                var m = raw.GetType().GetMethod("GetInstanceID", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (m != null)
                {
                    var v = m.Invoke(raw, null);
                    if (v is int i) return i;
                    try { return Convert.ToInt32(v); } catch { }
                }
            }
            catch { }
            return null;
        }

        private static object TryGetInventory(object item)
        {
            try { return item?.GetType().GetProperty("Inventory")?.GetValue(item, null); } catch { return null; }
        }
        private static object TryGetOwner(object item)
        {
            try { return item?.GetType().GetProperty("ParentItem")?.GetValue(item, null); } catch { return null; }
        }
    }
}
