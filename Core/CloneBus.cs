using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
    /// <summary>克隆策略。</summary>
    public enum CloneStrategy
    {
        /// <summary>自动：先尝试 TreeData，失败再回退 Unity。</summary>
        Auto,
        /// <summary>使用 TreeData 重建整棵子树（保真度高）。</summary>
        TreeData,
        /// <summary>使用 Unity 直接克隆 GameObject（快速）。</summary>
        Unity
    }
    /// <summary>变量合并策略。</summary>
    public enum VariableMergeMode
    {
        /// <summary>不合并变量。</summary>
        None,
        /// <summary>仅向目标添加缺失键。</summary>
        OnlyMissing,
        /// <summary>覆盖目标已有键。</summary>
        Overwrite
    }

    /// <summary>
    /// 克隆管线选项：控制克隆策略、变量合并、放置目标与诊断等。
    /// </summary>
    public sealed class ClonePipelineOptions
    {
        /// <summary>克隆策略（Auto 会优先 TreeData 失败再 Unity）。</summary>
        public CloneStrategy Strategy = CloneStrategy.Auto;
        /// <summary>变量合并模式。</summary>
        public VariableMergeMode VariableMerge = VariableMergeMode.OnlyMissing;
        /// <summary>是否复制标签。</summary>
        public bool CopyTags = true;
        /// <summary>放置目标：character/storage/null(auto)。</summary>
        public string Target = "character";
        /// <summary>放置后是否刷新 UI。</summary>
        public bool RefreshUI = true;
        /// <summary>是否收集诊断信息。</summary>
        public bool Diagnostics = false;
        /// <summary>可选：变量键过滤（null 表示全部接受）。</summary>
        public Func<string, bool> AcceptVariableKey = null;
    }

    /// <summary>
    /// 克隆管线返回结果：新物品、是否已添加、所在索引、使用的策略与诊断数据。
    /// </summary>
    public sealed class ClonePipelineResult
    {
        /// <summary>新克隆出的物品实例。</summary>
        public object NewItem { get; set; }
        /// <summary>是否已成功加入目标背包。</summary>
        public bool Added { get; set; }
        /// <summary>加入背包时的索引（1-based；未知为 -1）。</summary>
        public int Index { get; set; } = -1;
        /// <summary>实际使用的克隆策略。</summary>
        public string StrategyUsed { get; set; }
        /// <summary>可选诊断字典。</summary>
        public Dictionary<string, object> Diagnostics { get; set; }
    }

    /// <summary>
    /// 克隆管线接口：根据选项克隆物品并尝试放入背包。
    /// </summary>
    public interface IClonePipeline
    {
        /// <summary>
        /// 尝试将源物品克隆后放入背包。
        /// </summary>
        /// <param name="source">源物品。</param>
        /// <param name="options">管线选项。</param>
        /// <returns>结果，包含新物品与放置信息。</returns>
        RichResult<ClonePipelineResult> TryCloneToInventory(object source, ClonePipelineOptions options = null);
    }
}
