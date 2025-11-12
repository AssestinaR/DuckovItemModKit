using System;

namespace ItemModKit.Core
{
    /// <summary>
    /// 持久化辅助：从快照构造最小化元数据。
    /// </summary>
    public static class Persistence
    {
        /// <summary>根据快照构造基本元数据。</summary>
        public static ItemMeta BuildMetaFromSnapshot(ItemSnapshot s)
        {
            return new ItemMeta
            {
                NameKey = s?.NameRaw ?? s?.Name,
                TypeId = s?.TypeId ?? 0,
                Quality = s?.Quality ?? 0,
                DisplayQuality = s?.DisplayQuality ?? 0,
                Value = s?.Value ?? 0,
                // Affixes left to adapters to fill (game-specific)
            };
        }
    }

    /// <summary>
    /// 物品元数据：保存核心属性、嵌入扩展 JSON 与校验。
    /// </summary>
    public sealed class ItemMeta
    {
        /// <summary>元数据版本（用于迁移）。</summary>
        public int MetaVersion { get; set; }
        /// <summary>扩展 JSON 模式版本。</summary>
        public int FormatVersion { get; set; }
        /// <summary>名称键（优先使用 Raw）。</summary>
        public string NameKey { get; set; }
        /// <summary>描述键（可选）。</summary>
        public string RemarkKey { get; set; }
        /// <summary>类型 ID。</summary>
        public int TypeId { get; set; }
        /// <summary>品质。</summary>
        public int Quality { get; set; }
        /// <summary>显示品质。</summary>
        public int DisplayQuality { get; set; }
        /// <summary>价值。</summary>
        public int Value { get; set; }
        /// <summary>所属者 ID（哪个 Mod 拥有该元数据）。</summary>
        public string OwnerId { get; set; }
        /// <summary>嵌入的扩展 JSON 或 Base64。</summary>
        public string EmbeddedJson { get; set; }
        /// <summary>扩展块校验和。</summary>
        public string ExtraChecksum { get; set; }
    }
}
