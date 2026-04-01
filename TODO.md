# IMK TODO（面向总线的重构计划）

愿景
- 将核心重构为面向总线的架构：由单一职责的小服务组成，并通过门面统一对外暴露。
- 将 TreeData clone 提升为一等策略，但职责仅聚焦于结构化克隆/序列化本身。

近期待讨论的基础需求（先文档、后实现）
- 先在 Probe 验证计划与 IMK 本体规划文档中收敛边界、事务语义、回滚/恢复预期，再决定是否进入代码层。
- 支持为原本无槽位的物品补建槽位结构，并定义其初始化、挂载、回滚与持久化边界。
- 支持为原本一次性/无耐久语义的物品补建 durability 或 use-count 一类消耗状态，并明确与 vanilla 消耗链的兼容方式。
- 支持把原本不可堆叠的物品扩展为可堆叠物品，并明确 stack merge/split、容量约束、UI 表现与事务恢复语义。
- 评估为非武器或非 use-like 物品补建可装备/可使用行为的可行性，但该类“行为嫁接/范式转换”暂只保留在文档层讨论，不提前冻结 API。
- 评估“让物品从一种 archetype 迁移为另一种 archetype”的长期可能性，但在模块归属、运行时桥接、持久化恢复边界未稳定前，不进入实现排期。

面向后置 mod 的支持边界（2026-04-02）
- IMK 应优先直接承接“结构扩展类”能力：补槽、补 durability/use-count、扩展 stackability。这些能力应进入主线规划，因为它们本质上仍属于 structure / mutation / persistence 的连续延伸。
- IMK 应为“行为嫁接类”能力提供底层支撑，而不是一键模板：例如让非食物可食用、让非武器具备武器化行为。框架更适合提供组件补建、effect 注入、runtime bind、状态与 diagnostics，而不宜现在承诺通用成品能力。
- IMK 应逐步提供“runtime 资源与损坏状态”框架：支持多资源消耗、损坏而非直接删除、恢复/修复钩子、resource source 抽象与事务扣减。这类能力更适合进入第五支柱与运行时层，而不是塞进单次写接口。
- 动态合成表、拆解材料、工作台修复等系统外扩能力，当前只建议保留为长期候选方向。IMK 可以预留 descriptor / registry / hook 面，但不宜在现阶段把它们纳入核心承诺。

建议的落地顺序（面向 mod-ready baseline）
- 第一批：补槽、补 durability/use-count、扩展 stackability。目标是先把结构层和基础消耗层打透。
- 第二批：行为嫁接的最小能力，例如可食用或可 use-like 的最小闭环；并开始抽象 resource source 与 damage-state。
- 第三批：多资源联合消耗、损坏状态、repair flow、复杂 runtime trigger；再之后才讨论更激进的武器化与 crafting/deconstruction 扩展。

当前进度判断（2026-04-04）
- Slot Foundation 的核心结构能力已经前进到“基本完工，可暂时退出主带宽”的阶段。
- 已落地并通过构建验证的能力包括：`TryEnsureSlotHost`、`TryEnsureSlots`、`TryAddSlot`、`TryRemoveSlot`、`TryRemoveDynamicSlot`、`TryRemoveBuiltinSlot`、`TryRemoveSlots`、`TryRemoveSlotSystem`、`TryPlugIntoSlot`、`TryUnplugFromSlot`、`TryMoveBetweenSlots`、`TrySetSlotTags`，以及对应的持久化回放与保存生命周期闭环。
- 当前对 slot 的判断应记录为“基本完工”：核心结构、事务、持久化、save/load 与下游集成已形成可工作的基线；剩余工作主要是 Probe 断言修正、补充回归清单和后续按需维护。
- `LocalizedTexts` 已开始独立落地为共享读层：当前已补出独立文件夹与原版本地化桥接，可按 localization key / stat key 读取当前语言或全部语言文本；后续 stats、slots、variables 等阶段应复用这一层，而不是各自重复散落本地化解析逻辑。
- stats 当前已开始按正式分层收口：读取侧现在区分 `BaseValue` / `EffectiveValue` / `LocalizedNameCurrent`，写入侧 `TrySetStatValue` 语义已明确为“写基础值”，并补入 `TryMoveStat` 作为最小顺序调整原语。
- stats 宿主现已按 slot 同样视为可选能力域：IMK 已补出 `TryEnsureStatsHost`、`TryRemoveStatsHost` 和 `TryEnumerateAvailableStats`，其中可用 stat key 目录直接复用原版 `StringLists.StatKeys`，便于后续 UI/Probe 为用户提供候选字段选择。
- stats 宿主的 save/load 闭环现已补齐：embedded extra 侧的 `stats` 片段会同时记录宿主存在性与基础值条目，load 时可按持久化状态重建缺失宿主、回放条目，或显式恢复“宿主已关闭”的状态。
- 下一块建议切到 stats，尤其是武器相关属性：先把 stat key 目录、可稳定读写的武器统计项、Probe 样例和文档映射整理出来，再决定是否进入更深的 effect / modifier / combat consume 联动。

