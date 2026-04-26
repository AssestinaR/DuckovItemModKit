using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 读取服务（LocalizedTexts）：提供原版本地化键与 stat 显示键的只读查询。
    /// </summary>
    internal sealed partial class ReadService
    {
        /// <summary>按本地化键读取当前语言文本。</summary>
        public RichResult<LocalizedTextSnapshot> TryReadLocalizedText(string localizationKey)
        {
            return DuckovLocalizedTextService.TryRead(localizationKey);
        }

        /// <summary>按本地化键读取全部可用语言文本。</summary>
        public RichResult<LocalizedTextSnapshot> TryReadAllLocalizedTexts(string localizationKey)
        {
            return DuckovLocalizedTextService.TryReadAll(localizationKey);
        }

        /// <summary>按 stat key 读取当前语言显示文本。</summary>
        public RichResult<LocalizedTextSnapshot> TryReadStatLocalizedText(string statKey)
        {
            var localizationKey = DuckovLocalizedTextService.BuildStatLocalizationKey(statKey);
            if (string.IsNullOrEmpty(localizationKey)) return RichResult<LocalizedTextSnapshot>.Fail(ErrorCode.InvalidArgument, "statKey is null");
            return DuckovLocalizedTextService.TryRead(localizationKey);
        }

        /// <summary>按 stat key 读取全部可用语言显示文本。</summary>
        public RichResult<LocalizedTextSnapshot> TryReadAllStatLocalizedTexts(string statKey)
        {
            var localizationKey = DuckovLocalizedTextService.BuildStatLocalizationKey(statKey);
            if (string.IsNullOrEmpty(localizationKey)) return RichResult<LocalizedTextSnapshot>.Fail(ErrorCode.InvalidArgument, "statKey is null");
            return DuckovLocalizedTextService.TryReadAll(localizationKey);
        }
    }
}