using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
    /// <summary>
    /// 克隆选项：控制哪些域从源复制到目标。
    /// Core: 名称/品质/显示品质/价值；可选 TypeId
    /// Variables/Constants/Tags: 对应集合
    /// CloneAffixes: 快捷启用 Stats + Modifiers
    /// Stats/Modifiers: 细粒度控制（可与 CloneAffixes 组合）
    /// </summary>
    public sealed class CloneOptions
    {
        public bool Core = true;              // Name/Value/Quality/DisplayQuality/TypeId (TypeId optional)
        public bool IncludeTypeId = false;    // Usually false to keep target type
        public bool Variables = true;
        public bool Constants = true;
        public bool Tags = true;
        public bool CloneAffixes = false;     // when true, will try to clone Stats + Modifiers
        public bool Stats = false;            // fine-grained control; effective if CloneAffixes or explicitly true
        public bool Modifiers = false;        // fine-grained control; effective if CloneAffixes or explicitly true
    }

    /// <summary>
    /// 克隆器：提供从模板复制到目标物品的多域复制逻辑。
    /// 支持基础域、变量/常量/标签以及可选的 Stat 与 Modifier。
    /// </summary>
    public static class Cloner
    {
        /// <summary>基础克隆（不含 affix）。</summary>
        public static RichResult CloneFromTemplate(IItemAdapter adapter, IWriteService writer, object source, object target, CloneOptions options = null)
        {
            return CloneFromTemplate(adapter, writer, reader: null, source: source, target: target, options: options);
        }

        /// <summary>扩展克隆：若提供 reader 可复制 Stat 与 Modifier（含描述）。</summary>
        public static RichResult CloneFromTemplate(IItemAdapter adapter, IWriteService writer, IReadService reader, object source, object target, CloneOptions options = null)
        {
            if (adapter == null || writer == null || source == null || target == null)
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
    }
}
