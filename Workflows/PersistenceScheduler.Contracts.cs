using System;

namespace ItemModKit.Core
{
    /// <summary>
    /// 脏数据类别（位标志，可组合）。
    /// </summary>
    [Flags]
    public enum DirtyKind
    {
        /// <summary>无。</summary>
        None = 0,
        /// <summary>核心字段，例如名称、品质、价值等基础属性。</summary>
        Core = 1 << 0,
        /// <summary>变量集合。</summary>
        Variables = 1 << 1,
        /// <summary>常量集合。</summary>
        Constants = 1 << 2,
        /// <summary>标签集合。</summary>
        Tags = 1 << 3,
        /// <summary>修饰器与其派生状态。</summary>
        Modifiers = 1 << 4,
        /// <summary>统计值。</summary>
        Stats = 1 << 5,
        /// <summary>效果树与效果组件。</summary>
        Effects = 1 << 6,
        /// <summary>槽位结构与槽位内容物。</summary>
        Slots = 1 << 7,
        /// <summary>全部脏类别。</summary>
        All = 0xFFFF
    }

    /// <summary>
    /// 持久化调度接口：管理脏标记入队、刷新合并与按帧节流。
    /// 适合把频繁的小修改折叠成较少的持久化写入，避免每次字段变化都立即落盘。
    /// </summary>
    public interface IPersistenceScheduler
    {
        /// <summary>入队标记某物品为脏；immediate=true 时允许实现跳过延迟窗口尽快刷新。</summary>
        void EnqueueDirty(object item, DirtyKind kind, bool immediate = false);

        /// <summary>刷新单个物品；force=true 时允许实现忽略节流和去重策略。</summary>
        void Flush(object item, bool force = false);

        /// <summary>立即刷新当前队列中的全部物品；reason 仅用于日志或诊断标记。</summary>
        void FlushAll(string reason = null);

        /// <summary>按节流策略处理刷新；通常在 Update/Tick 中被周期性调用。</summary>
        void Tick(float? now = null);

        /// <summary>延迟窗口，表示最后一次脏变更后还要等待多久才允许自动刷新。</summary>
        float DelaySeconds { get; set; }

        /// <summary>每次 Tick 最多处理多少个待刷对象。</summary>
        int MaxPerTick { get; set; }

        /// <summary>当前待刷新队列长度。</summary>
        int PendingCount { get; }

        /// <summary>自调度器启动以来累计成功处理的对象数。</summary>
        long ProcessedTotal { get; }
    }
}
