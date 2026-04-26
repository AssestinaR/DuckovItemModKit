using System;
using System.Collections.Generic;
using System.Reflection;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 读取服务（Stats）：
    /// 负责 stats 快照读取以及原版 stat 目录枚举。
    /// </summary>
    internal sealed partial class ReadService
    {
        /// <summary>
        /// 读取 stats 快照。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <returns>成功返回 stats 快照；失败时返回对应错误码与错误信息。</returns>
        public RichResult<StatsSnapshot> TryReadStats(object item)
        {
            return TryReadStatsInternal(item);
        }

        /// <summary>
        /// 读取基础值视图。
        /// 当前实现与通用 stats 快照一致，但语义上强调 BaseValue 视角。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <returns>成功返回 stats 快照；失败时返回对应错误码与错误信息。</returns>
        public RichResult<StatsSnapshot> TryReadBaseStats(object item)
        {
            return TryReadStatsInternal(item);
        }

        /// <summary>
        /// 读取生效值视图。
        /// 当前实现与通用 stats 快照一致，但语义上强调 EffectiveValue 视角。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <returns>成功返回 stats 快照；失败时返回对应错误码与错误信息。</returns>
        public RichResult<StatsSnapshot> TryReadCurrentStats(object item)
        {
            return TryReadStatsInternal(item);
        }

        /// <summary>
        /// 枚举原版当前可用的 stat 字段目录。
        /// 返回项同时补带本地化 key 和当前语言名称，便于 UI/Probe 直接消费。
        /// </summary>
        /// <returns>成功返回 stat 目录列表；失败时返回对应错误码与错误信息。</returns>
        public RichResult<StatCatalogEntry[]> TryEnumerateAvailableStats()
        {
            try
            {
                var results = new List<StatCatalogEntry>();
                var stringListsType = DuckovTypeUtils.FindType("Duckov.Utilities.StringLists");
                if (stringListsType == null)
                {
                    return RichResult<StatCatalogEntry[]>.Fail(ErrorCode.DependencyMissing, "StringLists type missing");
                }

                var statKeysProp = stringListsType.GetProperty("StatKeys", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var statKeys = statKeysProp?.GetValue(null, null) as System.Collections.IEnumerable;
                if (statKeys == null)
                {
                    return RichResult<StatCatalogEntry[]>.Success(Array.Empty<StatCatalogEntry>());
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in statKeys)
                {
                    var key = Convert.ToString(entry)?.Trim();
                    if (string.IsNullOrEmpty(key) || !seen.Add(key)) continue;
                    var displayNameKey = DuckovLocalizedTextService.BuildStatLocalizationKey(key);
                    string localizedNameCurrent = null;
                    var localized = DuckovLocalizedTextService.TryRead(displayNameKey);
                    if (localized.Ok && localized.Value != null)
                    {
                        localizedNameCurrent = localized.Value.CurrentText;
                    }

                    results.Add(new StatCatalogEntry
                    {
                        Key = key,
                        DisplayNameKey = displayNameKey,
                        LocalizedNameCurrent = localizedNameCurrent
                    });
                }

                return RichResult<StatCatalogEntry[]>.Success(results.ToArray());
            }
            catch (Exception ex)
            {
                Log.Error("TryEnumerateAvailableStats failed", ex);
                return RichResult<StatCatalogEntry[]>.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>
        /// 统一执行 stats 快照读取。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <returns>成功返回 stats 快照；失败时返回对应错误码与错误信息。</returns>
        private RichResult<StatsSnapshot> TryReadStatsInternal(object item)
        {
            try
            {
                if (item == null) return RichResult<StatsSnapshot>.Fail(ErrorCode.InvalidArgument, "item is null");
                var snap = new StatsSnapshot();
                try
                {
                    var getStats = DuckovReflectionCache.GetGetter(item.GetType(), "Stats", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var stats = getStats?.Invoke(item) as System.Collections.IEnumerable;
                    if (stats != null)
                    {
                        var index = 0;
                        foreach (var st in stats)
                        {
                            if (st == null) continue;
                            string key = Convert.ToString(DuckovTypeUtils.GetMaybe(st, new[] { "Key", "key", "Name", "name" })) ?? string.Empty;
                            float baseValue = DuckovTypeUtils.ConvertToFloat(DuckovTypeUtils.GetMaybe(st, new[] { "BaseValue", "baseValue" }));
                            float effectiveValue = DuckovTypeUtils.ConvertToFloat(DuckovTypeUtils.GetMaybe(st, new[] { "Value", "value" }));
                            string displayNameKey = Convert.ToString(DuckovTypeUtils.GetMaybe(st, new[] { "DisplayNameKey", "displayNameKey" }));
                            if (string.IsNullOrEmpty(displayNameKey) && !string.IsNullOrEmpty(key))
                            {
                                displayNameKey = DuckovLocalizedTextService.BuildStatLocalizationKey(key);
                            }

                            string localizedNameCurrent = null;
                            if (!string.IsNullOrEmpty(displayNameKey))
                            {
                                var localized = DuckovLocalizedTextService.TryRead(displayNameKey);
                                if (localized.Ok && localized.Value != null)
                                {
                                    localizedNameCurrent = localized.Value.CurrentText;
                                }
                            }

                            snap.Entries.Add(new StatValueEntry
                            {
                                Key = key,
                                Index = index++,
                                BaseValue = baseValue,
                                EffectiveValue = effectiveValue,
                                DisplayNameKey = displayNameKey,
                                LocalizedNameCurrent = localizedNameCurrent
                            });
                        }
                    }
                }
                catch { }
                return RichResult<StatsSnapshot>.Success(snap);
            }
            catch (Exception ex) { Log.Error("TryReadStats failed", ex); return RichResult<StatsSnapshot>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
    }
}