using System;

namespace ItemModKit.Core
{
    /// <summary>事件来源类型。</summary>
    public enum ItemEventSourceType
    {
        /// <summary>来自背包。</summary>
        Backpack,
        /// <summary>来自仓库。</summary>
        Storage,
        /// <summary>来自槽位。</summary>
        Slot,
        /// <summary>来自世界（地面）。</summary>
        World,
        /// <summary>其他来源。</summary>
        Other
    }
    /// <summary>事件原因分类。</summary>
    public enum ItemEventCause
    {
        /// <summary>合成/制造。</summary>
        Craft,
        /// <summary>拾取/战利品。</summary>
        Loot,
        /// <summary>移动位置。</summary>
        Move,
        /// <summary>堆叠合并。</summary>
        Merge,
        /// <summary>堆叠拆分。</summary>
        Split,
        /// <summary>购买。</summary>
        Buy,
        /// <summary>出售。</summary>
        Sell,
        /// <summary>装备。</summary>
        Equip,
        /// <summary>卸下装备。</summary>
        Unequip,
        /// <summary>其他。</summary>
        Other
    }

    /// <summary>
    /// 事件上下文：用于事件发布与诊断。
    /// </summary>
    public sealed class ItemEventContext
    {
        /// <summary>来源类型。</summary>
        public ItemEventSourceType Source { get; set; }
        /// <summary>事件原因。</summary>
        public ItemEventCause Cause { get; set; }
        /// <summary>可选索引（如背包位置）。</summary>
        public int? Index { get; set; }
        /// <summary>所属者标识。</summary>
        public string OwnerId { get; set; }
        /// <summary>时间戳（unscaled 秒）。</summary>
        public float Timestamp { get; set; } // unscaled time seconds
    }
}
