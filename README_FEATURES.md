# ItemModKit features / 功能概览与接口速查

本页面向模组作者，列出 IMK 能力与常用接口，配合 README 的“上手配方”，可快速落地。

- 运行时：`netstandard2.1`
- 门面：`ItemModKit.Adapters.Duckov.IMKDuckov`（下文用 `IMKDuckov` 代指）

## 1. 选择 / 查询 / 工厂
- 选择：`UISelection.TryGetCurrentItem(out object item)`，`TryGetCurrentSlots(out object slotsOwner)`
- 查询：`Query.TryGetFromBackpack(index, out object item)`，`Query.TryFindByTypeId(typeId, out var list)`
- 生成/克隆/删除：
```csharp
var r1 = IMKDuckov.Factory.TryGenerateByTypeId(123);
var r2 = IMKDuckov.Factory.TryCloneItem(item);
var r3 = IMKDuckov.Factory.TryDeleteItem(item);
```

## 2. 读取服务（Read）
- 快照：`ItemSnapshot.Capture(IMKDuckov.Item, item)`
- 修饰描述：`Read.TryReadModifierDescriptions(item)`
- 效果列表：`Read.TryReadEffects(item)`
- 统计：`Read.TryReadStats(item)`
- 槽位：`Read.TryReadSlots(item)`

## 3. 写入服务（Write）
- 核心字段：
```csharp
Write.TryWriteCoreFields(item, new CoreFieldChanges {
  RawName = "MyName", Value = 100, Quality = 2, DisplayQuality = 2
});
```
- 变量 / 常量 / 标签：
```csharp
Write.TryWriteVariables(item, new[]{ new KeyValuePair<string,object>("My_VAR", 1) }, overwrite:true);
Write.TryWriteConstants(item, new[]{ new KeyValuePair<string,object>("My_CONST", "v") }, createIfMissing:true);
Write.TryWriteTags(item, new[]{ "TagA", "TagB" }, merge:true);
```
- 统计 / 修饰：
```csharp
Write.TryEnsureStat(item, "Damage", 1f);
Write.TrySetStatValue(item, "Damage", 2.5f);
Write.TryAddModifier(item, "Damage", 0.1f, false, "Add", source:this);
Write.TryReapplyModifiers(item);
```
- 效果：
```csharp
Write.TryAddEffect(item, "ItemStatsSystem.Effect");
Write.TrySetEffectProperty(item, idx, "description", "desc");
Write.TryAddEffectComponent(item, idx, "ItemStatsSystem.TickTrigger", "Trigger");
Write.TryEnableEffect(item, idx, true);
Write.TryRemoveEffect(item, idx);
```
- 槽位：
```csharp
var slots = Read.TryReadSlots(owner);
Write.TryPlugIntoSlot(owner, slotKey, child);
Write.TryUnplugFromSlot(owner, slotKey);
Write.TryMoveBetweenSlots(owner, fromKey, toKey);
Write.TryAddSlot(owner, new SlotCreateOptions{ Key="Socket", DisplayName="插槽" });
Write.TryRemoveSlot(owner, slotKey);
```
- 事务：
```csharp
var token = Write.BeginTransaction(item);
var ok1 = Write.TryWriteTags(item, new[]{"Txn"}, true).Ok;
var ok2 = Write.TryWriteVariables(item, new[]{ KVP("TXN", 1) }, true).Ok;
if (ok1 && ok2) Write.CommitTransaction(item, token); else Write.RollbackTransaction(item, token);
```

## 4. 迁移 / 持久化 / 重生
- 内嵌元数据：`Persistence.RecordMeta(item, meta, writeVariables:true)` / `TryExtractMeta`
- 元数据对象：`ItemMeta`（NameKey/TypeId/Quality/DisplayQuality/Value/OwnerId/EmbeddedJson）
- 替换重生：
```csharp
var rep = Rebirth.Replace(oldItem, meta:null, keepLocation:true);
if (rep.Ok) var newItem = rep.Value;
```
- 归属：`GetOwnerId(item)` / `IsOwnedBy(item, ownerId)` / `DuckovOwnership.Use(ownerId)`

## 5. 物品移动 / 背包 / 掉落
```csharp
Mover.TrySendToPlayerInventory(item, merge:false);
Mover.TrySendToWarehouse(item, queue:true);
Mover.TryAddToInventory(item, inventory, hint:null, allowMerge:true);
Mover.TryMoveInInventory(inventory, fromIndex, toIndex);
Mover.TrySplitStack(item, count);
Mover.TryMergeStacks(dst, src);
Mover.TryDropToWorldNearPlayer(item, radius:1.2f);
Mover.TryTakeFromWarehouseBuffer(0);
```

