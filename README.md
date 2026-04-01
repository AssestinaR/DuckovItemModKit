# ItemModKit (IMK) 物品模组工具包

IMK 是《逃离鸭科夫》的运行时物品修改前置框架。它的目标不是承包整个玩法系统，而是把物品结构操作、事务、回滚、持久化和常见诊断收口成可复用接口，让后置 mod 直接基于 `IMKDuckov` 写业务逻辑。

- 运行时要求：`.NET Standard 2.1`
- 主要入口：`ItemModKit.Adapters.Duckov.IMKDuckov`
- 当前定位：物品结构与状态修改框架，不是通用 combat framework

## 先看什么

默认只看两份文档：

1. 当前页 [README.md](README.md)：面向下游 mod 作者，讲如何接入和调用 IMK。
2. [docs/imk-working-brief.md](docs/imk-working-brief.md)：面向维护者，讲 IMK 的方向、边界和当前不做什么。

其余文档当前都视为补充或过渡材料：

- [API_FREEZE.md](API_FREEZE.md)：稳定公开面清单，主要给维护者判断签名/语义能不能动。
- [Collaboration.md](Collaboration.md)：多模组协作约定。
- [TODO.md](TODO.md)：当前 backlog 和未收口议题。
- [docs/README.md](docs/README.md)：旧的内部索引页，当前只作为过渡入口保留。

## IMK 当前能做什么

IMK 目前已经形成比较稳定的能力主轴：

- 物品基础读写：核心字段、变量、常量、标签。
- 结构能力：slot、inventory、child inventory、tree restore。
- 状态能力：stats、modifier descriptions、effects。
- 运行时支撑：clone、rebirth、persistence、dirty/flush、recent reports。
- 事件与定位：item events、world drops、ownership、QueryV2、UISelectionV2。

当前 `buffs` 也已进入 IMK，但仍刻意停留在 draft facade：它适合 Probe、内部工具和受控下游 mod 使用，暂时不进入冻结的 `IReadService` / `IWriteService`。

## 不在 IMK 当前主线里的内容

这些方向现在不应被理解为 IMK 的主承诺：

- 通用 combat runtime bridge
- 大而全的行为模板系统
- 由 IMK 自己负责感知开火、跑动、停止开火等所有运行时战斗状态

如果后续确实需要承接这类需求，方向也应是“薄事件入口或桥接点”，而不是把 IMK 做成完整玩法框架。

## 服务地图

`IMKDuckov` 目前的主要服务分组如下：

- `Item` / `Inventory` / `Slot`：最低层适配器。
- `Read` / `Write`：稳定主读写服务。
- `Factory` / `Mover` / `Clone` / `Rebirth` / `Persistence`：结构与生命周期主链。
- `QueryV2` / `UISelectionV2` / `LogicalIds` / `Ownership`：定位、查询和协作入口。
- `ItemEvents` / `WorldDrops` / `UIRefresh`：运行时观察与刷新入口。

兼容入口如 `Query` 和 `UISelection` 仍然保留，但新代码应优先使用 V2 路径。

## 一分钟上手

常见接入路径：

- 获取当前 UI 选中物品：`IMKDuckov.TryGetCurrentSelectedHandle()` 或 `IMKDuckov.UISelectionV2.TryGetCurrent(out var handle)`
- 读取快照或结构：`Read.Snapshot(...)`、`Read.TryReadSlots(...)`、`Read.TryReadStats(...)`、`Read.TryReadEffects(...)`
- 执行写入：`Write.TryWriteCoreFields(...)`、`Write.TryWriteVariables(...)`、`Write.TryEnsureStat(...)`、`Write.TryAddEffect(...)`
- 做结构操作：`Write.TryAddSlot(...)`、`Write.TryPlugIntoSlot(...)`、`Write.TryMoveBetweenSlots(...)`
- 做生命周期操作：`Factory.TryCloneItem(...)`、`Mover.TrySendToPlayerInventory(...)`、`ReplaceRebirthDetailed(...)`、`RestoreFromMetaDetailed(...)`
- 读取运行时 buff draft：`EnumerateBuffCatalogDraft()`、`TryReadBuffsDraft(...)`、`TryFindBuffDraft(...)`

所有 `Try*` API 都返回 `RichResult`。调用侧应明确消费 `Ok`、`Code` 和 `Error`，不要把失败当异常路径之外的“隐式成功”。

## 下游调用建议

- 优先走服务层，不要在 mod 里重复反射 Duckov 内部对象。
- 多步写入优先包在事务里，失败时显式 rollback。
- 新代码优先使用 `QueryV2` / `UISelectionV2`，不要继续扩散兼容 facade。
- 需要稳定语义时优先依赖冻结面；draft 方法只用于当前确有需要的受控场景。
- Buffs 当前属于 draft runtime support，不等同于 IMK 已承诺完整 combat support。

## Draft 能力说明

IMK 目前保留了几类有意不冻结的 draft 入口：

- 槽位补建草案：`EnsureSlotsDraft(...)`
- 资源补建草案：`EnsureResourceProvisionDraft(...)`
- effect schema 枚举草案：`EnumerateEffectSchemaDraft(...)`
- buffs runtime 草案：`TryReadBuffsDraft(...)`、`TryAddBuffDraft(...)` 等

这些入口的设计原则是：先通过 Probe 和实机验证形成闭环，再决定是否进入稳定服务面。

## 调试与诊断

- `ItemModKit.Samples` 提供调试窗口和样例调用。
- recent rebirth reports 可以通过 `GetRecentRebirthReports(...)`、`LogRecentRebirthReports(...)` 读取。
- 性能采样和其他诊断工具位于 `Diagnostics/`。

如果你是在做下游 mod，对你最有价值的通常仍是当前页和门面/contract 上的代码注释，而不是旧设计文档。

## 协作与兼容

- 跨模写入时使用 `DuckovOwnership.Use("YourMod")` 标识归属。
- 对共享对象做竞争写入时使用 `TryLock(...)` / `Unlock(...)`。
- 变量命名避免碰撞：IMK 保留 `IMK_*`，下游 mod 使用自己的前缀。
- 接入前可用 `IMKDuckov.Require(...)` 做最低版本握手。

## 当前文档整理状态

仓库正在做文档减法。目标稳态是：

- 一份短方向文档：[docs/imk-working-brief.md](docs/imk-working-brief.md)
- 一份下游接口文档：当前页 [README.md](README.md)

其余文档会逐步合并、降级为过渡材料，或把有效内容迁回代码旁边的 XML 注释。

本轮已处理掉一批可直接回收的重复文档；接口速查、effects/buffs 分层边界这类内容，后续以当前页和对应实现文件注释为准。
