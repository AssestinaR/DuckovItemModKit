using System;
namespace ItemModKit.Core
{
    /// <summary>
    /// 性能计数器：统计写入与调度的关键指标，用于窗口内观测与日志输出。
    /// </summary>
    public static class PerfCounters
    {
        public static long CoreWrites;
        public static long CoreWriteFailures;
        public static long SchedulerTicks;
        public static double SchedulerTickTotalMs;
        public static long SchedulerFlushes;
        public static double SchedulerFlushTotalMs;
        /// <summary>清零当前窗口内的计数与耗时。</summary>
        public static void ResetWindow()
        {
            CoreWrites = 0; CoreWriteFailures = 0; SchedulerTicks = 0; SchedulerTickTotalMs = 0;
            SchedulerFlushes = 0; SchedulerFlushTotalMs = 0;
        }
    }
}
