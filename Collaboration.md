# Multi-mod Collaboration Guidelines / 多模组协作约定

本文面向“多个模组同时依赖 IMK”的场景，提供可落地的协作模式与代码范式，尽量避免冲突与竞态。

## 1. Ownership / 归属标识
- IMK 会自动推断 `ItemMeta.OwnerId`（基于调用堆栈/入口程序集）。
- 跨模组代工时，务必用作用域覆写：
```csharp
using (DuckovOwnership.Use("YourModName"))
{
    // 期间所有 Persistence/Variables/Tags 写入都标注为 YourModName
    IMKDuckov.Write.TryWriteTags(item, new[]{"YourMod"}, merge:true);
}
```
- 查询归属：`IMKDuckov.GetOwnerId(item)`；校验归属：`IMKDuckov.IsOwnedBy(item, ownerId)`。

## 2. 变量键命名空间
- 框架保留前缀：`IMK_*`（含内嵌元数据、运行时标记、诊断等）。
- 你的模组用：`<ModName>_*` 或 `KeyHelper.BuildOwnedKey(ownerId, key)`。
- 约定：不删除非本模组前缀的键；变更时先读取并保留他方字段。

## 3. 并发互斥（强烈建议）
同时存在多个模组写入时，请在“同一物品”的写流程外层加锁，范围覆盖事务：
```csharp
var owner = "YourModName";
if (IMKDuckov.TryLock(item, owner))
{
    try
    {
        var tok = IMKDuckov.Write.BeginTransaction(item);
        var r1 = IMKDuckov.Write.TryWriteVariables(item, new[]{ KVP("YourMod_State", 1) }, true);
        var r2 = IMKDuckov.Write.TryWriteTags(item, new[]{"YourMod"}, true);
        if (r1.Ok && r2.Ok) IMKDuckov.Write.CommitTransaction(item, tok);
        else IMKDuckov.Write.RollbackTransaction(item, tok);
    }
    finally { IMKDuckov.Unlock(item, owner); }
}
```

## 4. 版本握手 / 能力探测
- 在模组初始化时校验最小版本：
```csharp
if (!IMKDuckov.Require(new System.Version(0,1,0), out var err))
{
    // 降级或提示用户升级 IMK
}
```
- 可读 `IMKDuckov.Capabilities` 判断可用能力位，按需降级。

## 5. 外部事件总线（替代轮询，推荐）
当你在补丁中能定位真实引擎事件时，建议启用外部事件模式：
```csharp
IMKDuckov.BeginExternalEvents(); // 初始化一次

// 在你的补丁中：
IMKDuckov.PublishItemAdded(item, new ItemEventContext {
    Source = ItemEventSourceType.Backpack,
    Cause  = ItemEventCause.Loot,
    Index  = slotIndex,
    OwnerId= IMKDuckov.GetOwnerId(item),
    Timestamp = UnityEngine.Time.unscaledTime
});
// 同理：Removed/Changed/Moved/Merged/Split

// 关闭：
IMKDuckov.EndExternalEvents();
```
- 说明：同一物品多事件在 `CoalesceWindow` 内会合并；外部模式开启时，IMK 内部轮询暂停，避免重复。

## 6. 读写统一服务（避免直反射）
- 读取：
```csharp
var snap = ItemSnapshot.Capture(IMKDuckov.Item, item);
var eff  = IMKDuckov.Read.TryReadEffects(item);
var mods = IMKDuckov.Read.TryReadModifierDescriptions(item);
var slots= IMKDuckov.Read.TryReadSlots(item);
```
- 写入：
```csharp
IMKDuckov.Write.TryWriteCoreFields(item, new CoreFieldChanges{ RawName="...", Value=10 });
IMKDuckov.Write.TryWriteVariables(item, new[]{ KVP("YourMod_X", 1) }, true);
IMKDuckov.Write.TryWriteTags(item, new[]{"YourMod"}, true);
```
- 返回统一 `RichResult`，请检查 `Ok/Code/Error` 做降级或回退。

## 7. 持久化与重生（跨模组一致性）
- 持久化：
```csharp
var meta = new ItemMeta{ NameKey = "Key", TypeId = 123, OwnerId = "YourMod" };
IMKDuckov.Persistence.RecordMeta(item, meta, writeVariables:true);
```
- 替换重生（接受 Component/GameObject/包装对象）：
```csharp
var rep = IMKDuckov.Rebirth.Replace(oldItem, meta:null, keepLocation:true);
if (rep.Ok) var newItem = rep.Value;
```
- 约定：重生前若他方也持有状态，请先 `TryExtractMeta` 并合并后写回。

## 8. 槽位/栈/背包操作的协作
- 槽位：若多个模组都会 `AddSlot/RemoveSlot`，务必约定前缀与编号规则（例如 `Socket/Socket1/Socket2...`），避免键名冲突。
- 栈：合并/拆分后请发布 `PublishItemMerged/Split`（外部模式）或等待 IMK 轮询捕捉。
- 背包：大量移动操作请分批或事务封装，并在失败时回退到 `SendToPlayer/SendToWarehouse`，避免丢失。

## 9. 错误码契约（跨模容错）
- `InvalidArgument`：输入对象/参数无效 → 立即放弃并记录。
- `NotFound`：目标不存在（例如无该 stat/slot）→ 可忽略或软失败。
- `NotSupported`：目标功能不支持（无 Stats/无 setter/无 Drop）→ 跳过并降级。
- `DependencyMissing`：缺少环境重载或外部依赖 → 采用回退策略（如 Drop→仓库队列）。
- `OperationFailed`：运行期异常 → 重试或记录后跳过。

## 10. 性能与时序
- 保证 IMK API 在主线程调用。
- 大量连续操作请 `yield` 或分帧执行，避免 UI 卡顿。
- 事件发布尽量去重与合并，减少风暴。

## 11. 示例：跨模组“代工改名 + 标记 + 归还位置”
```csharp
var owner = "YourMod";
if (!IMKDuckov.UISelection.TryGetCurrentItem(out var item) || item == null) return;
if (!IMKDuckov.TryLock(item, owner)) return;
try
{
    var txn = IMKDuckov.Write.BeginTransaction(item);
    var r1 = IMKDuckov.Write.TryWriteCoreFields(item, new CoreFieldChanges{ RawName = "[YourMod] " + IMKDuckov.Item.GetName(item) });
    var r2 = IMKDuckov.Write.TryWriteVariables(item, new[]{ new KeyValuePair<string,object>(owner+"_Touch", 1) }, true);
    if (r1.Ok && r2.Ok) IMKDuckov.Write.CommitTransaction(item, txn);
    else IMKDuckov.Write.RollbackTransaction(item, txn);
}
finally { IMKDuckov.Unlock(item, owner); }
```

以上约定可作为基线。若你的模组需要特殊协商（例如共享槽位命名、共享变量键、事件优先级），建议在 README 中明示。