第一批 support matrix（2026-04-02）
- 无槽位物品补槽：IMK 负责结构创建、slot attach/detach、事务回滚、save/load 恢复、diagnostics；后置 mod 负责决定加什么槽、槽规则和玩法含义；当前结构原语已扩展到 `TryEnsureSlotHost/TryEnsureSlots/TryAddSlot/TryRemoveSlot/TryRemoveDynamicSlot/TryRemoveBuiltinSlot/TryRemoveSlots/TryRemoveSlotSystem/TryPlugIntoSlot/TryUnplugFromSlot`，其中 save/load 生命周期、删除持久化和 host 初始化都已落地。当前状态可记为“基本完工”，剩余缺口主要是 Probe 断言收紧、诊断沉淀和后续维护性回归。
- 一次性物品补 durability/use-count：IMK 负责 durability/use-count 的状态模型、写入接口、耗尽语义、回滚、持久化与 diagnostics；后置 mod 负责定义每次使用扣多少、耗尽后是删除还是损坏；当前已有 stack/durability 读写原语，但仍缺“从无耐久语义物品初始化”为稳定能力的闭环定义。
- 不可堆叠物品扩展为可堆叠：IMK 负责 stackability 初始化、split/merge 事务、inventory 兼容、save/load 与 diagnostics；后置 mod 负责决定哪些物品可堆叠、最大堆叠量和玩法限制；当前已有 `TrySetStackCount/TrySetMaxStack/TrySplitStack/TryMergeStacks` 原语，但仍缺“原本不可堆叠 -> 可堆叠”的初始化与 Probe 闭环。

第一批落地前置条件
- 先为每项能力写清楚 request/result、失败码、回滚边界、cleanup 边界、save/load 预期。
- 先做最小 Probe 样例，不直接追求最终玩法 UI 或复杂运行时行为。
- 每项能力都先形成“静态通过 + Probe 通过 + 游戏内复测通过”的三段闭环，再考虑进入稳定公开面。

无槽位物品补槽的实现前准备（2026-04-02）
- request 草案：`EnsureSlotsRequest`
   - 最小字段建议：`TargetItem`、`Definitions`、`MergeIfExists`、`RefreshUI`、`Persist`、`DiagnosticsLevel`。
- result 草案：`EnsureSlotsResult`
   - 最小字段建议：`CreatedCount`、`ReusedCount`、`CreatedKeys`、`SkippedKeys`、`SaveRestoreReady`、`Diagnostics`、`RollbackOutcome`。
- 失败码建议优先复用现有 `RichResult.Code`：`InvalidArgument`、`NotSupported`、`OperationFailed`、`DependencyMissing`。
- 代码草案已落地：`Core/SlotProvisioning.Contracts.cs` 已定义 request/result/diagnostics，`Adapters/Duckov/DuckovSlotProvisioningDraft.cs` 已接入第一版执行器。
- 当前第一版执行器范围：`CreateSlotsComponent()` 初始化缺失 slot host、按 request 复用/新增槽位、把草案定义写入变量 JSON、触发基础 dirty/flush/UI refresh。
- facade 入口已落地：当前可通过 `IMKDuckov.EnsureSlotsDraft(request)` 直接调用补槽草案执行器，而不必绕过门面直接触碰适配器类。
- save/load 回放已接上：`DuckovPersistenceAdapter.EnsureApplied(...)` 现在会识别 `IMK_Meta.DynamicSlotsDraft` 并重放动态槽位定义；加载路径下默认不再触发额外 dirty/flush。
- 当前 slot 写服务稳定入口已补齐：`IMKDuckov.Write` 现在已提供 `TryEnsureSlotHost`、`TryEnsureSlots`、`TryRemoveSlotHost`、`TryRemoveDynamicSlot`、`TryRemoveBuiltinSlot`、`TryRemoveSlots`、`TryRemoveSlotSystem` 和 `TrySetSlotTags` 等稳定入口，后置 mod 不必再自行拼装这些底层流程。
- 当前 Probe 状态：slot 结构与持久化闭环本身已在代码与下游构建中打通，但 Probe/diagnostics 层还没有完全更新到“真实执行 + rollback/cleanup/save-load 断言”的最终形态，仍是当前最值得补齐的收口项。
- 当前架构语义补充：补槽事务的 cleanup 只负责恢复槽结构和补槽草案变量，不承诺把物品“从 IMK 管理中释放”。如果后续需要显式释放 IMK 痕迹，应走独立 intent，而不是继续扩展 write transaction cleanup。
- 当前最合适的“释放”锚点是 rebirth 替换链：`IMKDuckov.ReplaceRebirthDetailed(...)` / `IMKDuckov.ReplaceRebirthReport(...)`。它们天然具备“旧实例销毁 + 新实例替换”的语义，比 write cleanup 更适合承载未来的“干净重生/从 IMK 释放”能力。
- 但要注意现状：当前 `DuckovRebirthService` 仍会在 `HydrateReplacementRoot(...)` 中调用 `RecordMeta(...)`，并复制 `IMK_` / `Custom` 前缀变量，因此现有 rebirth 入口还不能等同于“已实现的释放路径”；它只是后续拆分“安全替换”和“干净重生”时应复用的代码锚点。
- Probe 最小样例：对一个原本无槽位物品执行“初始化 slots -> 创建新槽 -> attach 子物品 -> rollback/commit/cleanup -> save/load 验证”。
- 游戏内复测最小步骤：用户进入游戏选定样本物品，执行补槽、观察 UI、尝试插入/拔出、切场景或重载存档，再确认槽位与内容仍然一致。

