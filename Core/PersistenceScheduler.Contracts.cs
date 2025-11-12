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
        /// <summary>核心字段。</summary>
        Core = 1 << 0,
        /// <summary>变量。</summary>
        Variables = 1 << 1,
        /// <summary>常量。</summary>
        Constants = 1 << 2,
        /// <summary>标签。</summary>
        Tags = 1 << 3,
        /// <summary>修饰。</summary>
        Modifiers = 1 << 4,
        /// <summary>统计。</summary>
        Stats = 1 << 5,
        /// <summary>效果。</summary>
        Effects = 1 << 6,
        /// <summary>槽位。</summary>
        Slots = 1 << 7,
        /// <summary>全部。</summary>
        All = 0xFFFF
    }

    /// <summary>
    /// 持久化调度接口：管理入队、刷新与按帧节流。
    /// </summary>
    public interface IPersistenceScheduler
    {
        /// <summary>入队标记某物品为脏。</summary>
        void EnqueueDirty(object item, DirtyKind kind, bool immediate = false);
        /// <summary>强制刷新单个物品。</summary>
        void Flush(object item, bool force = false);
        /// <summary>立即刷新所有物品。</summary>
        void FlushAll(string reason = null);
        /// <summary>按节流策略处理刷新。</summary>
        void Tick(float? now = null);
        /// <summary>延迟窗口（最后一次变更后等待时间）。</summary>
        float DelaySeconds { get; set; }
        /// <summary>每帧最大刷新数。</summary>
        int MaxPerTick { get; set; }
        /// <summary>当前队列长度。</summary>
        int PendingCount { get; }
        /// <summary>累计处理总数。</summary>
        long ProcessedTotal { get; }
    }
}
