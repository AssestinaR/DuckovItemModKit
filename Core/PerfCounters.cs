using System;
namespace ItemModKit.Core
{
    /// <summary>
    /// 性能计数器：统计写入与调度关键指标（写次数/失败次数/调度 Tick/Flush 数与耗时）。
    /// </summary>
    public static class PerfCounters
    {
        /// <summary>核心写入次数。</summary>
        public static long CoreWrites;
        /// <summary>核心写入失败次数。</summary>
        public static long CoreWriteFailures;
        /// <summary>调度器 Tick 次数。</summary>
        public static long SchedulerTicks;
        /// <summary>调度器 Tick 累计耗时（毫秒）。</summary>
        public static double SchedulerTickTotalMs;
        /// <summary>调度器 Flush 次数。</summary>
        public static long SchedulerFlushes;
        /// <summary>调度器 Flush 累计耗时（毫秒）。</summary>
        public static double SchedulerFlushTotalMs;
        /// <summary>清零当前窗口内的计数与耗时。</summary>
        public static void ResetWindow()
        {
            CoreWrites = 0; CoreWriteFailures = 0; SchedulerTicks = 0; SchedulerTickTotalMs = 0;
            SchedulerFlushes = 0; SchedulerFlushTotalMs = 0;
        }
    }
}
