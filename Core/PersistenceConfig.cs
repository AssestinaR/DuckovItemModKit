namespace ItemModKit.Core
{
    /// <summary>
    /// 持久化配置：控制延迟、节流、编码与嵌入等策略。
    /// </summary>
    public sealed class PersistenceConfig
    {
        /// <summary>最后一次变更到写入的延迟秒数。</summary>
        public float DelaySeconds { get; set; } = 10f;
        /// <summary>自首次变更起的最大延迟秒数（超出强制写入）。</summary>
        public float MaxDelaySeconds { get; set; } = 30f;
        /// <summary>每帧最大写入条目。</summary>
        public int MaxPerTick { get; set; } = 8;
        /// <summary>是否对嵌入 JSON 使用 Base64 编码。</summary>
        public bool UseBase64Encoding { get; set; } = false; // prefer raw JSON for perf
        /// <summary>是否为嵌入 JSON 写入校验和。</summary>
        public bool EnableChecksum { get; set; } = false; // disable by default for perf
        /// <summary>单个物品嵌入 JSON 最大字节数。</summary>
        public int MaxBlobBytes { get; set; } = 64 * 1024; // guard upper bound per item
        /// <summary>是否冗余写入变量到引擎变量集合。</summary>
        public bool WriteRedundantVariables { get; set; } = false; // IMK_* split variables off by default
        /// <summary>写入后是否重新应用修饰器（较重）。</summary>
        public bool ReapplyAfterWrite { get; set; } = false; // heavy, keep off by default
        /// <summary>是否嵌入扩展块（变量/标签/修饰/槽位等）。</summary>
        public bool EmbedExtra { get; set; } = true; // allow disabling extra JSON embedding
        /// <summary>是否仅接受显式的 MarkDirty/Flush 调用。</summary>
        public bool ExplicitOnly { get; set; } = true; // default to explicit commit driven dirty marking
    }

    /// <summary>
    /// 持久化设置入口：获取当前配置或通过回调修改。
    /// </summary>
    public static class PersistenceSettings
    {
        /// <summary>当前配置实例。</summary>
        public static PersistenceConfig Current { get; } = new PersistenceConfig();
        /// <summary>通过回调修改当前配置。</summary>
        public static void Configure(System.Action<PersistenceConfig> configure)
        {
            if (configure == null) return;
            try { configure(Current); } catch { }
        }
    }
}
