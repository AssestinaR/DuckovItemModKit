# ItemModKit features / 功能总览与接口速查

本页面向模组开发者，列出 IMK 的常用接口，配合 README 的“入门/调用方式”可快速接入。

- 目标运行时：`netstandard2.1`
- 入口：`ItemModKit.Adapters.Duckov.IMKDuckov`（简称 `IMKDuckov`）

## 0. 观察层 / World Drop 捕获 (无 Harmony)
IMK 通过两条路径捕获物品位置变化：
1. 事件桥 (DuckovEventBridge)：订阅原生 Item 事件 (`onParentChanged/onItemTreeChanged` 等)；当物品从背包或槽位脱离且不再属于任何 Inventory/Slot 时，立即判定为“世界掉落”并调用 `WorldDrops.RegisterExternalWorldItem(item)`。
2. 定期轻量扫描 (DuckovWorldDropEventSource)：补漏未被事件及时触发的物品，触发 `OnEnvironmentDrop`。

特点：
- 不依赖 Harmony，不修改原方法；纯观察。
- 事件优先、扫描兜底；避免性能压力和漏报。
- 你可以只订阅 `IMKDuckov.WorldDrops.OnEnvironmentDrop` 获取世界中新增的可拾取物品。

使用示例：
```csharp
IMKDuckov.WorldDrops.OnEnvironmentDrop += raw => {
    var handle = IMKDuckov.TryGetHandle(raw); // 快速获得句柄
    // 分类 / 标记 / 自定义逻辑
};
```
如果你自行判定了一个物品已经是世界掉落（例如自定义生成逻辑）：
```csharp
IMKDuckov.WorldDrops.RegisterExternalWorldItem(myItem);
```
> 注意：仅在物品不属于任何 Inventory 且未插入 Slot 时调用，否则会被忽略。

## 1. 选择 / 查询 / 生成
- 选择：`UISelection.TryGetCurrentItem(out object item)` / `TryGetCurrentSlots(out object slotsOwner)`
- 查询：`Query.TryGetFromBackpack(index, out object item)` / `Query.TryFindByTypeId(typeId, out var list)`
- 生成/克隆/删除
```csharp
var r1 = IMKDuckov.Factory.TryGenerateByTypeId(123);
var r2 = IMKDuckov.Factory.TryCloneItem(item);
var r3 = IMKDuckov.Factory.TryDeleteItem(item);
```

## 2. 读取（Read）
- 快照：`ItemSnapshot.Capture(IMKDuckov.Item, item)`
- 修饰描述：`Read.TryReadModifierDescriptions(item)`
- 效果（基础）：`Read.TryReadEffects(item)`
- 效果（详细）：`Read.TryReadEffectsDetailed(item)`
- 效果（深度，新增）：`Read.TryReadEffectsDeep(item)` 返回 `EffectDetails[]`
  - 字段：`Name/Enabled/Display/Description`
  - 组件：`Triggers/Filters/Actions`（元素为 `EffectComponentDetails`：`Kind/Type/Properties`）
  - 回退建议：`TryReadEffectsDeep` 失败 → `TryReadEffectsDetailed` → `TryReadEffects`
- 统计：`Read.TryReadStats(item)`
- 插槽：`Read.TryReadSlots(item)`（含扩展字段：`SlotIcon/RequireTagKeys/ExcludeTagKeys/ForbidSameID/ContentTypeId/ContentName`）

示例（深度读取 Effects）
```csharp
var rr = IMKDuckov.Read.TryReadEffectsDeep(item);
if (rr.Ok)
{
    foreach (var e in rr.Value)
    {
        Debug.Log($"Effect: {e.Name} enabled={e.Enabled} display={e.Display} desc={e.Description}");
        foreach (var a in e.Actions)
        {
            var props = string.Join(",", a.Properties?.Keys ?? Array.Empty<string>());
            Debug.Log($"  Action<{a.Type}> props=[{props}]");
        }
    }
}
```

