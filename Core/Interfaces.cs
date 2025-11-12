using System;

namespace ItemModKit.Core
{
    /// <summary>
    /// 物品读写适配器接口：提供对名称、品质、价值、变量、常量、修饰、插槽与标签的读写。
    /// </summary>
    public interface IItemAdapter
    {
        // Basic info
        /// <summary>获取物品显示名称。</summary>
        string GetName(object item);
        /// <summary>设置物品显示名称。</summary>
        void SetName(object item, string name);
        /// <summary>获取原始显示名称（未本地化）。</summary>
        string GetDisplayNameRaw(object item);
        /// <summary>设置原始显示名称。</summary>
        void SetDisplayNameRaw(object item, string raw);
        /// <summary>获取类型 ID。</summary>
        int GetTypeId(object item);
        /// <summary>设置类型 ID。</summary>
        void SetTypeId(object item, int typeId);
        /// <summary>获取品质。</summary>
        int GetQuality(object item);
        /// <summary>设置品质。</summary>
        void SetQuality(object item, int quality);
        /// <summary>获取显示品质。</summary>
        int GetDisplayQuality(object item);
        /// <summary>设置显示品质。</summary>
        void SetDisplayQuality(object item, int dq);
        /// <summary>获取价值。</summary>
        int GetValue(object item);
        /// <summary>设置价值。</summary>
        void SetValue(object item, int value);

        // Variables
        /// <summary>获取全部变量条目。</summary>
        VariableEntry[] GetVariables(object item);
        /// <summary>设置变量键的值（constant 标志指示是否常量）。</summary>
        void SetVariable(object item, string key, object value, bool constant);
        /// <summary>读取指定变量键的值。</summary>
        object GetVariable(object item, string key);
        /// <summary>移除指定变量。</summary>
        bool RemoveVariable(object item, string key);

        // Constants
        /// <summary>获取全部常量条目。</summary>
        VariableEntry[] GetConstants(object item);
        /// <summary>设置常量键（可选创建）。</summary>
        void SetConstant(object item, string key, object value, bool createIfNotExist);
        /// <summary>读取指定常量键的值。</summary>
        object GetConstant(object item, string key);
        /// <summary>移除指定常量。</summary>
        bool RemoveConstant(object item, string key);

        // Modifiers / Slots / Tags
        /// <summary>获取全部修饰器条目。</summary>
        ModifierEntry[] GetModifiers(object item);
        /// <summary>重新应用修饰器集合。</summary>
        void ReapplyModifiers(object item);
        /// <summary>获取全部插槽条目。</summary>
        SlotEntry[] GetSlots(object item);
        /// <summary>获取标签集合。</summary>
        string[] GetTags(object item);
        /// <summary>设置标签集合（覆盖或合并由实现决定）。</summary>
        void SetTags(object item, string[] tags);
    }

    /// <summary>
    /// 背包/容器适配器：判断物品是否在背包、获取容器容量、定位与添加/合并等。
    /// </summary>
    public interface IInventoryAdapter
    {
        /// <summary>判断物品是否处于某背包中。</summary>
        bool IsInInventory(object item);
        /// <summary>获取物品所属的背包对象。</summary>
        object GetInventory(object item);
        /// <summary>获取背包容量。</summary>
        int GetCapacity(object inventory);
        /// <summary>按索引获取背包中的物品。</summary>
        object GetItemAt(object inventory, int index);
        /// <summary>获取指定物品在背包中的索引。</summary>
        int IndexOf(object inventory, object item);
        /// <summary>在指定索引处添加物品。</summary>
        bool AddAt(object inventory, object item, int index);
        /// <summary>添加物品并尝试与堆叠合并。</summary>
        bool AddAndMerge(object inventory, object item);
        /// <summary>将物品从其背包中分离。</summary>
        void Detach(object item);
    }

    /// <summary>
    /// 槽位适配器：用于将物品尝试插入到角色槽位。
    /// </summary>
    public interface ISlotAdapter
    {
        /// <summary>尝试把物品插入到角色可用槽位（从 preferredFirstIndex 开始）。</summary>
        bool TryPlugToCharacter(object newItem, int preferredFirstIndex = 0);
    }

