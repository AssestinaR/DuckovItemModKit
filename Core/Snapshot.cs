using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
    /// <summary>
    /// 物品快照：捕获核心字段、变量、修饰、插槽与标签用于持久化或诊断。
    /// </summary>
    [Serializable]
    public sealed class ItemSnapshot
    {
        /// <summary>显示名称。</summary>
        public string Name;
        /// <summary>原始名称。</summary>
        public string NameRaw;
        /// <summary>类型 ID。</summary>
        public int TypeId;
        /// <summary>品质。</summary>
        public int Quality;
        /// <summary>显示品质。</summary>
        public int DisplayQuality;
        /// <summary>价值。</summary>
        public int Value;
        /// <summary>标签集合。</summary>
        public string[] Tags = Array.Empty<string>();
        /// <summary>变量集合。</summary>
        public VariableEntry[] Variables = Array.Empty<VariableEntry>();
        /// <summary>修饰集合。</summary>
        public ModifierEntry[] Modifiers = Array.Empty<ModifierEntry>();
        /// <summary>插槽集合。</summary>
        public SlotEntry[] Slots = Array.Empty<SlotEntry>();

        /// <summary>
        /// 捕获当前物品的核心状态（不含 Effects）。
        /// </summary>
        /// <param name="adapter">物品适配器。</param>
        /// <param name="item">目标物品。</param>
        /// <returns>快照实例。</returns>
        public static ItemSnapshot Capture(IItemAdapter adapter, object item)
        {
            if (adapter == null || item == null) return new ItemSnapshot();
            return new ItemSnapshot
            {
                Name = adapter.GetName(item),
                NameRaw = adapter.GetDisplayNameRaw(item),
                TypeId = adapter.GetTypeId(item),
                Quality = adapter.GetQuality(item),
                DisplayQuality = adapter.GetDisplayQuality(item),
                Value = adapter.GetValue(item),
                Tags = adapter.GetTags(item) ?? Array.Empty<string>(),
                Variables = adapter.GetVariables(item) ?? Array.Empty<VariableEntry>(),
                Modifiers = adapter.GetModifiers(item) ?? Array.Empty<ModifierEntry>(),
                Slots = adapter.GetSlots(item) ?? Array.Empty<SlotEntry>(),
            };
        }
    }

    /// <summary>
    /// 快照扩展：辅助格式化输出内容便于诊断。
    /// </summary>
    public static class ItemSnapshotExtensions
    {
        /// <summary>格式化输出快照内容，便于诊断。</summary>
        /// <param name="s">目标快照。</param>
        /// <returns>多行字符串。</returns>
        public static string ToPrettyString(this ItemSnapshot s)
        {
            if (s == null) return "<null>";
            var lines = new List<string>();
            lines.Add($"Name: {s.Name}");
            lines.Add($"NameRaw: {s.NameRaw}");
            lines.Add($"TypeId: {s.TypeId}");
            lines.Add($"Quality: {s.Quality} / Display: {s.DisplayQuality}");
            lines.Add($"Value: {s.Value}");
            lines.Add("Tags: [" + string.Join(", ", s.Tags ?? Array.Empty<string>()) + "]");
            lines.Add("Variables:");
            foreach (var v in s.Variables) lines.Add($" - {v.Key} = {v.Value}");
            lines.Add("Modifiers:");
            foreach (var m in s.Modifiers) lines.Add($" - {m.Key} {m.Modifier} {(m.IsPercent ? "(%)" : "")} = {m.Value}");
            lines.Add("Slots:");
            foreach (var sl in s.Slots) lines.Add($" - {sl.Key} Occupied={sl.Occupied} PlugType={sl.PlugType}");
            return string.Join(Environment.NewLine, lines);
        }
    }
}
