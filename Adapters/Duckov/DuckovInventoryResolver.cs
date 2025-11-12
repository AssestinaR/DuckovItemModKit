using System.Reflection;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 背包解析服务：根据字符串标识解析到具体背包对象。
    /// 支持 "character"/空值 -> 主角背包；"storage" -> 仓库背包；否则返回 null。
    /// </summary>
    internal sealed class DuckovInventoryResolver : IInventoryResolver
    {
        /// <summary>
        /// 按目标标识解析背包。
        /// </summary>
        /// <param name="target">目标标识：character/storage/null(auto)。</param>
        /// <returns>解析到的背包对象或 null。</returns>
        public object Resolve(string target)
        {
            if (string.IsNullOrEmpty(target) || string.Equals(target, "character", System.StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var tLM = DuckovTypeUtils.FindType("TeamSoda.Duckov.Core.LevelManager") ?? DuckovTypeUtils.FindType("LevelManager");
                    var pInst = tLM?.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    var lm = pInst?.GetValue(null, null);
                    var pMain = lm?.GetType().GetProperty("MainCharacter", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var main = pMain?.GetValue(lm, null);
                    var pCharItem = main?.GetType().GetProperty("CharacterItem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var chItem = pCharItem?.GetValue(main, null);
                    var pInv = chItem?.GetType().GetProperty("Inventory", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var inv2 = pInv?.GetValue(chItem, null);
                    if (inv2 != null) return inv2;
                }
                catch { }
            }
            if (string.Equals(target, "storage", System.StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var tPS = DuckovTypeUtils.FindType("TeamSoda.Duckov.Core.PlayerStorage") ?? DuckovTypeUtils.FindType("PlayerStorage");
                    var pInv = tPS?.GetProperty("Inventory", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    var inv = pInv?.GetValue(null, null);
                    if (inv != null) return inv;
                }
                catch { }
            }
            return null;
        }

        /// <summary>解析默认背包（主角背包）。</summary>
        public object ResolveFallback()
        {
            return Resolve(null);
        }
    }
}
