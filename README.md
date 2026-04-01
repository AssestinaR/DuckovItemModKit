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

# ItemModKit (IMK) ?????鹤???

> Stage 1 API Freeze: See `docs/API_FREEZE.md` for the list of stable contracts. Additive changes only; no breaking signature changes.

?????????????????????????????????????????????????????????????С?????????????????????????

## Stage 1 快照与事务示例
```csharp
var item = IMKDuckov.UISelection.TryGetCurrentItem(out var it) ? it : null;
if (item != null)
{
    // Capture original core state
    var originalCore = SnapshotHelper.CaptureCore(IMKDuckov.Read, item);
    // Begin transaction
    var tok = IMKDuckov.Write.BeginTransaction(item);
    // Attempt core field change
    var changes = new CoreFieldChanges{ Name = originalCore.Name + "*", Value = originalCore.Value + 100 };
    var r = IMKDuckov.Write.TryWriteCoreFields(item, changes);
    if (!r.Ok)
    {
        IMKDuckov.Write.RollbackTransaction(item, tok);
        // Optionally rollback via snapshot helper (defensive duplicate):
        SnapshotHelper.RollbackCore(IMKDuckov.Write, item, originalCore);
    }
    else
    {
        IMKDuckov.Write.CommitTransaction(item, tok);
        IMKDuckov.MarkDirty(item, DirtyKind.CoreFields, immediate:false);
    }
}

```

## 精准(Accurate)物品位置与范围观察
IMK 为模组提供精准的物品位置与状态信息采集能力，通过 Harmony Patch 直接植入游戏逻辑 + 轮询 + 事件模式灵活组合，供模组调用与观察者订阅：

### 主要机制
1. **事件桥接 (Event Bridge)** `DuckovEventBridge` 直接订阅物品 原始事件 (`onParentChanged/onItemTreeChanged`) 并在物品移动至 Inventory/Slot 时自动标记为 "World" 状态，再触发 `WorldDrops.RegisterExternalWorldItem(item)`。
2. **掉落扫描 (WorldDrops Scan)** `DuckovWorldDropEventSource.Tick()` 周期性扫描物品信息，并推演可能的掉落/拾取状态至指定区域。
3. **分类器/识别 (Classifier)** `DuckovInventoryClassifier` 识别 Player / Storage / LootBox 等多种来源，并通过 QueryV2 + Ownership 精确匹配物品位置与归属，推演 SlotDepth/AncestorChain 等信息。

### 识别来源
- **Player**: 通过 `CharacterMainControl.Main.CharacterItem.Inventory` 获取
- **Storage**: 通过 `PlayerStorage.Inventory` 获取
- **LootBox**: 通过 `LevelManager.LootBoxInventories / LootBoxInventoriesParent` 获取
- **World**: 物品 ParentObject == null && !InInventory && !PluggedIntoSlot (事件驱动) 通过扫描确认

### 事件订阅
```csharp
IMKDuckov.WorldDrops.OnEnvironmentDrop += raw => {
    var handle = IMKDuckov.TryGetHandle(raw); // 可以通过 handle 获取更详细信息
    // 更新物品位置、状态等信息后可选推送通知/更新 UI
};
IMKDuckov.ItemEvents.OnItemChanged += raw => { /* 处理物品位置变化逻辑 */ };
```

### 位置查询 (Locator + QueryV2)
```csharp
// 通过handle 查询已知类型物品
action<IMKDuckov.QueryV2.ByTypeId(254).Equipped(true).Take(5)> 
var guns = IMKDuckov.QueryV2.ResetPredicates().ByTagAny("Gun","Rifle").Depth(0,3).All();
foreach (var h in guns) {
    Debug.Log($"Gun {h.DisplayName} tid={h.TypeId} invOwned={(h.TryGetRaw()?.GetType().GetProperty("InInventory")!=null)}");
}
```

### 互操作与嵌套作用域 (Scope APIs)
- 新增 `IItemScope` 以支持复杂的作用域判断与嵌套，默认支持 Slot/词缀前缺
- 作用链查询：Player → Storage → LootBox → World → Other
- 扩展：可选实现通用接口 `Includes(item, inventory, owner)` 以支持各种自定义作用域场景，例如 UI 选择等。

### 示例：物品掉落至世界状态确认
```csharp
void EnsureWorldState(object item) {
    if (item == null) return;
    var inv = item.GetType().GetProperty("InInventory", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)?.GetValue(item, null);
    var slot = item.GetType().GetProperty("PluggedIntoSlot", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)?.GetValue(item, null);
    if (inv == null && slot == null) {
        IMKDuckov.WorldDrops.RegisterExternalWorldItem(item); // 确保掉落至世界
    }
}
```

### 性能与注意事项
| 特性 | 描述 | 推荐性 | 限制 | 备注 |
|------|------|--------|-------|------|
| EventBridge | 是 | 高（实时） | 否 | 直接响应原始事件，适合敏感场合 |
| WorldDrops.Scan | 否 | 中（周期性） | 是 | 兜底方案，适合大部分场合 |
| QueryV2 | 是 | 高（索引查询） | 否 | 推荐优先使用，性能最优 |
| Classifier | 是 | 中（识别归属） | 否 | 自动识别物品归属，增强场景支持 |

### 已知问题与建议
- 无法通过 Harmony Patch 覆盖某些原生代码或被强制避开的场景
- 部分复杂场景可考虑结合使用多种机制以提高覆盖率
- 推荐定期观察更新状态或配合事件系统实现动态更新

更多细节与接口说明见 `README_FEATURES.md` 更新部分。
