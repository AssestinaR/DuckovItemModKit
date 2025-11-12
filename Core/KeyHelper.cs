using System;

namespace ItemModKit.Core
{
    /// <summary>
    /// 键辅助工具：构造带所有者前缀的变量键，生成不重复的自增键。
    /// </summary>
    public static class KeyHelper
    {
        /// <summary>
        /// 构造带命名空间/所有者前缀的键，如 Owner_MyVar。
        /// </summary>
        /// <param name="ownerId">所有者标识。</param>
        /// <param name="key">原始键。</param>
        /// <returns>合成键。</returns>
        public static string BuildOwnedKey(string ownerId, string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            var owner = string.IsNullOrEmpty(ownerId) ? "Unknown" : ownerId.Trim();
            return owner + "_" + key.Trim();
        }

        /// <summary>
        /// 生成不重复的自增键：desired, desired1, desired2 ...。
        /// </summary>
        /// <param name="existingKeys">已有键集合。</param>
        /// <param name="desired">期望键名。</param>
        /// <returns>最终键名。</returns>
        public static string NextIncrementalKey(System.Collections.Generic.ISet<string> existingKeys, string desired)
        {
            if (string.IsNullOrEmpty(desired)) desired = "Key";
            if (existingKeys == null || !existingKeys.Contains(desired)) return desired;
            int n = 1;
            string baseName = desired;
            while (existingKeys.Contains(baseName + n)) n++;
            return baseName + n;
        }
    }
}
