using System;

namespace ItemModKit.Core
{
    /// <summary>IMK 能力标记（按位组合）。</summary>
    [Flags]
    public enum IMKCapabilities
    {
        /// <summary>无。</summary>
        None = 0,
        /// <summary>物品适配器。</summary>
        ItemAdapter = 1 << 0,
        /// <summary>背包适配器。</summary>
        InventoryAdapter = 1 << 1,
        /// <summary>槽位适配器。</summary>
        SlotAdapter = 1 << 2,
        /// <summary>查询。</summary>
        Query = 1 << 3,
        /// <summary>持久化。</summary>
        Persistence = 1 << 4,
        /// <summary>重生。</summary>
        Rebirth = 1 << 5,
        /// <summary>UI 选中。</summary>
        UISelection = 1 << 6,
        /// <summary>背包事件。</summary>
        InventoryEvents = 1 << 7,
        /// <summary>世界掉落事件。</summary>
        WorldDrops = 1 << 8,
        /// <summary>归属与所有权。</summary>
        Ownership = 1 << 9,
        /// <summary>外部事件发布。</summary>
        ExternalEventPublishing = 1 << 10,
        /// <summary>互斥。</summary>
        Mutex = 1 << 11,
        /// <summary>日志。</summary>
        Logging = 1 << 12,
        /// <summary>统一结果类型。</summary>
        RichResults = 1 << 13
    }

    /// <summary>IMK 版本与能力。</summary>
    public static class IMKVersion
    {
        /// <summary>当前版本。</summary>
        public static readonly Version Version = new Version(0, 1, 0);
        /// <summary>当前能力集合。</summary>
        public static readonly IMKCapabilities Capabilities =
        IMKCapabilities.ItemAdapter | IMKCapabilities.InventoryAdapter | IMKCapabilities.SlotAdapter |
        IMKCapabilities.Query | IMKCapabilities.Persistence | IMKCapabilities.Rebirth | IMKCapabilities.UISelection |
        IMKCapabilities.InventoryEvents | IMKCapabilities.WorldDrops | IMKCapabilities.Ownership |
        IMKCapabilities.ExternalEventPublishing | IMKCapabilities.Mutex | IMKCapabilities.Logging | IMKCapabilities.RichResults;

        /// <summary>确保满足最低版本要求，不满足时输出错误。</summary>
        public static bool Require(Version min, out string error)
        {
            if (Version >= min) { error = null; return true; }
            error = $"IMK version {Version} < required {min}"; return false;
        }
    }
}
