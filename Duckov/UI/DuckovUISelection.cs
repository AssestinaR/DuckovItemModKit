using System;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// UI 选中项访问器：尝试从“物品详情面板”和“操作菜单”解析当前选中的物品。
    /// 内部使用弱引用缓存实例，避免频繁查找。
    /// </summary>
    [Obsolete("新代码优先使用 IMKDuckov.UISelection 兼容选中项门面或 IMKDuckov.UISelectionV2。", false)]
    internal sealed class DuckovUISelection : IUISelection
    {
        private static readonly DuckovCompatUISelectionFacade s_facade = new DuckovCompatUISelectionFacade();

        /// <summary>从详情面板获取选中物品。</summary>
        public bool TryGetDetailsItem(out object item) => s_facade.TryGetDetailsItem(out item);
        /// <summary>从操作菜单获取选中物品。</summary>
        public bool TryGetOperationMenuItem(out object item) => s_facade.TryGetOperationMenuItem(out item);
        /// <summary>优先菜单项，其次详情面板。</summary>
        public bool TryGetCurrentItem(out object item) => s_facade.TryGetCurrentItem(out item);
    }
}
