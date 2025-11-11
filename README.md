# ItemModKit (IMK) 物品模组工具包

轻量、代码优先的《逃离鸭科夫》物品模组开发工具包。目标：统一常见物品操作接口，最小化反射细节，让模组专注业务逻辑。

- 运行时要求：.NET Standard 2.1（游戏为 mono，非 IL2CPP）
- 组成：`ItemModKit.Core`（接口/DTO/结果/日志） + `ItemModKit.Adapters.Duckov`（Duckov 运行时适配） + `ItemModKit.Samples`（示例与调试窗口）

## 安装与引用
- 将 `ItemModKit` 作为前置框架 Mod 放入 Mods 目录（或将本仓编译出的 DLL 作为依赖）
- 你的模组工程目标框架设为 `netstandard2.1`
- 在代码中直接使用门面 `IMKDuckov` 获取服务（无需额外初始化）

## 服务地图（门面 `IMKDuckov`）
- `Item`：`IItemAdapter`（名称/品质/价值/变量/常量/修饰/槽位等的最低层访问）
- `Inventory`：`IInventoryAdapter`（是否在背包、容量、索引等）
- `Slot`：`ISlotAdapter`（宿主与槽位交互的低层工具）
- `Read`：`IReadService`（读取快照、修饰描述、效果列表、统计等）
- `Write`：`IWriteService`（写核心字段、变量/常量/标签、统计/修饰、效果、槽位、事务）
- `Mover`：`IItemMover`（发送到玩家/仓库、背包移动、拆分/合并、世界掉落、缓冲取回）
- `Factory`：`IItemFactory`（按 TypeId 生成、克隆、删除）
- `Rebirth`：`IRebirthService`（替换重生，支持保留位置 `keepLocation`）
- `Persistence`：`IItemPersistence`（内嵌元数据、IMK_* 变量、Owner 归属）
- `Query`：`IItemQuery`（常见来源检索）
- `UISelection`：`IUISelection`（当前 UI 选中物品/集合）
- 事件：`ItemEvents`（物品增删改移/合并/拆分）、`WorldDrops`（世界掉落）
- 版本/能力：`Version`、`Capabilities`、`Require(min, out err)`
- 日志/归属/锁：`UseLogger`、`GetOwnerId/IsOwnedBy`、`TryLock/Unlock`

## 一分钟上手（常用场景配方）
- 取当前 UI 选择：`IMKDuckov.UISelection.TryGetCurrentItem(out var item)`
- 写核心字段/变量/常量/标签：`Write.TryWriteCoreFields/Variables/Constants/Tags`
- 统计与修饰：`Write.TryEnsureStat/SetStatValue/AddModifier`，`Read.TryReadModifierDescriptions`
- 效果：`Write.TryAddEffect/SetEffectProperty/AddEffectComponent/EnableEffect/RemoveEffect`
- 槽位：`Read.TryReadSlots` + `Write.TryPlugIntoSlot/UnplugFromSlot/AddSlot/RemoveSlot/MoveBetweenSlots`
- 背包与世界：`Mover.TryAddToInventory/MoveInInventory/SendToPlayerInventory/SendToWarehouse/TryDropToWorldNearPlayer/Buffer` 系列
- 栈：`Mover.TrySplitStack/TryMergeStacks`
- 生成与删除：`Factory.TryGenerateByTypeId/TryCloneItem/TryDeleteItem`
- 替换重生：`Rebirth.Replace(oldItem, meta:null, keepLocation:true|false)`（支持包装对象）
- 事务：`var tok = Write.BeginTransaction(item); ... Write.CommitTransaction(item, tok)`（失败走 `RollbackTransaction`）
- 持久化：`Persistence.RecordMeta(item, meta, writeVariables:true)` / `TryExtractMeta`

所有 `Try*` 返回 `RichResult`：检查 `Ok/Code/Error`，避免异常传播。

## 事件（可选）
- 直接订阅：`IMKDuckov.ItemEvents.OnItemAdded/Removed/Changed/Moved/Merged/Split += ...`
- 外部事件模式：
  - 启用：`IMKDuckov.BeginExternalEvents()`（IMK 停止轮询）
  - 从你的补丁中发布：`IMKDuckov.PublishItemAdded(item, ctx)` 等
  - 停用：`IMKDuckov.EndExternalEvents()`

## 示例与调试
- `ItemModKit.Samples` 提供 `IMKDebugWindow`：
  - 一键测试、随机压力、全量新接口、随机综合、超级压力测试（可回放追踪、性能采样）
  - 可作为调用参考与自检脚本

## 错误码与健壮性
- 常见 `ErrorCode`：`InvalidArgument/NotFound/NotSupported/OperationFailed/DependencyMissing`
- 建议：所有写操作前验证输入，读取 `RichResult.Code` 决定降级或回退（例如 Drop 失败改为 SendToWarehouse）

## 多模组协作建议
- 归属：默认自动推断；跨模时用 `DuckovOwnership.Use("YourMod")` 包裹写入
- 锁：对竞争对象写入使用 `TryLock/Unlock`
- 变量命名：IMK 保留 `IMK_*`，你的模组使用 `<ModName>_*` 前缀
- 版本握手：`IMKDuckov.Require(new Version(0,1,0), out var err)`

## 最佳实践
- 所有 IMK API 在主线程调用
- 优先用服务层 `Read/Write/Mover/Factory`，避免自行反射
- 使用事务包裹多步写入，失败回滚
- 记录 `RichResult` 并输出上下文，便于问题定位

## 疑难排查
- 对象无 `Stats/Slots/Effects`：按 `NotSupported` 处理即可
- 放置/掉落失败：检查物品是否允许或补全回退策略
- Rebirth 失败：确认输入是否为有效 `Item` 或可被解包的包装对象

更多特性与接口清单见 `README_FEATURES.md`。

## 迁移说明（Clone 总线化管线）
- 已提供总线化克隆管线：`IMKDuckov.Clone.TryCloneToInventory(item, options)`，包含策略选择（TreeData→Unity 回退）、变量合并、标签复制、放置与可选 UI 刷新。
- 旧的 TreeData 直接克隆示例与端点已迁移到新管线：
  - `/item/cloneTreeToBag`、`/item/cloneTreeAdvToBag` 等现已转为内部调用 `IMKDuckov.Clone`（保持兼容）。
  - 推荐统一使用新端点 `/item/clonePipeline`，可通过 query 参数控制 `strategy/merge/filter/target`。
- API 兼容性：`DuckovTreeDataService.TryCloneIntoInventoryAdvanced*` 已标记 `[Obsolete]` 并内部转发到管线，未来版本将移除。
- 第三方模组可通过 `ClonePipelineOptions.AcceptVariableKey` 传入变量过滤器。
