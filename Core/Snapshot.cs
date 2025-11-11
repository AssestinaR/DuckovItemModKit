using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
 [Serializable]
 public sealed class ItemSnapshot
 {
 public string Name;
 public string NameRaw;
 public int TypeId;
 public int Quality;
 public int DisplayQuality;
 public int Value;
 public string[] Tags = Array.Empty<string>();
 public VariableEntry[] Variables = Array.Empty<VariableEntry>();
 public ModifierEntry[] Modifiers = Array.Empty<ModifierEntry>();
 public SlotEntry[] Slots = Array.Empty<SlotEntry>();

 /// <summary>
 /// 捕获当前物品的核心状态（不含 Effects）。
 /// </summary>
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

 public static class ItemSnapshotExtensions
 {
 /// <summary>格式化输出快照内容，便于诊断。</summary>
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
 foreach (var m in s.Modifiers) lines.Add($" - {m.Key} {m.Modifier} {(m.IsPercent?"(%)":"")} = {m.Value}");
 lines.Add("Slots:");
 foreach (var sl in s.Slots) lines.Add($" - {sl.Key} Occupied={sl.Occupied} PlugType={sl.PlugType}");
 return string.Join(Environment.NewLine, lines);
 }
 }
}
