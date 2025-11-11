using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
    public enum CloneStrategy { Auto, TreeData, Unity }
    public enum VariableMergeMode { None, OnlyMissing, Overwrite }

    public sealed class ClonePipelineOptions
    {
        public CloneStrategy Strategy = CloneStrategy.Auto;
        public VariableMergeMode VariableMerge = VariableMergeMode.OnlyMissing;
        public bool CopyTags = true;
        // target: "character" | "storage" | null(auto)
        public string Target = "character";
        public bool RefreshUI = true;
        public bool Diagnostics = false;
        // Optional: external filter decides which variable keys are accepted for merge (null = accept all)
        public Func<string, bool> AcceptVariableKey = null;
    }

    public sealed class ClonePipelineResult
    {
        public object NewItem { get; set; }
        public bool Added { get; set; }
        public int Index { get; set; } = -1;
        public string StrategyUsed { get; set; }
        public Dictionary<string, object> Diagnostics { get; set; }
    }

    public interface IClonePipeline
    {
        RichResult<ClonePipelineResult> TryCloneToInventory(object source, ClonePipelineOptions options = null);
    }
}
