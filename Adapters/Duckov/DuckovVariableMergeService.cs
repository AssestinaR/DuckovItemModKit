using System;
using System.Collections.Generic;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 变量合并服务：从源拷贝变量到目标。
    /// 模式说明：
    /// - None：不执行合并
    /// - OnlyMissing：仅拷贝目标缺失的键
    /// - Overwrite：覆盖目标已有键
    /// 可通过 acceptKey 过滤参与合并的键。
    /// </summary>
    internal sealed class DuckovVariableMergeService : IVariableMergeService
    {
        /// <summary>
        /// 执行变量合并。
        /// </summary>
        /// <param name="source">源物品。</param>
        /// <param name="target">目标物品。</param>
        /// <param name="mode">合并模式。</param>
        /// <param name="acceptKey">可选键过滤（null 接受全部）。</param>
        public void Merge(object source, object target, VariableMergeMode mode, Func<string, bool> acceptKey = null)
        {
            if (source == null || target == null || mode == VariableMergeMode.None) return;
            var srcVars = IMKDuckov.Item.GetVariables(source) ?? Array.Empty<Core.VariableEntry>();
            var dstVars = IMKDuckov.Item.GetVariables(target) ?? Array.Empty<Core.VariableEntry>();
            var existing = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < dstVars.Length; i++) { var k = dstVars[i].Key; if (!string.IsNullOrEmpty(k)) existing.Add(k); }
            var batch = new List<KeyValuePair<string, object>>();
            foreach (var v in srcVars)
            {
                var k = v.Key; if (string.IsNullOrEmpty(k)) continue; if (v.Value == null) continue;
                if (acceptKey != null && !acceptKey(k)) continue;
                bool exists = existing.Contains(k);
                if (mode == VariableMergeMode.OnlyMissing && exists) continue;
                if (!exists || mode == VariableMergeMode.Overwrite) { batch.Add(new KeyValuePair<string, object>(k, v.Value)); existing.Add(k); }
            }
            if (batch.Count > 0) IMKDuckov.Write.TryWriteVariables(target, batch, overwrite: true);
        }
    }
}
