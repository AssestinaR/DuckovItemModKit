using System.Collections.Generic;

namespace ItemModKit.Core
{
    /// <summary>
    /// Tree export 恢复结果：承载恢复出的根物品、附加结果与导入模式。
    /// 该结果主要服务于 IMKDuckov.RestoreFromTreeExportDetailed(...) 这类门面入口。
    /// </summary>
    public sealed class TreeRestoreResult
    {
        /// <summary>恢复出的根物品；失败路径不会返回有效对象。</summary>
        public object RootItem { get; set; }

        /// <summary>是否已附加到目标宿主；为 false 时可能是分离恢复，也可能是附加失败。</summary>
        public bool Attached { get; set; }

        /// <summary>目标宿主是否已成功解析；可用于区分“目标未解析”与“解析成功但附加失败”。</summary>
        public bool TargetResolved { get; set; }

        /// <summary>附加索引；未知或不适用时为 -1。</summary>
        public int AttachedIndex { get; set; } = -1;

        /// <summary>实际使用的恢复策略标签。</summary>
        public string StrategyUsed { get; set; }

        /// <summary>当前导入模式，例如 tree 或 minimal。</summary>
        public string ImportMode { get; set; }

        /// <summary>可选共享诊断字典；通常包含 fallback、entriesImported、attachOutcome 等上下文。</summary>
        public Dictionary<string, object> Diagnostics { get; set; }
    }
}