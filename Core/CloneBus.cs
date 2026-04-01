using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
    /// <summary>
    /// 克隆策略。
    /// 用于决定“新根物品”是通过 tree data 重建，还是通过 Unity 直接复制，或由管线自行选择回退路径。
    /// </summary>
    public enum CloneStrategy
    {
        /// <summary>自动：先尝试 TreeData，失败再回退 Unity。</summary>
        Auto,

        /// <summary>使用 TreeData 重建整棵子树，通常保真度更高，但依赖导出/重建链路可用。</summary>
        TreeData,

        /// <summary>使用 Unity 直接克隆运行时对象，通常更直接，但树结构和部分元数据保真度可能较弱。</summary>
        Unity
    }

    /// <summary>
    /// 变量合并策略。
    /// 用于控制克隆或 restore 时，源变量应如何写入到新对象。
    /// </summary>
    public enum VariableMergeMode
    {
        /// <summary>不合并变量。</summary>
        None,

        /// <summary>仅向目标添加缺失键；目标已有同名键时保持原值。</summary>
        OnlyMissing,

        /// <summary>覆盖目标已有键。</summary>
        Overwrite
    }

    /// <summary>
    /// 克隆管线选项：控制克隆策略、变量合并、放置目标与诊断等。
    /// 这是 clone 门面最主要的行为配置对象。
    /// </summary>
    public sealed class ClonePipelineOptions
    {
        /// <summary>克隆策略；Auto 会优先 TreeData，失败后再回退到 Unity。</summary>
        public CloneStrategy Strategy = CloneStrategy.Auto;

        /// <summary>变量合并模式。</summary>
        public VariableMergeMode VariableMerge = VariableMergeMode.OnlyMissing;

        /// <summary>是否复制标签。</summary>
        public bool CopyTags = true;

        /// <summary>放置目标键，例如 character、storage；为空时由实现自行选择解析或回退目标。</summary>
        public string Target = "character";

        /// <summary>放置后是否刷新 UI；纯 detached clone 场景通常不需要开启。</summary>
        public bool RefreshUI = true;

        /// <summary>是否收集诊断信息；开启后结果对象会附带 Diagnostics 或 RestoreDiagnostics。</summary>
        public bool Diagnostics = false;

        /// <summary>可选变量键过滤器；为 null 时表示全部接受。</summary>
        public Func<string, bool> AcceptVariableKey = null;
    }

    /// <summary>
    /// 克隆管线返回结果：新物品、是否已添加、所在索引、使用的策略与诊断数据。
    /// 适合让调用方同时消费“最终对象”和“附加过程”的上下文。
    /// </summary>
    public sealed class ClonePipelineResult
    {
        /// <summary>新克隆出的物品实例。</summary>
        public object NewItem { get; set; }

        /// <summary>是否已成功加入目标宿主；为 false 时可能是 detached clone，也可能是 attach 失败。</summary>
        public bool Added { get; set; }

        /// <summary>加入背包时的索引；未知或不适用时为 -1。</summary>
        public int Index { get; set; } = -1;

        /// <summary>实际使用的克隆策略标签。</summary>
        public string StrategyUsed { get; set; }

        /// <summary>共享 restore diagnostics；当 clone 走入 shared restore orchestrator 时可用。</summary>
        public RestoreDiagnostics RestoreDiagnostics { get; set; }

        /// <summary>可选诊断字典；通常用于直接消费轻量键值对的旧路径或调试路径。</summary>
        public Dictionary<string, object> Diagnostics { get; set; }
    }

    /// <summary>
    /// 克隆管线接口：根据选项克隆物品并尝试放入目标宿主。
    /// 它是 clone 门面的主合同，调用方应优先通过这里而不是手写多步克隆/放置流程。
    /// </summary>
    public interface IClonePipeline
    {
        /// <summary>
        /// 尝试将源物品克隆后放入目标宿主。
        /// 失败时返回 RichResult.Fail；成功时结果对象会说明是否真正附加、附加到哪里以及用了哪条策略。
        /// </summary>
        /// <param name="source">源物品。</param>
        /// <param name="options">管线选项。</param>
        /// <returns>结果，包含新物品与放置信息。</returns>
        RichResult<ClonePipelineResult> TryCloneToInventory(object source, ClonePipelineOptions options = null);
    }
}
