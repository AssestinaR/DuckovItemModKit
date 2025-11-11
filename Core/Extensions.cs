using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ItemModKit.Core
{
    [Flags]
    public enum ContributorPhase
    {
        None = 0,
        Capture = 1,
        Enrich = 2,
        Apply = 4
    }

    public interface IItemStateContributor
    {
        string Key { get; }
        DirtyKind KindMask { get; }
        object TryCapture(object item, ItemSnapshot baseSnapshot);
    }

    public interface IMetaEnricher
    {
        bool TryEnrich(object item, ItemMeta meta, IDictionary<string, object> extra);
    }

    public interface IItemStateApplier
    {
        string Key { get; }
        void TryApply(object item, ItemMeta meta, JToken fragment);
    }

    public static class ItemStateExtensions
    {
        private static readonly List<IItemStateContributor> s_contributors = new List<IItemStateContributor>();
        private static readonly List<IMetaEnricher> s_enrichers = new List<IMetaEnricher>();
        private static readonly List<IItemStateApplier> s_appliers = new List<IItemStateApplier>();

        public static void RegisterContributor(IItemStateContributor c)
        {
            if (c == null) return; lock (s_contributors) s_contributors.Add(c);
        }
        public static void RegisterEnricher(IMetaEnricher e)
        {
            if (e == null) return; lock (s_enrichers) s_enrichers.Add(e);
        }
        public static void RegisterApplier(IItemStateApplier a)
        {
            if (a == null) return; lock (s_appliers) s_appliers.Add(a);
        }

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

        public static void Enrich(object item, ItemMeta meta, IDictionary<string, object> extra)
        {
            if (item == null || meta == null || extra == null) return;
            List<IMetaEnricher> copy; lock (s_enrichers) copy = new List<IMetaEnricher>(s_enrichers);
            foreach (var e in copy)
            {
                try { e.TryEnrich(item, meta, extra); } catch { }
            }
        }

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
