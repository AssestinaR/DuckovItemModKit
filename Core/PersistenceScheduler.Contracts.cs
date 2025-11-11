using System;

namespace ItemModKit.Core
{
    /// <summary>
    /// 脏数据类别（位标志，可组合）。
    /// </summary>
    [Flags]
    public enum DirtyKind
    {
        None = 0,
        Core = 1 << 0,
        Variables = 1 << 1,
        Constants = 1 << 2,
        Tags = 1 << 3,
        Modifiers = 1 << 4,
        Stats = 1 << 5,
        Effects = 1 << 6,
        Slots = 1 << 7,
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
