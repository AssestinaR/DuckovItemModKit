using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
    /// <summary>
    /// 克隆选项：控制从源物品复制到目标物品的各域行为。
    /// </summary>
    public sealed class CloneOptions
    {
        /// <summary>是否复制核心字段 (Name/RawName/Quality/DisplayQuality/Value/TypeId)。</summary>
        public bool Core = true;
        /// <summary>是否包含 TypeId（通常为 false，以保留目标原类型）。</summary>
        public bool IncludeTypeId = false;
        /// <summary>是否复制变量集合。</summary>
        public bool Variables = true;
        /// <summary>是否复制常量集合。</summary>
        public bool Constants = true;
        /// <summary>是否复制标签集合。</summary>
        public bool Tags = true;
        /// <summary>快捷启用 Stats + Modifiers 的聚合标记。</summary>
        public bool CloneAffixes = false;
        /// <summary>是否复制统计 (Stats)；可与 CloneAffixes 组合。</summary>
        public bool Stats = false;
        /// <summary>是否复制修饰 (Modifiers)；可与 CloneAffixes 组合。</summary>
        public bool Modifiers = false;
    }

    /// <summary>
    /// 克隆器：提供从模板源复制多域到目标物品的逻辑，支持扩展域（Stats/Modifiers）。
    /// </summary>
    public static class Cloner
    {
        /// <summary>
        /// 基础克隆（不含统计与修饰）。
        /// </summary>
        /// <param name="adapter">物品适配器。</param>
        /// <param name="writer">写入服务。</param>
        /// <param name="source">源物品。</param>
        /// <param name="target">目标物品。</param>
        /// <param name="options">克隆选项（可 null 使用默认）。</param>
        /// <returns>操作结果。</returns>
        public static RichResult CloneFromTemplate(IItemAdapter adapter, IWriteService writer, object source, object target, CloneOptions options = null)
        {
            return CloneFromTemplate(adapter, writer, reader: null, source: source, target: target, options: options);
        }

        /// <summary>
        /// 扩展克隆：在提供 reader 时可复制 Stats 与 Modifier 描述。
        /// </summary>
        /// <param name="adapter">物品适配器。</param>
        /// <param name="writer">写入服务。</param>
        /// <param name="reader">读取服务（用于 Stats/Modifiers 描述）。</param>
        /// <param name="source">源物品。</param>
        /// <param name="target">目标物品。</param>
        /// <param name="options">克隆选项。</param>
        /// <returns>结果（成功或错误码）。</returns>
        public static RichResult CloneFromTemplate(IItemAdapter adapter, IWriteService writer, IReadService reader, object source, object target, CloneOptions options = null)
        {
            if (AdapterOrArgsInvalid(adapter, writer, source, target))
                return RichResult.Fail(ErrorCode.InvalidArgument, "null args");
            options = options ?? new CloneOptions();

            try
            {
                // Core fields
                if (options.Core)
                {
                    var core = new CoreFieldChanges
                    {
                        Name = adapter.GetDisplayNameRaw(source),
                        RawName = adapter.GetDisplayNameRaw(source),
                        Quality = adapter.GetQuality(source),
                        DisplayQuality = adapter.GetDisplayQuality(source),
                        Value = adapter.GetValue(source),
                        TypeId = options.IncludeTypeId ? (int?)adapter.GetTypeId(source) : null
                    };
                    var r = writer.TryWriteCoreFields(target, core);
                    if (!r.Ok) return r;
                }

                // Variables
                if (options.Variables)
                {
                    var vars = adapter.GetVariables(source);
                    if (vars != null)
                    {
                        var list = new List<KeyValuePair<string, object>>(vars.Length);
                        foreach (var v in vars)
                            list.Add(new KeyValuePair<string, object>(v.Key, v.Value));
                        var r = writer.TryWriteVariables(target, list, overwrite: true);
                        if (!r.Ok) return r;
                    }
                }

                // Constants
                if (options.Constants)
                {
                    var cons = adapter.GetConstants(source);
                    if (cons != null)
                    {
                        var list = new List<KeyValuePair<string, object>>(cons.Length);
                        foreach (var c in cons)
                            list.Add(new KeyValuePair<string, object>(c.Key, c.Value));
                        var r = writer.TryWriteConstants(target, list, createIfMissing: true);
                        if (!r.Ok) return r;
                    }
                }

                // Tags
                if (options.Tags)
                {
                    var tags = adapter.GetTags(source) ?? Array.Empty<string>();
                    var r = writer.TryWriteTags(target, tags, merge: false);
                    if (!r.Ok) return r;
                }

                // Affixes (Stats + Modifiers)
                bool doStats = options.CloneAffixes || options.Stats;
                bool doMods = options.CloneAffixes || options.Modifiers;

                if (doStats && reader != null)
                {
                    var statRes = reader.TryReadStats(source);
                    if (statRes.Ok && statRes.Value != null && statRes.Value.Entries != null)
                    {
                        foreach (var s in statRes.Value.Entries)
                        {
                            var rEnsure = writer.TryEnsureStat(target, s.Key, s.Value);
                            if (!rEnsure.Ok) return rEnsure;
                            var rSet = writer.TrySetStatValue(target, s.Key, s.Value);
                            if (!rSet.Ok) return rSet;
                        }
                    }
                }

                if (doMods)
                {
                    bool usedDescriptions = false;
                    if (reader != null)
                    {
                        var mdRes = reader.TryReadModifierDescriptions(source);
                        if (mdRes.Ok && mdRes.Value != null && mdRes.Value.Length > 0)
                        {
                            usedDescriptions = true;
                            var clr = writer.TryClearModifierDescriptions(target);
                            if (!clr.Ok) return clr;
                            foreach (var d in mdRes.Value)
                            {
                                if (string.IsNullOrEmpty(d.Key)) continue; // skip invalid entries
                                var add = writer.TryAddModifierDescription(target, d.Key, d.Type, d.Value, d.Display, d.Order, d.Target);
                                if (!add.Ok) return add;
                            }
                            var reap = writer.TryReapplyModifiers(target);
                            if (!reap.Ok) return reap;
                        }
                    }
                    if (!usedDescriptions)
                    {
                        var mods = adapter.GetModifiers(source);
                        if (mods != null)
                        {
                            foreach (var m in mods)
                            {
                                var rAdd = writer.TryAddModifier(target, m.Key, m.Value, m.IsPercent, m.Modifier, source: null);
                                if (!rAdd.Ok) return rAdd;
                            }
                        }
                    }
                }

                return RichResult.Success();
            }
            catch (Exception ex)
            {
                Log.Error("CloneFromTemplate failed", ex);
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>检查适配器或输入对象是否为 null。</summary>
        private static bool AdapterOrArgsInvalid(IItemAdapter adapter, IWriteService writer, object source, object target)
        {
            return adapter == null || writer == null || source == null || target == null;
        }
    }
}