协作推进流程（Copilot 主导 + 用户游戏验证）
- 第 1 步：先写 support matrix。对每一类新能力明确“IMK 负责什么、后置 mod 自己负责什么、当前缺什么原语”。
- 第 2 步：先定契约，再写实现。每个能力先定义输入、失败语义、回滚边界、持久化语义、runtime 生命周期、diagnostics 字段和 Probe 断言项。
- 第 3 步：先做 Probe 验证样例，不直接追求最终玩法。每个能力都先收成可回放、可日志化、可对照的最小场景。
- 第 4 步：实现时按依赖顺序推进，不跨层乱跳。先结构，再基础消耗，再行为嫁接，再复杂 runtime 资源模型。
- 第 5 步：由 Copilot 负责代码、文档、Probe、静态校验、日志分析；由用户负责进入游戏执行样例、观察 UI/动画/数值/树状态，并回传日志与现象。
- 第 6 步：每个能力都必须经过“实现 -> 进游戏验证 -> 修复 -> 复测 -> 文档更新”的闭环，不能只靠静态代码通过。
- 第 7 步：只有当某项能力在 Probe 和真实运行时都通过后，才考虑是否进入 `API_FREEZE.md` 的稳定公开面。

mod-ready baseline 的完成定义
- 结构层稳定：补槽、拆槽、slot attach/detach、持久化恢复与事务回滚稳定。
- 消耗层稳定：stack、durability、use-count 的读写与耗尽语义稳定。
- 行为层最小可用：effects、基础 use-like 行为嫁接、有限 runtime trigger 可以工作。
- persistence 稳定：save/load 后结构与关键状态不丢失。
- diagnostics 可用：日志、recent reports、Probe 报告足以定位问题。
- 至少 2 到 3 类真实后置需求已经跑通，IMK 可作为后置 mod 的稳定起点，而不再阻塞后置开发。

当前迭代待办（Sprint backlog）
1) 服务与契约
   - 定义 `ITreeDataCloneService`（Clone/Export/Import，不负责放置与刷新）
   - 定义 `IInventoryPlacementService`（Add/IndexOf/Verify/Retry 策略）
   - 定义 `IVariableMergeService`（none|onlyMissing|overwrite）
   - 定义 `IUIRefreshService`（NeedInspection + Refresh）
   - 定义 `ICloneDiagnostics`（耗时、计数、strategyUsed、retries、degraded）
   - 预留第五支柱运行时接口：事件总线、状态存储、trigger、action、binder、runtime diagnostics
2) 门面与选项
   - 添加 `IMKDuckov.Clone.CloneTreeAsync(source, CloneOptions)`
   - `CloneOptions`：strategy(TreeData|Unity|Auto)、variableMerge、copyTags、target(character|storage|explicit)、retryPolicy、diagnostics
   - `CloneResult`：newItem、placement info、diagnostics、strategyUsed、degraded
3) TreeData 模块瘦身
   - 保留：`FromItem/InstantiateAsync` 包装 + 精确的 CustomData 序列化
   - 移除/下沉：inventory resolve、UI refresh、diagnostics、delay logic
   - 集中管理 TreeData/CustomData 的反射与 delegate 缓存
4) 放置流程统一
   - clone 后校验：AddAndMerge → IndexOf/InInventory → retry（下一帧）→ 可选 target 切换
   - 触发 `PublishItemAdded`，并通过 `IUIRefreshService` 调用 UI 刷新
