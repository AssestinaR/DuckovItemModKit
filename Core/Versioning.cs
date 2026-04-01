using System;

namespace ItemModKit.Core
{
    /// <summary>
    /// IMK 能力标记（按位组合）。
    /// 用于让外部调用方在运行时判断当前宿主暴露了哪些稳定能力，而不是靠反射或版本字符串硬猜。
    /// </summary>
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
        /// <summary>Stage 1 兼容查询入口。</summary>
        Query = 1 << 3,
        /// <summary>持久化。</summary>
        Persistence = 1 << 4,
        /// <summary>重生。</summary>
        Rebirth = 1 << 5,
        /// <summary>UI 选中读取能力。</summary>
        UISelection = 1 << 6,
        /// <summary>背包事件。</summary>
        InventoryEvents = 1 << 7,
        /// <summary>世界掉落事件。</summary>
        WorldDrops = 1 << 8,
        /// <summary>归属、宿主链与所有权查询能力。</summary>
        Ownership = 1 << 9,
        /// <summary>外部事件发布与手动事件模式。</summary>
        ExternalEventPublishing = 1 << 10,
        /// <summary>互斥与锁能力。</summary>
        Mutex = 1 << 11,
        /// <summary>日志。</summary>
        Logging = 1 << 12,
        /// <summary>统一 RichResult 结果语义。</summary>
        RichResults = 1 << 13,
        /// <summary>rebirth 报告与诊断导出。</summary>
        RebirthReports = 1 << 14
    }

    /// <summary>
    /// IMK 版本与能力入口。
    /// 外部 mod 应优先通过这里做版本下限校验和 capability gating。
    /// </summary>
    public static class IMKVersion
    {
        /// <summary>当前公开版本号。</summary>
        public static readonly Version Version = new Version(1, 0, 0);

        /// <summary>当前宿主公开声明支持的能力集合。</summary>
        public static readonly IMKCapabilities Capabilities =
        IMKCapabilities.ItemAdapter | IMKCapabilities.InventoryAdapter | IMKCapabilities.SlotAdapter |
        IMKCapabilities.Query | IMKCapabilities.Persistence | IMKCapabilities.Rebirth | IMKCapabilities.UISelection |
        IMKCapabilities.InventoryEvents | IMKCapabilities.WorldDrops | IMKCapabilities.Ownership |
        IMKCapabilities.ExternalEventPublishing | IMKCapabilities.Mutex | IMKCapabilities.Logging | IMKCapabilities.RichResults |
        IMKCapabilities.RebirthReports;

        /// <summary>确保满足最低版本要求；不满足时返回 false 并输出可直接展示的错误文本。</summary>
        public static bool Require(Version min, out string error)
        {
            if (Version >= min) { error = null; return true; }
            error = $"IMK version {Version} < required {min}"; return false;
        }
    }
}
