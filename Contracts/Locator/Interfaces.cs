using System;
using System.Collections.Generic;

namespace ItemModKit.Core.Locator
{
    /// <summary>
    /// 轻量、与引擎解耦的物品 handle。
    /// 它通过弱语义的 resolver 引用运行时对象，并允许在 replace/rebirth 后通过 LogicalId 重新绑定到“同一件逻辑物品”。
    /// </summary>
    public interface IItemHandle
    {
        /// <summary>当前是否还能解析到有效的运行时对象。</summary>
        bool IsAlive { get; }

        /// <summary>尝试获取底层运行时对象；对象已销毁或解析失败时返回 null。</summary>
        object TryGetRaw();

        /// <summary>底层实例 ID；用于快速查找、日志和调试。</summary>
        int? InstanceId { get; }

        /// <summary>逻辑 ID；用于跨 replace/rebirth 维持身份连续性。</summary>
        string LogicalId { get; }

        /// <summary>重新绑定逻辑 ID；通常由逻辑 ID 映射服务内部调用。</summary>
        void RebindLogical(string newId);

        /// <summary>缓存的类型 ID。</summary>
        int TypeId { get; }

        /// <summary>缓存的显示名称。</summary>
        string DisplayName { get; }

        /// <summary>缓存的标签集合。</summary>
        string[] Tags { get; }

        /// <summary>刷新缓存元数据，不改变 handle 身份。</summary>
        void RefreshMetadata();
    }

    /// <summary>
    /// locator 主入口。
    /// 当调用方需要把裸对象、实例 ID、逻辑 ID 或当前 UI 选中项统一转换为 handle 时，应从这里进入。
    /// </summary>
    public interface IItemLocator
    {
        /// <summary>根据运行时实例创建或解析 handle。</summary>
        IItemHandle FromInstance(object raw);

        /// <summary>根据实例 ID 解析 handle。</summary>
        IItemHandle FromInstanceId(int instanceId);

        /// <summary>根据逻辑 ID 解析 handle。</summary>
        IItemHandle FromLogicalId(string id);

        /// <summary>从当前 UI 选中项解析 handle。</summary>
        IItemHandle FromUISelection();

        /// <summary>返回最近创建的 handle；常用于事件驱动路径的快速访问。</summary>
        IItemHandle LastCreated();

        /// <summary>
        /// 按给定谓词和范围查询 handle 集合。
        /// 当前 predicate 仍保持宽泛 object 形态，后续会进一步向专门的 Query API 收束。
        /// </summary>
        IItemHandle[] Query(object predicate = null, IItemScope scope = null);
    }

    /// <summary>
    /// locator 侧的快速索引。
    /// 主要负责在创建、销毁、移动等事件发生时维护实例 ID 到 handle 的映射。
    /// </summary>
    public interface IItemIndex
    {
        /// <summary>通知索引某个对象刚刚创建。</summary>
        void OnCreated(object raw);

        /// <summary>通知索引某个对象已经销毁。</summary>
        void OnDestroyed(object raw);

        /// <summary>通知索引某个对象已经移动到新容器。</summary>
        void OnMoved(object raw, object newContainer = null);

        /// <summary>按实例 ID 查找 handle。</summary>
        IItemHandle FindByInstanceId(int instanceId);

        /// <summary>查找指定 TypeId 的全部 handle。</summary>
        IItemHandle[] FindAllByTypeId(int typeId);
    }

    /// <summary>
    /// 背包分类器。
    /// 负责把运行时容器归类为玩家背包、仓库、战利品箱、世界容器等抽象类别。
    /// </summary>
    public interface IInventoryClassifier
    {
        /// <summary>对指定背包对象进行分类。</summary>
        InventoryKind ClassifyInventory(object inv);

        /// <summary>判断是否为战利品箱或同类临时容器。</summary>
        bool IsLootBox(object inv);

        /// <summary>判断是否为玩家主背包或角色常驻容器。</summary>
        bool IsPlayerInventory(object inv);

        /// <summary>判断是否为仓库存储。</summary>
        bool IsStorage(object inv);
    }

    /// <summary>
    /// 查询范围提示。
    /// 不同适配器可以通过它定义“哪些物品算在当前查询范围内”。
    /// </summary>
    public interface IItemScope
    {
        /// <summary>判断给定物品及其宿主关系是否应包含在当前范围中。</summary>
        bool Includes(object rawItem, object inventory, object ownerItem);
    }

    /// <summary>抽象背包类别。</summary>
    public enum InventoryKind
    {
        /// <summary>无法识别或未分类。</summary>
        Unknown = 0,

        /// <summary>玩家主背包或角色常驻容器。</summary>
        Player = 1,

        /// <summary>长期存储容器，例如仓库。</summary>
        Storage = 2,

        /// <summary>临时战利品箱或拾取容器。</summary>
        LootBox = 3,

