using System;
using ItemModKit.Core;
using SodaCraft.Localizations;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// Duckov 本地化桥接：负责按键读取当前语言或全部语言文本。
    /// 当前仅提供只读能力，后续 slots/stats/effects 可复用同一解析层。
    /// </summary>
    internal static class DuckovLocalizedTextService
    {
        /// <summary>把 stat key 规范化为原版本地化键。</summary>
        public static string BuildStatLocalizationKey(string statKey)
        {
            if (string.IsNullOrWhiteSpace(statKey)) return null;
            return "Stat_" + statKey.Trim();
        }

        /// <summary>读取当前语言文本。</summary>
        public static RichResult<LocalizedTextSnapshot> TryRead(string localizationKey)
        {
            return TryReadInternal(localizationKey, includeAllLanguages: false);
        }

        /// <summary>读取全部语言文本。</summary>
        public static RichResult<LocalizedTextSnapshot> TryReadAll(string localizationKey)
        {
            return TryReadInternal(localizationKey, includeAllLanguages: true);
        }

        private static RichResult<LocalizedTextSnapshot> TryReadInternal(string localizationKey, bool includeAllLanguages)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(localizationKey))
                {
                    return RichResult<LocalizedTextSnapshot>.Fail(ErrorCode.InvalidArgument, "localizationKey is null");
                }

                var normalizedKey = localizationKey.Trim();
                var snapshot = new LocalizedTextSnapshot
                {
                    Key = normalizedKey,
                    CurrentLanguage = LocalizationManager.CurrentLanguage.ToString(),
                    CurrentText = LocalizationManager.GetPlainText(normalizedKey)
                };

                if (!includeAllLanguages)
                {
                    return RichResult<LocalizedTextSnapshot>.Success(snapshot);
                }

                var database = LocalizationDatabase.Instance;
                if (database == null)
                {
                    return RichResult<LocalizedTextSnapshot>.Success(snapshot);
                }

                foreach (UnityEngine.SystemLanguage language in Enum.GetValues(typeof(UnityEngine.SystemLanguage)))
                {
                    var entry = database.GetEntry(language);
                    if (entry == null) continue;
                    string text = null;
                    try { text = entry.GetPlainText(normalizedKey); } catch { }
                    snapshot.Entries.Add(new LocalizedTextEntry
                    {
                        Language = language.ToString(),
                        Text = text
                    });
                }

                return RichResult<LocalizedTextSnapshot>.Success(snapshot);
            }
            catch (Exception ex)
            {
                Log.Error("TryRead localized text failed", ex);
                return RichResult<LocalizedTextSnapshot>.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }
    }
}