5) Rebirth/Mover/Write 拆分
   - Rebirth：只负责生成新树与映射 meta；放置/UI 刷新/持久化全部委托出去
   - Mover：拆分“纯 inventory 移动”和“发送到玩家/仓库”操作
   - WriteService：拆分为 CoreFields/Variables/Modifiers/Slots，Transactions 保持独立
6) Await 与生命周期
   - 为 UniTask 提供统一 awaiter（`IAwaiter` 或 helper），避免 busy-wait
   - 对齐阶段：Capture → Instantiate → VariableMerge/Appliers → Placement → Refresh
7) Inventory resolver
   - `IInventoryResolver`：character/storage/current-UI/explicit handle，避免临时拼凑式反射

诊断与安全性
- 为 clone/placement 采样增加可选诊断等级（off|warn|detail）
- 增加结构化日志：entries、varCount、timings、target、result index、retries
- 在 clone 期间抑制事件风暴，结束后再合并发布事件

性能
- 预热反射缓存；热点路径优先使用 CreateDelegate
- 为诊断采样定义大小/耗时阈值

兼容性与回退
- TreeData 失败时 → 回退到 UnityClone + 差异变量合并，并在结果中标记 degraded
- 旧 API 保留一版过渡期 shim

文档
- 编写作者指南：总线架构、职责划分、策略选择、持久化预期
- 恢复架构草案：`docs/imk-restore-architecture.md`（基于 vanilla 恢复链证据的阶段化设计草案）
- 恢复内部契约草案：`docs/imk-restore-contracts.md`（定义 RestorePhase、RestoreResult 与各阶段结果对象）
- Clone / Rebirth / Restore 统一草案：`docs/imk-clone-rebirth-unification.md`（定义三条入口如何共享同一条 orchestrator 管线）
- 服务拆分草案：`docs/imk-service-decomposition.md`（定义 Core / Adapters / Samples 的职责边界与迁移方向）
- 第五支柱与接口预铺草案：`docs/imk-runtime-effects-and-interface-prep.md`（定义 Item Runtime / Runtime Effects 的目标、边界与准备阶段接口面）
- 五大支柱 readiness 评估：`docs/imk-foundation-readiness.md`（总结五大支柱完备度、v1 开工边界与仍待抓取的重点接口）
- Samples：高级实验功能放在 debug flag 后面
- 文档分层约束：根目录只保留高频入口；内部架构、治理、研究、vanilla 证据优先收口到 `docs/README.md`
- 文档整理候选：补 Samples / diagnostics 入口，并持续把新增内部文档先挂到 `docs/README.md`，避免继续平铺新增 md

近期已完成
- Slot Foundation 的结构实现已进一步收口：slot 相关写服务已迁入 `Adapters/Duckov/Slots/`，并按 runtime / workflow / persistence / removal 分层。
- 已补齐 `TryRemoveSlots` 与 `TryRemoveSlotSystem` 两个稳定删除工作流入口，并把槽位删除持久化与运行时删除语义对齐。
- 已修复 slot 持久化在切场景 / 重启后的回放闭环，包含动态槽位、原版槽位 tombstone 与 save/load 生命周期桥接。
- 已修复 scene switch 时由 slot 保存桥接引入的性能抖动，当前保存路径只对 IMK 管理物品做必要同步。
- Slot 目录内关键文件已补齐一轮中文 XML 注释，后续阅读成本已显著下降。
- 在 Samples 中加入带详细诊断的高级 TreeData clone
- 修复 `/item/cloneTreeAdvToBag` 的路由匹配（不再被 StartsWith 阴影覆盖）
- 新增 UI lab 页面与 inventory diagnostics，并支持 target=character/storage 选择
- 修复网页 `app.js` 缺失的辅助函数（`nodeKey`、open-set persistence）

待办积压（保留自旧版 TODO）
- 应用 Effects 时去重（若同类型已存在则跳过）
- Transaction API：`Commit(flushImmediate)`
- backlog 很大时，让 Scheduler 自适应调整 MaxPerTick
- 增加 flush JSON 大小与耗时日志开关
- SuperStress：通过 UI 字段暴露配置（waves、mods、seed）
- 为未来 stat schema 增加 FormatVersion 升级路径
- 在 `PersistenceScheduler` 上增加 `Reset()`，用于 profile 切换
- 增量 stat delta 捕获（延后）
- MetaEnricher 示例（裁剪大型数组）
- 单元测试支架（适合编辑器环境）

已移除（按产品方向不再适用）
- 按大小阈值启用的可选压缩（LZ4）（用户已放弃）
- 校验型功能（生产环境默认不启用，仅保留为诊断用途）
