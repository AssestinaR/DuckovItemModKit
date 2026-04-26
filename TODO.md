# IMK TODO

这份文件现在只保留还活着的 backlog，不再兼任历史记录、设计长文或阶段汇报。

## 当前焦点

- 继续把 IMK 收口成“物品结构与状态修改框架”，而不是扩张成通用 combat framework。
- 文档继续做减法：主入口只保留 README 和 working brief，其余说明优先回到代码注释。
- 当前最值得投入的代码面主线是 stats / weapon attributes，其次才是新的结构型扩展能力。

## 进行中

1. Stats / weapon attributes 基线整理
   - 收口稳定可读写的武器相关 stat key。
   - 补齐 Probe 样例和最小回归清单。
   - 继续把本地化读取复用到 stats 相关读层。

2. 文档减法
   - 继续删除重复文档和纯过渡索引。
   - 把剩余有效说明迁回 facade、contracts 和实现文件 XML 注释。
   - 只保留确有长期入口价值的少量 markdown。

## 下一批候选能力

1. Durability / use-count 初始化闭环
   - 目标：让“原本无耐久/次数语义的物品”具备稳定初始化、回滚、持久化和 Probe 闭环。

2. Stackability 初始化闭环
   - 目标：补齐“原本不可堆叠 -> 可堆叠”的初始化与验证链，而不只是已有 split/merge 原语。

3. Buffs draft 继续孵化
   - 保持在 draft facade。
   - 优先补齐更有价值的查询、层数语义和验证样例。
   - 不把它误推成完整 combat bridge。

## 延后但仍保留的方向

- behavior grafting 的最小能力，例如让非 use-like 物品具备有限 use 语义。
- runtime resource / damage-state 抽象。
- clone / restore / rebirth 主链继续瘦身和职责拆分。
- diagnostics 与 scheduler 的小型增强项。

## 明确不作为当前主线

- 通用 combat runtime bridge
- 大而全的玩法模板系统
- 复杂投射物、AI、地图机关、全局技能树

如果未来确实要承接这类能力，也应停留在外部逻辑层或薄 bridge 层，而不是继续扩大 IMK 核心承诺。

## 小型积压

- Effects 去重：若同类型 effect 已存在，可选跳过。
- Transaction API：评估 `Commit(flushImmediate)` 是否值得补入。
- PersistenceScheduler：评估 `Reset()` 与 backlog 自适应策略。
- stats schema：预留 `FormatVersion` 升级路径。
- 补最小单元测试支架，优先服务编辑器环境。
