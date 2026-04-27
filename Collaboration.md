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

## 10.5 运行时加固规则

后续修改 IMK 运行时代码时，默认额外遵守下面几条。

### A. 异常处理分层
- `public contract path`：下游 mod 可能直接调用，或直接影响公开返回结果的路径。
- `internal workflow path`：restore、rebirth、persistence、state replay 这类内部流程路径。
- `best-effort auxiliary path`：debug log、metrics、非关键 UI 刷新、附加 diagnostics 记录。

### B. `public contract path` 规则
- 禁止新增裸 `catch { }`。
- 失败时优先返回 `RichResult.Fail(...)`。
- 错误信息至少说明失败动作与缺失约束，不要只返回空值或静默降级。
- 若现有兼容签名无法返回 `RichResult`，至少补充稳定日志或 diagnostics 记录点。

### C. `internal workflow path` 规则
- 可以做局部保护，但不能让“主结果失败”无声消失。
- 主结果失败应尽量向上冒泡到结构化结果对象、最终 `RichResult` 或稳定 diagnostics。
- 只有附加步骤失败时，才允许退化为 best-effort。

### D. `best-effort auxiliary path` 规则
- 允许保护性吞异常。
- 但应只用于不改变业务主结果的路径。
- 若后续需要排查，应能补接 debug log、计数器或最近错误缓冲，而不是完全无痕。

### E. 反射规则
- 新增高频反射逻辑时，默认先进缓存或 access plan。
- 不要在热路径直接散写 `GetProperty`、`GetMethod`、`GetField` 多级试探链。
- 运行期优先执行已解析约束；结构探测尽量前移到初始化或首次命中阶段。

### F. facade 注释风格
- 对公开 facade helper，如果返回值本身承载了“失败时的默认行为”，应在 XML 注释里直接写清楚。
- `Try*` 风格方法：优先在 `returns` 或 `remarks` 中写明成功、未命中、失败时分别返回什么。
- 保兼容的 `void` helper：若失败时不会抛出兼容层异常，应在 `remarks` 中写明会通过 diagnostics 或日志暴露失败。
- 元组返回值：应写清每个槽位的语义，以及失败时的零值约定。
- 注释的目标不是解释实现细节，而是让后置 mod 作者不用翻源码也能理解“该怎么判断失败”。

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