    /// <summary>
    /// 持久化接口：记录/提取/应用元数据，清理模板效果，以及是否应纳管。
    /// </summary>
    public interface IItemPersistence
    {
        /// <summary>记录物品元数据（可选写变量）。</summary>
        void RecordMeta(object item, ItemMeta meta, bool writeVariables);
        /// <summary>尝试提取元数据。</summary>
        bool TryExtractMeta(object item, out ItemMeta meta);
        /// <summary>确保元数据已应用到物品。</summary>
        bool EnsureApplied(object item);
        /// <summary>清理模板效果。</summary>
        void ClearTemplateEffects(object item);
        /// <summary>判断物品是否需要纳入持久化管理。</summary>
        bool ShouldConsider(object item);
    }

    /// <summary>
    /// 查询接口：从背包/仓库/任意背包定位物品，或枚举集合。
    /// </summary>
    public interface IItemQuery
    {
        /// <summary>按 1-based 索引从角色背包获取物品。</summary>
        bool TryGetFromBackpack(int index1Based, out object item);
        /// <summary>按 1-based 索引从仓库获取物品。</summary>
        bool TryGetFromStorage(int index1Based, out object item);
        /// <summary>按 1-based 索引从任意背包获取物品。</summary>
        bool TryGetFromAnyInventory(int index1Based, out object item);
        /// <summary>按 1-based 索引获取武器槽物品。</summary>
        bool TryGetWeaponSlot(int slotIndex1Based, out object item);
        /// <summary>枚举角色背包全部物品。</summary>
        System.Collections.Generic.IEnumerable<object> EnumerateBackpack();
        /// <summary>枚举仓库全部物品。</summary>
        System.Collections.Generic.IEnumerable<object> EnumerateStorage();
        /// <summary>枚举所有可发现的背包物品集合。</summary>
        System.Collections.Generic.IEnumerable<object> EnumerateAllInventories();
    }

    /// <summary>
    /// UI 选中项辅助：提供当前详情/操作菜单目标的读取。
    /// </summary>
    public interface IUISelection
    {
        /// <summary>尝试获取详情面板当前物品。</summary>
        bool TryGetDetailsItem(out object item);
        /// <summary>尝试获取操作菜单当前物品。</summary>
        bool TryGetOperationMenuItem(out object item);
        /// <summary>获取当前物品：优先操作菜单，其次详情面板。</summary>
        bool TryGetCurrentItem(out object item);
    }

    /// <summary>
    /// 物品事件源：新增/移除/变化事件。
    /// </summary>
    public interface IItemEventSource
    {
        /// <summary>物品新增事件。</summary>
        event System.Action<object> OnItemAdded;
        /// <summary>物品移除事件。</summary>
        event System.Action<object> OnItemRemoved;
        /// <summary>物品变化事件。</summary>
        event System.Action<object> OnItemChanged;
    }

    /// <summary>
    /// 世界掉落事件源：敌人/环境掉落。
    /// </summary>
    public interface IWorldDropEventSource
    {
        /// <summary>敌人掉落事件。</summary>
        event System.Action<object> OnEnemyDrop;
        /// <summary>环境掉落事件。</summary>
        event System.Action<object> OnEnvironmentDrop;
    }

    /// <summary>
    /// 重生/替换服务：用新物品替换旧物品，必要时保持位置。
    /// </summary>
    public interface IRebirthService
    {
        /// <summary>替换旧物品并应用元数据。</summary>
        /// <param name="oldItem">原物品。</param>
        /// <param name="meta">元数据。</param>
        /// <param name="keepLocation">是否保持位置。</param>
        /// <returns>结果，包含新物品。</returns>
        RichResult<object> ReplaceRebirth(object oldItem, ItemMeta meta, bool keepLocation = true);
    }