## 3. 写入（Write）
- 核心字段
```csharp
Write.TryWriteCoreFields(item, new CoreFieldChanges {
  RawName = "MyName", Value = 100, Quality = 2, DisplayQuality = 2
});
```
- 变量 / 常量 / 标签
```csharp
Write.TryWriteVariables(item, new[]{ new KeyValuePair<string,object>("My_VAR", 1) }, overwrite:true);
Write.TryWriteConstants(item, new[]{ new KeyValuePair<string,object>("My_CONST", "v") }, createIfMissing:true);
Write.TryWriteTags(item, new[]{ "TagA", "TagB" }, merge:true);
```
- 统计 / 修饰
```csharp
Write.TryEnsureStat(item, "Damage", 1f);
Write.TrySetStatValue(item, "Damage", 2.5f);
Write.TryAddModifier(item, "Damage", 0.1f, false, "Add", source:this);
Write.TryReapplyModifiers(item);
```
- 效果（基础）
```csharp
Write.TryAddEffect(item, "ItemStatsSystem.Effect");
Write.TrySetEffectProperty(item, idx, "description", "desc");
Write.TryAddEffectComponent(item, idx, "ItemStatsSystem.TickTrigger", "Trigger");
Write.TryEnableEffect(item, idx, true);
Write.TryRemoveEffect(item, idx);
```
- 效果（便捷/排序，新增）
```csharp
// 基础便捷项
Write.TryRenameEffect(item, idx, "NewName");
Write.TrySetEffectDisplay(item, idx, display:true);
Write.TrySetEffectDescription(item, idx, "Shown on UI");

// 效果排序（改变执行顺序）
Write.TryMoveEffect(item, fromIndex, toIndex);

// 组件排序（Trigger/Filter/Action）
Write.TryMoveEffectComponent(item, idx, "Action", from, to);

// 清理（移除空项/错误归属、清空组件 null）
Write.TrySanitizeEffects(item);

// 组件属性（仍可按需使用通用属性写入）
Write.TrySetEffectComponentProperty(item, idx, "Action", compIndex, "SomeNumber", 1.5f);
```
- 插槽（扩展字段已兼容）
```csharp
var slotsResult = Read.TryReadSlots(owner);
if (slotsResult.Ok) {
  foreach (var se in slotsResult.Value) {
    Debug.Log($"Slot {se.Key} occ={se.Occupied} type={se.PlugType} contentTid={se.ContentTypeId} contentName={se.ContentName}");
  }
}
Write.TryPlugIntoSlot(owner, slotKey, child);
Write.TryUnplugFromSlot(owner, slotKey);
Write.TryMoveBetweenSlots(owner, fromKey, toKey);
// 避免强行设置 DisplayName；游戏通常由第一个 requireTag 派生显示名
Write.TryAddSlot(owner, new SlotCreateOptions{ Key="Socket", RequireTags = new[]{"Barrel"} });
Write.TryRemoveSlot(owner, slotKey);
// 实验性接口（可用性依赖底层）：
IMKDuckov.Write.TrySetSlotIcon(owner, slotKey, mySprite);
IMKDuckov.Write.TrySetSlotTags(owner, slotKey, new[]{"Scope"}, new[]{"Melee"});
```
- 事务
```csharp
var token = Write.BeginTransaction(item);
var ok1 = Write.TryWriteTags(item, new[]{"Txn"}, true).Ok;
var ok2 = Write.TryWriteVariables(item, new[]{ new KeyValuePair<string,object>("TXN", 1) }, true).Ok;
if (ok1 && ok2) Write.CommitTransaction(item, token); else Write.RollbackTransaction(item, token);
```

## 4. 迁移 / 持久化 / 重生
- 元数据嵌入：`Persistence.RecordMeta(item, meta, writeVariables:true)` / `TryExtractMeta`
- 元数据结构 `ItemMeta`：NameKey/TypeId/Quality/DisplayQuality/Value/OwnerId/EmbeddedJson
- 替换（带定位）：
```csharp
var rep = Rebirth.Replace(oldItem, meta:null, keepLocation:true);
if (rep.Ok) { var newItem = rep.Value; }
```
- 所有者：`GetOwnerId(item)` / `IsOwnedBy(item, ownerId)` / `DuckovOwnership.Use(ownerId)`

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

## 6. 事件源 / 选择 / 外部事件
- 直接订阅：
```csharp
IMKDuckov.ItemEvents.OnItemAdded += it => { /* ... */ };
IMKDuckov.ItemEvents.OnItemRemoved += it => { /* ... */ };
IMKDuckov.ItemEvents.OnItemChanged += it => { /* ... */ };
IMKDuckov.WorldDrops.OnEnvironmentDrop += it => { /* 世界掉落 */ };
```
- 外部事件模式：`BeginExternalEvents()` → 发布事件 → `EndExternalEvents()`

事件语义补充：
- WorldDrops.OnEnvironmentDrop：物品进入“无归属”世界状态（未被任何 Inventory/Slot 持有）。重复登记自动去重。
- ItemEvents.OnItemChanged：包含位置变化、变量、标签、插槽等结构性变化。

## 7. 版本 / 能力 / 日志
```csharp
if (!IMKDuckov.Require(new Version(0,1,0), out var err)) { Debug.LogError(err); }
IMKDuckov.UseLogger(new MyLogger());
var caps = IMKDuckov.Capabilities; // 查看支持的模块位
```

## 8. 结果与错误（RichResult.Code）
- `Ok` 成功；失败查看 `Code` 与 `Error`
- `InvalidArgument` 参数/索引非法
- `NotFound` 目标/字段/集合不存在
- `NotSupported` 目标不支持（如某些 Stats/Slots/Setter）
- `DependencyMissing` 依赖缺失（掉落/外部资源）
- `OperationFailed` 运行时异常

## 9. 实用建议
- 多线程调用：仅使用 `Read/Write/Mover/Factory` 的 Try* 方法
- 写入前可用 `IMKDuckov.TryLock/Unlock` 做排他
- 错误提示建议透传 `RichResult.Error`（便于问题定位）
- 自定义变量建议前缀 `<ModName>_*`，避免与 IMK 内部 `IMK_*` 冲突

## 10. 示例与工具
- `IMKDebugWindow`：压力测试与日志视图
- `ItemSnapshot`：序列化/比对/回滚

---

### UI Mod 适配新版 Effects（速查）
- 首选 `Read.TryReadEffectsDeep`，失败降级到 `TryReadEffectsDetailed`/`TryReadEffects`
- 常用写接口：
  - 启用/禁用：`Write.TryEnableEffect`
  - 重命名：`Write.TryRenameEffect`
  - 显示/描述：`Write.TrySetEffectDisplay` / `Write.TrySetEffectDescription`
  - 排序：`Write.TryMoveEffect` / `TryMoveEffectComponent`
  - 属性：`TrySetEffectProperty` / `TrySetEffectComponentProperty`
  - 清理：`Write.TrySanitizeEffects`
- 交互建议：
  - 行内编辑 `Enabled/Display/Description/Name`
  - 拖拽排序 → 调用 `TryMove*`
  - 组件属性仅渲染基础类型；提交前可用事务批量提交
- 刷新：写入后重新调用 `Read.TryReadEffectsDeep` 或使用现有 UI 刷新方法

更多关于接口稳定性与阶段性新增，参见 `docs/API_FREEZE.md` 的 Stage 1 Additions
