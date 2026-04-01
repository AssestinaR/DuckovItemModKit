# IMK TODO（面向总线的重构计划）

愿景
- 将核心重构为面向总线的架构：由单一职责的小服务组成，并通过门面统一对外暴露。
- 将 TreeData clone 提升为一等策略，但职责仅聚焦于结构化克隆/序列化本身。

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

近期已完成
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