    /// <summary>变量条目。</summary>
    public struct VariableEntry 
    { 
        /// <summary>变量键。</summary>
        public string Key; 
        /// <summary>变量值。</summary>
        public object Value; 
        /// <summary>是否常量（不可变）。</summary>
        public bool Constant; 
    }
    /// <summary>修饰条目。</summary>
    public struct ModifierEntry 
    { 
        /// <summary>统计键。</summary>
        public string Key; 
        /// <summary>数值。</summary>
        public float Value; 
        /// <summary>修饰类型名（如 Add/Multiply）。</summary>
        public string Modifier; 
        /// <summary>是否为百分比。</summary>
        public bool IsPercent; 
    }
    /// <summary>插槽条目。</summary>
    public struct SlotEntry 
    { 
        /// <summary>槽位键。</summary>
        public string Key; 
        /// <summary>是否已占用。</summary>
        public bool Occupied; 
        /// <summary>可插入类型。</summary>
        public string PlugType; 
    }

    /// <summary>物品定位结果句柄。</summary>
    public readonly struct ItemHandle
    {
        /// <summary>定位到的物品实例。</summary>
        public readonly object Item; 
        /// <summary>所在背包实例（可为空）。</summary>
        public readonly object Inventory; 
        /// <summary>在背包中的 1-based 索引（无则 0 或负）。</summary>
        public readonly int Index1Based; 
        /// <summary>所在槽位键（可为空）。</summary>
        public readonly string SlotKey;
        /// <summary>构造句柄。</summary>
        public ItemHandle(object item, object inventory, int index1Based, string slotKey) { Item = item; Inventory = inventory; Index1Based = index1Based; SlotKey = slotKey; }
        /// <summary>是否携带背包信息。</summary>
        public bool HasInventory => Inventory != null;
        /// <summary>是否携带槽位信息。</summary>
        public bool HasSlot => !string.IsNullOrEmpty(SlotKey);
    }

    /// <summary>通用错误码。</summary>
    public enum ErrorCode
    {
        /// <summary>无错误。</summary>
        None = 0,
        /// <summary>未找到目标。</summary>
        NotFound,
        /// <summary>参数无效。</summary>
        InvalidArgument,
        /// <summary>索引越界。</summary>
        OutOfRange,
        /// <summary>依赖缺失。</summary>
        DependencyMissing,
        /// <summary>操作失败。</summary>
        OperationFailed,
        /// <summary>不支持的操作。</summary>
        NotSupported,
        /// <summary>未授权。</summary>
        Unauthorized,
        /// <summary>冲突（状态不一致）。</summary>
        Conflict
    }

    /// <summary>统一返回结果（不含值）。</summary>
    public readonly struct RichResult
    {
        /// <summary>是否成功。</summary>
        public bool Ok { get; }
        /// <summary>错误信息（成功时为空）。</summary>
        public string Error { get; }
        /// <summary>错误码。</summary>
        public ErrorCode Code { get; }
        /// <summary>创建成功结果。</summary>
        public static RichResult Success() => new RichResult(true, null, ErrorCode.None);
        /// <summary>创建失败结果。</summary>
        public static RichResult Fail(ErrorCode code, string err) => new RichResult(false, err, code);
        private RichResult(bool ok, string error, ErrorCode code) { Ok = ok; Error = error; Code = code; }
    }

    /// <summary>携带值的统一返回结果。</summary>
    public readonly struct RichResult<T>
    {
        /// <summary>是否成功。</summary>
        public bool Ok { get; }
        /// <summary>错误信息。</summary>
        public string Error { get; }
        /// <summary>错误码。</summary>
        public ErrorCode Code { get; }
        /// <summary>返回值。</summary>
        public T Value { get; }
        /// <summary>创建成功结果。</summary>
        public static RichResult<T> Success(T v) => new RichResult<T>(true, null, ErrorCode.None, v);
        /// <summary>创建失败结果。</summary>
        public static RichResult<T> Fail(ErrorCode code, string err) => new RichResult<T>(false, err, code, default(T));
        private RichResult(bool ok, string error, ErrorCode code, T v) { Ok = ok; Error = error; Code = code; Value = v; }
    }

