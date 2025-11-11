namespace ItemModKit.Core
{
    public sealed class PersistenceConfig
    {
        public float DelaySeconds { get; set; } = 10f;
        public float MaxDelaySeconds { get; set; } = 30f;
        public int MaxPerTick { get; set; } = 8;
        public bool UseBase64Encoding { get; set; } = false; // prefer raw JSON for perf
        public bool EnableChecksum { get; set; } = false; // disable by default for perf
        public int MaxBlobBytes { get; set; } = 64 * 1024; // guard upper bound per item
        public bool WriteRedundantVariables { get; set; } = false; // IMK_* split variables off by default
        public bool ReapplyAfterWrite { get; set; } = false; // heavy, keep off by default
        public bool EmbedExtra { get; set; } = true; // allow disabling extra JSON embedding
        public bool ExplicitOnly { get; set; } = true; // default to explicit commit driven dirty marking
    }

    public static class PersistenceSettings
    {
        public static PersistenceConfig Current { get; } = new PersistenceConfig();
        public static void Configure(System.Action<PersistenceConfig> configure)
        {
            if (configure == null) return;
            try { configure(Current); } catch { }
        }
    }
}