        /// <summary>世界容器或地面掉落承载对象。</summary>
        World = 4,

        /// <summary>其他未单独建模的容器类型。</summary>
        Other = 5,
    }

    /// <summary>背包 handle。</summary>
    public interface IInventoryHandle
    {
        /// <summary>容器实例 ID。</summary>
        int InstanceId { get; }

        /// <summary>容器容量。</summary>
        int Capacity { get; }

        /// <summary>容器分类。</summary>
        InventoryKind Kind { get; }

        /// <summary>拥有该容器的宿主物品；无宿主时可为 null。</summary>
        IItemHandle OwnerItem { get; }

        /// <summary>底层运行时对象。</summary>
        object Raw { get; }
    }

    /// <summary>槽位 handle。</summary>
    public interface ISlotHandle
    {
        /// <summary>槽位键。</summary>
        string Key { get; }

        /// <summary>槽位所属宿主。</summary>
        IItemHandle Owner { get; }

        /// <summary>槽位当前是否被占用。</summary>
        bool Occupied { get; }

        /// <summary>槽位内容物；未占用时可为 null。</summary>
        IItemHandle Content { get; }

        /// <summary>底层运行时对象。</summary>
        object Raw { get; }
    }

    /// <summary>
    /// 宿主关系服务。
    /// 用于从任意物品 handle 反查 owner、角色根、背包或槽位。
    /// </summary>
    public interface IOwnershipService
    {
        /// <summary>获取直接宿主物品。</summary>
        IItemHandle GetOwner(IItemHandle item);

        /// <summary>获取角色根物品。</summary>
        IItemHandle GetCharacterRoot(IItemHandle item);

        /// <summary>获取所在背包 handle。</summary>
        IInventoryHandle GetInventory(IItemHandle item);

        /// <summary>获取所在槽位 handle。</summary>
        ISlotHandle GetSlot(IItemHandle item);
    }

    /// <summary>
    /// handle 语义下的组合式查询接口。
    /// 与 Core.IItemQuery 不同，这里强调链式过滤、宿主关系和范围控制。
    /// </summary>
    public interface IItemQuery
    {
        /// <summary>按 TypeId 过滤。</summary>
        IItemQuery ByTypeId(int typeId);

        /// <summary>限制到指定背包。</summary>
        IItemQuery InInventory(IInventoryHandle inventory);

        /// <summary>限制到指定查询范围。</summary>
        IItemQuery InScope(IItemScope scope);

        /// <summary>按标签过滤，采用 AND 语义。</summary>
        IItemQuery ByTags(params string[] tags);

        /// <summary>按标签过滤，采用 OR 语义。</summary>
        IItemQuery ByTagAny(params string[] tags);

        /// <summary>按名称包含关系过滤。</summary>
        IItemQuery NameContains(string part);

        /// <summary>限制到 owner 链条中包含指定根物品的条目。</summary>
        IItemQuery OwnedBy(IItemHandle ownerRoot);

        /// <summary>按是否处于装备状态过滤。</summary>
        IItemQuery Equipped(bool equipped = true);

        /// <summary>按 owner 链深度过滤，闭区间为 [min, max]。</summary>
        IItemQuery Depth(int min, int max);

        /// <summary>取首个匹配项；无结果时返回 null。</summary>
        IItemHandle First();

        /// <summary>取前 count 个结果。</summary>
        IItemHandle[] Take(int count);

        /// <summary>取全部结果。</summary>
        IItemHandle[] All();

        /// <summary>清空当前累积的过滤条件。</summary>
        IItemQuery ResetPredicates();
    }

    /// <summary>新的 UI 选中项接口，直接返回 handle 或 inventory handle。</summary>
    public interface IUISelectionV2
    {
        /// <summary>尝试获取当前选中的物品 handle。</summary>
        bool TryGetCurrent(out IItemHandle handle);

        /// <summary>尝试获取当前选中的背包 handle。</summary>
        bool TryGetCurrentInventory(out IInventoryHandle inventory);
    }

    /// <summary>
    /// 逻辑 ID 映射服务。
    /// 用于在 replace/rebirth 后把旧 handle 和新 handle 绑定到同一逻辑身份上。
    /// </summary>
    public interface ILogicalIdMap
    {
        /// <summary>把旧 handle 与新 handle 绑定到同一个逻辑 ID。</summary>
        void Bind(IItemHandle oldItem, IItemHandle newItem);

        /// <summary>按逻辑 ID 解析 handle。</summary>
        IItemHandle Resolve(string logicalId);

        /// <summary>尝试获取某个 handle 当前对应的逻辑 ID。</summary>
        bool TryGetLogicalId(IItemHandle item, out string logicalId);

        /// <summary>解除某个 handle 的逻辑 ID 绑定。</summary>
        void Unbind(IItemHandle item);
    }
}
