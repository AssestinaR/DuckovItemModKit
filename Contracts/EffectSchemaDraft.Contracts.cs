using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
    /// <summary>
    /// effects schema draft 的分类。
    /// </summary>
    public enum EffectSchemaDraftCategory
    {
        /// <summary>Effect 本体类型。</summary>
        Effect,
        /// <summary>Trigger 组件类型。</summary>
        Trigger,
        /// <summary>Filter 组件类型。</summary>
        Filter,
        /// <summary>Action 组件类型。</summary>
        Action,
    }

    /// <summary>
    /// effects schema draft 的单个成员摘要。
    /// </summary>
    [Serializable]
    public sealed class EffectSchemaMemberDraft
    {
        /// <summary>成员名。</summary>
        public string Name { get; set; }

        /// <summary>成员类型全名。</summary>
        public string TypeName { get; set; }

        /// <summary>成员来源：Property 或 Field。</summary>
        public string SourceKind { get; set; }

        /// <summary>是否可写。</summary>
        public bool Writable { get; set; }

        /// <summary>是否公开。</summary>
        public bool Public { get; set; }

        /// <summary>是否显式序列化字段。</summary>
        public bool SerializedField { get; set; }
    }

    /// <summary>
    /// effects schema draft 的单个类型条目。
    /// </summary>
    [Serializable]
    public sealed class EffectSchemaEntryDraft
    {
        /// <summary>分类。</summary>
        public EffectSchemaDraftCategory Category { get; set; }

        /// <summary>类型全名。</summary>
        public string TypeFullName { get; set; }

        /// <summary>短类型名。</summary>
        public string TypeName { get; set; }

        /// <summary>所属程序集名。</summary>
        public string AssemblyName { get; set; }

        /// <summary>是否抽象类型。</summary>
        public bool Abstract { get; set; }

        /// <summary>可用成员集合。</summary>
        public List<EffectSchemaMemberDraft> Members { get; set; } = new List<EffectSchemaMemberDraft>();
    }

    /// <summary>
    /// effects schema draft 的总目录。
    /// 这是 stock effect graph 的结构视图，不是完整 runtime gameplay contract，也不承诺 richer combat payload。
    /// </summary>
    [Serializable]
    public sealed class EffectSchemaCatalogDraft
    {
        /// <summary>生成时刻（UTC）。</summary>
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>是否包含抽象类型。</summary>
        public bool IncludeAbstractTypes { get; set; }

        /// <summary>effect 本体类型集合。</summary>
        public List<EffectSchemaEntryDraft> Effects { get; set; } = new List<EffectSchemaEntryDraft>();

        /// <summary>trigger 类型集合。</summary>
        public List<EffectSchemaEntryDraft> Triggers { get; set; } = new List<EffectSchemaEntryDraft>();

        /// <summary>filter 类型集合。</summary>
        public List<EffectSchemaEntryDraft> Filters { get; set; } = new List<EffectSchemaEntryDraft>();

        /// <summary>action 类型集合。</summary>
        public List<EffectSchemaEntryDraft> Actions { get; set; } = new List<EffectSchemaEntryDraft>();
    }
}