    /// <summary>
    /// 物品工厂接口：实例化/生成/克隆/删除等操作。
    /// </summary>
    public interface IItemFactory
    {
        /// <summary>按类型 ID 实例化（原始方式）。</summary>
        RichResult<object> TryInstantiateByTypeId(int typeId);
        /// <summary>从预制对象实例化。</summary>
        RichResult<object> TryInstantiateFromPrefab(object prefab);
        /// <summary>按类型 ID 生成（可带适配器级回退）。</summary>
        RichResult<object> TryGenerateByTypeId(int typeId);
        /// <summary>注册动态条目以便后续快速生成。</summary>
        RichResult TryRegisterDynamicEntry(object prefab);
        /// <summary>克隆现有物品。</summary>
        RichResult<object> TryCloneItem(object item);
        /// <summary>删除物品实例。</summary>
        RichResult TryDeleteItem(object item);
    }

    /// <summary>
    /// 物品移动接口：跨背包/仓库/世界以及堆叠操作。
    /// </summary>
    public interface IItemMover
    {
        // Inventory
        /// <summary>添加到背包（可选索引与合并）。</summary>
        RichResult TryAddToInventory(object item, object inventory, int? index = null, bool allowMerge = true);
        /// <summary>从背包移除物品。</summary>
        RichResult TryRemoveFromInventory(object item);
        /// <summary>在同一背包移动物品位置。</summary>
        RichResult TryMoveInInventory(object inventory, int fromIndex, int toIndex);
        /// <summary>跨背包转移物品。</summary>
        RichResult TryTransferBetweenInventories(object item, object fromInventory, object toInventory, int? toIndex = null, bool allowMerge = true);
        // Send
        /// <summary>发送到玩家（可选不合并与送仓库）。</summary>
        RichResult TrySendToPlayer(object item, bool dontMerge = false, bool sendToStorage = true);
        /// <summary>仅发送到玩家角色背包。</summary>
        RichResult TrySendToPlayerInventory(object item, bool dontMerge = false);
        /// <summary>发送到仓库（可选进入缓冲区）。</summary>
        RichResult TrySendToWarehouse(object item, bool directToBuffer = false);
        /// <summary>从仓库缓冲区取出指定索引物品。</summary>
        RichResult TryTakeFromWarehouseBuffer(int index);
        // World
        /// <summary>在玩家附近丢到世界地面。</summary>
        RichResult TryDropToWorldNearPlayer(object item, float radius = 1f);
        /// <summary>按坐标丢到世界地面。</summary>
        RichResult TryDropToWorld(object item, float x, float y, float z, bool usePhysics = true, float fx = 0f, float fy = 0f, float fz = 0f);
        // Stacks
        /// <summary>拆分堆叠。</summary>
        RichResult<object> TrySplitStack(object item, int count);
        /// <summary>合并两个堆叠。</summary>
        RichResult TryMergeStacks(object a, object b);
        /// <summary>重新打包多个堆叠。</summary>
        RichResult TryRepackStacks(object[] items);
    }

    /// <summary>
    /// 变量合并服务：按模式合并 source->target 的变量集合（可选筛选键）。
    /// </summary>
    public interface IVariableMergeService
    {
        /// <summary>执行变量合并。</summary>
        void Merge(object source, object target, VariableMergeMode mode, Func<string, bool> acceptKey = null);
    }

    /// <summary>
    /// UI 刷新服务：触发背包刷新与需要检查状态变更。
    /// </summary>
    public interface IUIRefreshService
    {
        /// <summary>刷新背包 UI。</summary>
        void RefreshInventory(object inventory, bool markNeedInspection = true);
    }

    /// <summary>
    /// 背包解析服务：解析字符串目标到具体背包（如 character/storage）。
    /// </summary>
    public interface IInventoryResolver
    {
        /// <summary>解析指定标识到背包。</summary>
        object Resolve(string target);
        /// <summary>解析默认背包（通常为角色背包）。</summary>
        object ResolveFallback();
    }

    /// <summary>
    /// 背包放置服务：尝试放置并在必要时安排延迟重试。
    /// </summary>
    public interface IInventoryPlacementService
    {
        /// <summary>尝试将物品放入背包（返回添加结果、索引与是否安排延迟）。</summary>
        (bool added, int index, bool deferredScheduled) TryPlace(object inventory, object item, bool allowMerge = true, bool enableDeferredRetry = true);
    }
}
