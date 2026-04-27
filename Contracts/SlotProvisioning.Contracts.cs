using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
    /// <summary>
    /// 补槽草案管线的执行阶段。
    /// 用于描述一次补槽请求当前走到了哪一步，也用于结果对象和诊断对象对外暴露阶段状态。
    /// </summary>
    public enum SlotProvisioningPhase
    {
        /// <summary>尚未进入任何阶段。</summary>
        None = 0,

        /// <summary>解析并校验目标物品。</summary>
        ResolveOwner = 1,

        /// <summary>确保目标物品具备可写槽位宿主。</summary>
        EnsureSlotHost = 2,

        /// <summary>合并已有槽位并生成新增槽位计划。</summary>
        MergeDefinitions = 3,

        /// <summary>写入持久化元数据。</summary>
        PersistMetadata = 4,

        /// <summary>刷新运行时缓存、UI 或派生状态。</summary>
        RefreshRuntime = 5,

        /// <summary>流程成功完成。</summary>
        Completed = 6,

        /// <summary>流程失败退出。</summary>
        Failed = 7,
    }

    /// <summary>
    /// 补槽时可选的模板来源。
    /// 调用方可以指定一个现有槽位作为模板，让新建槽位在过滤器、图标等细节上尽量贴近已有运行时结构。
    /// </summary>
    [Serializable]
    public sealed class SlotProvisionTemplateReference
    {
        /// <summary>
        /// 优先复用的模板槽位键。
        /// 当 <see cref="TemplateSlot"/> 为空时，会在宿主现有槽位集合中按该键解析模板槽位。
        /// </summary>
        public string TemplateSlotKey { get; set; }

        /// <summary>
        /// 显式给定的模板槽位对象。
        /// 不为 null 时优先于 <see cref="TemplateSlotKey"/> 使用，适合调用方已经拿到运行时槽位对象的场景。
        /// </summary>
        public object TemplateSlot { get; set; }

        /// <summary>
        /// 是否复制模板的过滤标签。
        /// 开启后，若新定义自身未显式提供 RequireTags / ExcludeTags，则优先继承模板槽位上的过滤条件。
        /// </summary>
        public bool CloneFilters { get; set; } = true;

        /// <summary>
        /// 是否复制模板图标。
        /// 开启后，若新定义自身未显式提供 SlotIcon，则会尝试复用模板槽位上的图标引用。
        /// </summary>
        public bool CloneIcon { get; set; } = true;

        /// <summary>
        /// 是否复制模板显示名。
        /// 当前实现主要保留该语义位，便于后续把模板显示名复制纳入统一补槽草案流程。
        /// </summary>
        public bool CloneDisplayName { get; set; }
    }

    /// <summary>
    /// 单个目标槽位的草案定义。
    /// 每个定义代表“最终希望宿主上存在一个什么样的槽位”，由补槽流程负责判断复用、创建或拒绝。
    /// </summary>
    [Serializable]
    public sealed class SlotProvisionDefinition
    {
        /// <summary>
        /// 槽位键；在宿主物品内应唯一。
        /// 补槽流程会使用该键判断是否已存在同名槽位，并把它作为持久化和后续同步的主键。
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// 槽位显示名。
        /// 该值主要影响 UI 展示；若为空，则由运行时槽位构造逻辑或模板回退决定最终显示文本。
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 槽位图标对象；允许为 null。
        /// 一般传入运行时可接受的 Sprite 或等价资源对象；若为 null，则保留实现默认值或模板回退值。
        /// </summary>
        public object SlotIcon { get; set; }

        /// <summary>
        /// 必需标签集合；为空时不做必需标签限制。
        /// 只有满足这些标签约束的物品才允许插入到对应槽位中。
        /// </summary>
        public string[] RequireTags { get; set; }

        /// <summary>
        /// 排除标签集合；命中任一标签时拒绝插入。
        /// 该集合常用于限制某类物品不能进入指定槽位。
        /// </summary>
        public string[] ExcludeTags { get; set; }

        /// <summary>
        /// 是否禁止插入相同 TypeID 的物品；为 null 时保持实现默认值。
        /// 该字段主要影响“相同类型物品是否允许重复挂载”这一槽位语义。
        /// </summary>
        public bool? ForbidItemsWithSameID { get; set; }

        /// <summary>
        /// 模板来源；用于从现有槽位克隆过滤器、图标或其他可复用配置。
        /// 为 null 时表示完全依赖当前定义本身构造新槽位。
        /// </summary>
        public SlotProvisionTemplateReference Template { get; set; }

        /// <summary>
        /// 如果目标键已存在，是否将其视为“已满足”而不是失败。
        /// 设为 true 时，该键会出现在结果对象的 ReusedSlotKeys 中；设为 false 时则会导致冲突失败。
        /// </summary>
        public bool ReuseExistingIfPresent { get; set; } = true;
    }

    /// <summary>
    /// “给原本无槽位或槽位定义不足的物品补槽”的内部草案请求。
    /// 该类型用于先固定实现语言与 Probe 验证输入，不代表稳定公开 API 已冻结。
    /// </summary>
    /// <remarks>
    /// 对后置作者来说，这个请求对象最关键的是三件事：
    /// 1. `OwnerItem` 指向谁；
    /// 2. `DesiredSlots` 想补哪些槽；
    /// 3. 成功后要不要顺手刷新 UI、发布事件和刷写持久化。
    /// </remarks>
    [Serializable]
    public sealed class EnsureSlotsRequest
    {
        /// <summary>
        /// 目标宿主物品。
        /// 补槽流程会在该对象上解析或创建 Slots 宿主，并把新槽位实际写入这个运行时物品实例。
        /// </summary>
        public object OwnerItem { get; set; }

        /// <summary>
        /// 期望最终存在的槽位集合。
        /// 补槽流程会逐项处理这些定义；成功新建的键、复用的键和拒绝的键都会反映到结果对象里。
        /// </summary>
        public SlotProvisionDefinition[] DesiredSlots { get; set; } = Array.Empty<SlotProvisionDefinition>();

        /// <summary>
        /// 当宿主当前没有槽位系统时，是否尝试创建槽位宿主。
        /// 关闭后，如果物品当前不存在可写 Slots 宿主，请求会直接失败。
        /// </summary>
        public bool CreateSlotHostIfMissing { get; set; } = true;

        /// <summary>
        /// 是否把动态槽位定义写入变量或其他持久化载体。
        /// 开启后，新创建的动态槽定义会同步写入草案 JSON，便于存档恢复后再次回放。
        /// </summary>
        public bool PersistDefinitionsToVariables { get; set; }

        /// <summary>
        /// 动态槽位定义使用的持久化键；为空时由实现决定。
        /// 常规情况下可留空，使用默认键；只有需要和别的实验性草案隔离时才建议自定义。
        /// </summary>
        public string PersistenceVariableKey { get; set; }

        /// <summary>
        /// 成功后是否刷新 UI。
        /// 关闭时更适合批处理或离线恢复路径；开启时更适合用户当前正打开界面的即时操作。
        /// </summary>
        public bool RefreshUI { get; set; } = true;

        /// <summary>
        /// 成功后是否发布运行时事件。
        /// 开启后，依赖 ItemChanged 或类似事件的后置逻辑能更快感知槽位结构变化。
        /// </summary>
        public bool PublishEvents { get; set; } = true;

        /// <summary>
        /// 成功后是否标记脏状态。
        /// 开启后，会把槽位和变量变更纳入 IMK 的持久化调度。
        /// </summary>
        public bool MarkDirty { get; set; } = true;

        /// <summary>
        /// 标记脏后是否立即强制 flush 持久化。
        /// 适合希望“补槽成功后立刻落盘”的场景；若更看重吞吐量，可关闭并交给调度器延后刷新。
        /// </summary>
        public bool ForceFlushPersistence { get; set; } = true;

        /// <summary>
        /// 调用方标签，用于 diagnostics 和 Probe 识别来源。
        /// 建议后置 mod 显式写入自己的标识，便于后续排查是谁发起了这次补槽请求。
        /// </summary>
        public string CallerTag { get; set; }

        /// <summary>
        /// 额外诊断元数据。
        /// 调用方可在这里附加自定义键值，供后续日志、Probe 或内部排错面板读取。
        /// </summary>
        public Dictionary<string, object> DiagnosticsMetadata { get; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// 补槽草案的共享诊断信息。
    /// 该对象用于描述这次补槽请求在运行时到底做了什么、耗时如何、以及是否动到了宿主结构或持久化元数据。
    /// </summary>
    [Serializable]
    public sealed class EnsureSlotsDiagnostics
    {
        /// <summary>
        /// 开始时间（UTC）。
        /// 用于和完成时间一起估算整次补槽请求的墙钟耗时。
        /// </summary>
        public DateTime StartedAtUtc { get; set; }

        /// <summary>
        /// 结束时间（UTC）。
        /// 若该值仍是默认值，通常表示流程在异常或早退路径上没有完整跑完。
        /// </summary>
        public DateTime CompletedAtUtc { get; set; }

        /// <summary>
        /// 各阶段耗时，单位为毫秒。
        /// 键是 <see cref="SlotProvisioningPhase"/>，值是该阶段的累计耗时。
        /// </summary>
        public Dictionary<SlotProvisioningPhase, long> PhaseTimings { get; } = new Dictionary<SlotProvisioningPhase, long>();

        /// <summary>
        /// 是否实际创建了槽位宿主。
        /// 为 true 说明目标物品原先没有 Slots 宿主，本次请求先补建了宿主再继续补槽。
        /// </summary>
        public bool SlotHostCreated { get; set; }

        /// <summary>
        /// 是否复用了已有槽位作为模板来源。
        /// 为 true 说明至少有一个目标槽位从现有模板槽继承了过滤器、图标或同类配置。
        /// </summary>
        public bool TemplateUsed { get; set; }

        /// <summary>
        /// 是否写入了持久化元数据。
        /// 为 false 且请求要求持久化时，通常意味着流程在 PersistMetadata 阶段失败或被回滚。
        /// </summary>
        public bool MetadataPersisted { get; set; }

        /// <summary>
        /// 附加元数据。
        /// 可用于记录异常文本、调用来源、补槽策略分支等非结构化信息。
        /// </summary>
        public Dictionary<string, object> Metadata { get; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// 补槽草案成功路径的内部结果。
    /// 该对象描述“这次请求最终创建了什么、复用了什么、刷新了什么”。
    /// </summary>
    /// <remarks>
    /// 如果调用方只想快速判断成功与否，看外层 `RichResult.Ok` 即可；
    /// 如果需要知道哪些键是新建、哪些键是复用、是否已经落盘，则看这个结果对象里的细分字段。
    /// </remarks>
    [Serializable]
    public sealed class EnsureSlotsResult
    {
        /// <summary>
        /// 结束时所在阶段。
        /// 成功返回通常为 <see cref="SlotProvisioningPhase.Completed"/>；若调用方自行构造结果对象，也可用来描述中间态。
        /// </summary>
        public SlotProvisioningPhase FinalPhase { get; set; }

        /// <summary>
        /// 最终宿主物品。
        /// 一般就是请求中的 OwnerItem，但保留该字段有利于后续在包装或替换路径上传递最终目标对象。
        /// </summary>
        public object OwnerItem { get; set; }

        /// <summary>
        /// 本次新建的槽位键集合。
        /// 只有真正调用运行时新增逻辑成功落地的键才会出现在这里。
        /// </summary>
        public string[] CreatedSlotKeys { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 已存在并被复用的槽位键集合。
        /// 这些键在请求前就已经存在，并且对应定义允许 `ReuseExistingIfPresent = true`。
        /// </summary>
        public string[] ReusedSlotKeys { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 被拒绝或跳过的槽位键集合。
        /// 通常出现在冲突、创建失败或请求中本身含有无效定义的路径上。
        /// </summary>
        public string[] RejectedSlotKeys { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 是否实际创建了槽位宿主。
        /// 与 diagnostics 中同名字段语义一致，这里是给成功结果做快速读取的便捷镜像。
        /// </summary>
        public bool SlotHostCreated { get; set; }

        /// <summary>
        /// 是否写入了持久化元数据。
        /// 为 true 时通常意味着新的动态槽定义已进入对应变量草案，可随存档再次回放。
        /// </summary>
        public bool MetadataPersisted { get; set; }

        /// <summary>
        /// 是否触发了运行时刷新。
        /// 该字段综合反映 UI 刷新、事件发布或脏标记等至少一种刷新动作是否发生。
        /// </summary>
        public bool RuntimeRefreshTriggered { get; set; }

        /// <summary>
        /// 是否标记了脏状态。
        /// 对希望确认“这次补槽会不会被持久化系统继续处理”的调用方来说，这个字段比看请求参数更可靠。
        /// </summary>
        public bool DirtyMarked { get; set; }

        /// <summary>
        /// 是否执行了持久化 flush。
        /// 为 true 说明成功路径上已经尝试把本次补槽引发的脏数据立即写出。
        /// </summary>
        public bool PersistenceFlushed { get; set; }

        /// <summary>
        /// 共享 diagnostics。
        /// 调用方需要看分阶段耗时、模板是否命中、异常元数据等细节时，应优先读取这里。
        /// </summary>
        public EnsureSlotsDiagnostics Diagnostics { get; set; }
    }
}