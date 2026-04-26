using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
    /// <summary>
    /// 持久化辅助：从快照构造最小化元数据。
    /// </summary>
    public static class Persistence
    {
        /// <summary>
        /// 根据快照构造基本元数据。
        /// 这里只补最核心、最稳定的字段；更复杂的 affix、扩展块和游戏特定内容仍交给适配器层补齐。
        /// </summary>
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
    /// 持久化恢复结果：承载恢复出的根物品、附加结果与共享诊断快照。
    /// 它对应的是 persistence 入口语义，内部仍应复用共享 restore 中段，
    /// 而不是让 persistence 再维护一套独立的节点实例化、树连接和附加实现。
    /// </summary>
    public sealed class PersistenceRestoreResult
    {
        /// <summary>恢复出的根物品；失败路径不会返回有效对象。</summary>
        public object RootItem { get; set; }

        /// <summary>是否已附加到目标宿主；为 false 时通常表示 detached restore 或 attach 失败。</summary>
        public bool Attached { get; set; }

        /// <summary>目标宿主是否已成功解析；当调用方提供 targetKey 时，这个字段可用于区分“目标未解析”与“解析成功但附加失败”。</summary>
        public bool TargetResolved { get; set; }

        /// <summary>附加索引；未知或不适用时为 -1。</summary>
        public int AttachedIndex { get; set; } = -1;

        /// <summary>实际使用的恢复策略。</summary>
        public string StrategyUsed { get; set; }

        /// <summary>可选共享诊断字典；通常包含 target、attach、fallback、strategy 等上下文。</summary>
        public Dictionary<string, object> Diagnostics { get; set; }
    }

    /// <summary>
    /// 物品元数据：保存核心属性、嵌入扩展 JSON 与校验。
    /// </summary>
    public sealed class ItemMeta
    {
        /// <summary>元数据版本，用于 IMK 自身迁移。</summary>
        public int MetaVersion { get; set; }

        /// <summary>扩展 JSON 或嵌入块的格式版本。</summary>
        public int FormatVersion { get; set; }

        /// <summary>名称键；优先记录 RawName 或底层名称键。</summary>
        public string NameKey { get; set; }

        /// <summary>描述键；没有描述时可为空。</summary>
        public string RemarkKey { get; set; }

        /// <summary>类型 ID，是 restore/rebirth 时最关键的建根字段之一。</summary>
        public int TypeId { get; set; }

        /// <summary>品质。</summary>
        public int Quality { get; set; }

        /// <summary>显示品质。</summary>
        public int DisplayQuality { get; set; }

        /// <summary>价值。</summary>
        public int Value { get; set; }

        /// <summary>所属者 ID，用于标记是哪个 mod 或系统写入了这份元数据。</summary>
        public string OwnerId { get; set; }

        /// <summary>嵌入的扩展 JSON 或 Base64 内容。</summary>
        public string EmbeddedJson { get; set; }

        /// <summary>扩展块校验和；用于校验 EmbeddedJson 是否被篡改或损坏。</summary>
        public string ExtraChecksum { get; set; }
    }
}
