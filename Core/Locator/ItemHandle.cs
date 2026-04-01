using System;

namespace ItemModKit.Core.Locator
{
    /// <summary>
    /// 运行时物品 handle 的默认实现。
    /// 它通过 resolver 延迟获取底层对象，并用 LogicalId 维持 replace/rebirth 之后的身份连续性。
    /// </summary>
    internal sealed class ItemHandle : IItemHandle
    {
        // 运行时对象通过委托延迟解析，避免 handle 强持有已销毁对象。
        private readonly Func<object> _resolver;

        /// <summary>底层实例 ID；对象已销毁后仍可用于日志或重新关联。</summary>
        public int? InstanceId { get; }

        /// <summary>逻辑 ID；用于跨 replace/rebirth 重新绑定“同一件逻辑物品”。</summary>
        public string LogicalId { get; private set; }

        /// <summary>
        /// 构造一个仅带 resolver 的 handle。
        /// 适合先拿到运行时对象，再按需懒加载元数据的场景。
        /// </summary>
        public ItemHandle(Func<object> resolver, int? instanceId = null, string logicalId = null)
        {
            _resolver = resolver ?? (() => null);
            InstanceId = instanceId;
            LogicalId = logicalId ?? (instanceId?.ToString());
        }

        /// <summary>对象当前是否仍能解析到有效运行时实例。</summary>
        public bool IsAlive => TryGetRaw() != null;

        /// <summary>尝试解析底层运行时对象；失败或对象已失效时返回 null。</summary>
        public object TryGetRaw() { try { return _resolver(); } catch { return null; } }

        public override string ToString() => $"ItemHandle(iid={InstanceId}, lid={LogicalId})";

        /// <summary>把 handle 重新绑定到新的逻辑 ID；通常由逻辑 ID 映射服务在 replace/rebirth 后调用。</summary>
        public void RebindLogical(string newId)
        {
            if (!string.IsNullOrEmpty(newId)) LogicalId = newId;
        }

        // 以下缓存字段用于避免频繁反射读取 TypeID、DisplayName 和 Tags。
        private int _typeId; private bool _typeInit;
        private string _name; private bool _nameInit;
        private string[] _tags; private bool _tagsInit;

        /// <summary>
        /// 构造一个带预热元数据的 handle。
        /// 当调用方已经知道 TypeId、DisplayName 或 Tags 时，可直接注入，减少首次访问的反射开销。
        /// </summary>
        public ItemHandle(Func<object> resolver, int? instanceId = null, string logicalId = null, int preTypeId = 0, string preName = null, string[] preTags = null)
        {
            _resolver = resolver ?? (() => null);
            InstanceId = instanceId;
            LogicalId = logicalId ?? (instanceId?.ToString());
            if (preTypeId != 0) { _typeId = preTypeId; _typeInit = true; }
            if (!string.IsNullOrEmpty(preName)) { _name = preName; _nameInit = true; }
            if (preTags != null) { _tags = preTags; _tagsInit = true; }
        }

        /// <summary>类型 ID 的懒缓存读取。</summary>
        public int TypeId { get { if (!_typeInit) { _typeInit = true; try { var raw = TryGetRaw(); _typeId = raw?.GetType().GetProperty("TypeID")?.GetValue(raw, null) is int v ? v : 0; } catch { _typeId = 0; } } return _typeId; } }

        /// <summary>显示名称的懒缓存读取。</summary>
        public string DisplayName { get { if (!_nameInit) { _nameInit = true; try { var raw = TryGetRaw(); _name = raw?.GetType().GetProperty("DisplayName")?.GetValue(raw, null) as string; } catch { _name = null; } } return _name; } }

        /// <summary>标签集合的懒缓存读取。</summary>
        public string[] Tags { get { if (!_tagsInit) { _tagsInit = true; try { var raw = TryGetRaw(); var tg = raw?.GetType().GetProperty("Tags")?.GetValue(raw, null) as System.Collections.IEnumerable; if (tg != null) { var list = new System.Collections.Generic.List<string>(); foreach (var x in tg) if (x != null) list.Add(x.ToString()); _tags = list.ToArray(); } else _tags = System.Array.Empty<string>(); } catch { _tags = System.Array.Empty<string>(); } } return _tags ?? System.Array.Empty<string>(); } }

        /// <summary>
        /// 刷新缓存元数据。
        /// 这个操作不会重建 handle，也不会改变逻辑身份，只会让后续 TypeId、DisplayName、Tags 重新从当前运行时对象读取一次。
        /// </summary>
        public void RefreshMetadata()
        {
            _typeInit = false; _nameInit = false; _tagsInit = false;
            var _ = TypeId; var __ = DisplayName; var ___ = Tags;
        }
    }
}
