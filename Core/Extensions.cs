using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ItemModKit.Core
{
    /// <summary>贡献阶段标记。</summary>
    [Flags]
    public enum ContributorPhase
    {
        /// <summary>无。</summary>
        None = 0,
        /// <summary>捕获阶段。</summary>
        Capture = 1,
        /// <summary>富集阶段。</summary>
        Enrich = 2,
        /// <summary>应用阶段。</summary>
        Apply = 4
    }

    /// <summary>状态贡献者：在捕获阶段输出附加片段。</summary>
    public interface IItemStateContributor
    {
        /// <summary>贡献键（用于 JSON 片段命名）。</summary>
        string Key { get; }
        /// <summary>生效的脏掩码。</summary>
        DirtyKind KindMask { get; }
        /// <summary>尝试捕获扩展片段。</summary>
        object TryCapture(object item, ItemSnapshot baseSnapshot);
    }

    /// <summary>元数据富集器：在富集阶段补充 ItemMeta。</summary>
    public interface IMetaEnricher
    {
        /// <summary>尝试富集元数据。</summary>
        bool TryEnrich(object item, ItemMeta meta, IDictionary<string, object> extra);
    }

    /// <summary>状态应用器：从 JSON 片段恢复到物品。</summary>
    public interface IItemStateApplier
    {
        /// <summary>片段键。</summary>
        string Key { get; }
        /// <summary>尝试应用片段。</summary>
        void TryApply(object item, ItemMeta meta, JToken fragment);
    }

    /// <summary>扩展注册与分发工具。</summary>
    public static class ItemStateExtensions
    {
        private static readonly List<IItemStateContributor> s_contributors = new List<IItemStateContributor>();
        private static readonly List<IMetaEnricher> s_enrichers = new List<IMetaEnricher>();
        private static readonly List<IItemStateApplier> s_appliers = new List<IItemStateApplier>();

        /// <summary>注册贡献者。</summary>
        public static void RegisterContributor(IItemStateContributor c)
        {
            if (c == null) return; lock (s_contributors) s_contributors.Add(c);
        }
        /// <summary>注册富集器。</summary>
        public static void RegisterEnricher(IMetaEnricher e)
        {
            if (e == null) return; lock (s_enrichers) s_enrichers.Add(e);
        }
        /// <summary>注册应用器。</summary>
        public static void RegisterApplier(IItemStateApplier a)
        {
            if (a == null) return; lock (s_appliers) s_appliers.Add(a);
        }

        /// <summary>分发捕获：收集扩展片段。</summary>
        public static void Contribute(object item, ItemSnapshot snapshot, DirtyKind dirtyMask, IDictionary<string, object> extra)
        {
            if (item == null || extra == null) return;
            List<IItemStateContributor> copy; lock (s_contributors) copy = new List<IItemStateContributor>(s_contributors);
            foreach (var c in copy)
            {
                try
                {
                    if ((c.KindMask & dirtyMask) == 0) continue;
                    var v = c.TryCapture(item, snapshot);
                    if (v != null) extra[c.Key] = v;
                }
                catch { }
            }
        }

        /// <summary>分发富集：基于 extra 调整元数据。</summary>
        public static void Enrich(object item, ItemMeta meta, IDictionary<string, object> extra)
        {
            if (item == null || meta == null || extra == null) return;
            List<IMetaEnricher> copy; lock (s_enrichers) copy = new List<IMetaEnricher>(s_enrichers);
            foreach (var e in copy)
            {
                try { e.TryEnrich(item, meta, extra); } catch { }
            }
        }

        /// <summary>尝试应用扩展片段到物品。</summary>
        public static void TryApply(object item, ItemMeta meta)
        {
            if (item == null || meta == null) return;
            var raw = meta.EmbeddedJson;
            if (string.IsNullOrEmpty(raw)) return;
            string json = raw;
            try
            {
                if (!raw.TrimStart().StartsWith("{") && !raw.TrimStart().StartsWith("["))
                {
                    // possibly base64
                    try { json = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(raw)); } catch { json = raw; }
                }
                var root = JToken.Parse(json);
                if (!(root is JObject obj)) return;
                List<IItemStateApplier> copy; lock (s_appliers) copy = new List<IItemStateApplier>(s_appliers);
                foreach (var a in copy)
                {
                    try
                    {
                        if (obj.TryGetValue(a.Key, out var token))
                        {
                            a.TryApply(item, meta, token);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
