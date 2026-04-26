using System.Collections.Generic;
using ItemModKit.Core;
using static ItemModKit.Adapters.Duckov.DuckovTypeUtils;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// Duckov 槽位适配器：
    /// 提供面向角色主物品槽位系统的轻量桥接入口。
    /// </summary>
    internal sealed class DuckovSlotAdapter : ISlotAdapter
    {
        /// <summary>
        /// 尝试把一个物品插入主角色物品的可用槽位。
        /// 该入口会反射调用角色物品上的 TryPlug，并按目标方法签名动态拼装参数。
        /// </summary>
        /// <param name="newItem">待插入角色槽位系统的物品实例。</param>
        /// <param name="preferredFirstIndex">优先尝试的起始槽位索引；仅在底层签名支持时传入。</param>
        /// <returns>底层插入成功时返回 true；无法解析主角色、角色物品或 TryPlug 失败时返回 false。</returns>
        public bool TryPlugToCharacter(object newItem, int preferredFirstIndex = 0)
        {
            try
            {
                var cmcT = FindType("CharacterMainControl") ?? FindType("TeamSoda.Duckov.Core.CharacterMainControl");
                var main = cmcT?.GetProperty("Main", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null, null);
                var charItem = main?.GetType().GetProperty("CharacterItem", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.GetValue(main, null);
                if (charItem == null) return false;
                var tryPlug = charItem.GetType().GetMethod("TryPlug", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (tryPlug != null)
                {
                    var ps = tryPlug.GetParameters();
                    if (ps.Length >= 1)
                    {
                        var args = new List<object>();
                        args.Add(newItem);
                        if (ps.Length >= 2) args.Add(true);
                        if (ps.Length >= 3) args.Add(null);
                        if (ps.Length >= 4) args.Add(preferredFirstIndex);
                        var r = tryPlug.Invoke(charItem, args.ToArray());
                        if (r is bool b) return b; return true;
                    }
                }
            }
            catch { }
            return false;
        }
    }
}