## 6. 事件（内置轮询或外部总线）
- 直接订阅：
```csharp
IMKDuckov.ItemEvents.OnItemAdded += it => { /* ... */ };
IMKDuckov.ItemEvents.OnItemRemoved += it => { /* ... */ };
IMKDuckov.ItemEvents.OnItemChanged += it => { /* ... */ };
IMKDuckov.ItemEvents.OnItemMoved += (it, from, to) => { /* ... */ };
IMKDuckov.WorldDrops.OnEnvironmentDrop += it => { /* ... */ };
```
- 外部事件模式：启用 `BeginExternalEvents()`，在你的补丁发出 `Publish*` 事件，结束 `EndExternalEvents()`。

## 7. 版本 / 能力 / 日志
```csharp
if (!IMKDuckov.Require(new Version(0,1,0), out var err)) { Debug.LogError(err); }
IMKDuckov.UseLogger(new MyLogger());
var caps = IMKDuckov.Capabilities; // 查看支持的能力位
```

## 8. 错误码约定（RichResult.Code）
- `Ok` 表示成功；失败常见值：
- `InvalidArgument`（参数错误/对象无效）
- `NotFound`（对象/字段/索引不存在）
- `NotSupported`（目标不支持此能力，如无 Stats/Slots/Setter）
- `DependencyMissing`（目标缺少重载/外部依赖，如无 Drop 重载）
- `OperationFailed`（运行期异常）

## 9. 最佳实践
- 主线程调用；尽量使用 `Read/Write/Mover/Factory` 的 Try* 方法
- 写入前考虑 `IMKDuckov.TryLock/Unlock`，并用事务包裹多步更改
- 读取 `RichResult`，根据 `Code` 降级或回退（例如 Drop 失败→仓库队列）
- 给变量键加前缀 `<ModName>_*`，避免冲突；IMK 保留 `IMK_*`

## 10. 示例与工具
- `ItemModKit.Samples` -> `IMKDebugWindow`：一键测试/压力测试/全量测试（含性能采样与回放追踪）
- `ItemSnapshot`：序列化/导出/比对，便于问题复现

更多协作与外部事件总线说明见 `docs/Collaboration.md`。

# IMK Features

Core:
- Debounced persistence scheduler with checksum & size guard
- Single blob meta (`IMK_Meta`) JSON/base64 selectable
- Extension capture (stats, effects) via contributors
- Rebirth replace API
- DirtyKind granular marking across core/tags/variables/modifiers/stats/effects/slots

Diagnostics:
- Performance sampling (`IMKPerf`)
- Debug window now includes only Super Stress test (removed quick/full/random suites)

Samples:
- `IMKDebugWindow` trimmed to Super Stress Test + log view.
- Other ad-hoc test harnesses removed for lean distribution.

Planned:
- Optional enable flags for contributors
- Incremental stat delta serialization
- Meta enrichers (compression / pruning)

### Clone Pipeline (Bus-Oriented)
A higher-level cloning facade combining strategy selection (TreeData→Unity fallback), selective variable merge, tag copy, placement, and optional UI refresh.

Example:
```
var opts = new ClonePipelineOptions { Strategy = CloneStrategy.Auto, VariableMerge = VariableMergeMode.OnlyMissing, Target = "character", Diagnostics = true };
var r = IMKDuckov.Clone.TryCloneToInventory(sourceItem, opts);
if (r.Ok) {
    var newItem = r.Value.NewItem;
    // use newItem
}
```
Diagnostics fields include: strategy, target, added, index, newTid, newName.

Filter variables on clone
```
// Example: accept only IMK_* keys (caller-controlled)
var opts = new ClonePipelineOptions {
  VariableMerge = VariableMergeMode.OnlyMissing,
  AcceptVariableKey = k => k != null && k.StartsWith("IMK_", StringComparison.Ordinal)
};
IMKDuckov.Clone.TryCloneToInventory(src, opts);
```
// Example: blacklist certain prefixes
var blocked = new[] { "D2T_", "TMP_" };
var opts2 = new ClonePipelineOptions {
  VariableMerge = VariableMergeMode.Overwrite,
  AcceptVariableKey = k => {
    if (string.IsNullOrEmpty(k)) return false;
    foreach (var p in blocked) if (k.StartsWith(p, StringComparison.Ordinal)) return false;
    return true;
  }
};

```

### Web endpoint: /item/clonePipeline
Query params:
- id: item id (optional, will use UI selection when missing)
- target: character|storage
- strategy: auto|tree|unity
- merge: none|missing|overwrite
- filter: imkonly|nod2t|prefixes|exclude
- prefixes: comma-separated for filter=prefixes or filter=exclude
- diag: true|false

Example:
- /item/clonePipeline?strategy=tree&merge=missing&filter=nod2t
- /item/clonePipeline?merge=overwrite&filter=exclude&prefixes=D2T_,TMP_

> Deprecation: Legacy TreeData clone endpoints (`/item/cloneTreeToBag`, `/item/cloneTreeAdvToBag`) and methods (`DuckovTreeDataService.TryCloneIntoInventoryAdvanced*`) now forward to the unified clone pipeline. Use `/item/clonePipeline` or call `IMKDuckov.Clone.TryCloneToInventory` directly. Legacy methods will be removed in a future release.
