using System;
using System.Collections.Generic;
using ItemModKit.Core;
using System.Reflection;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 统一的“读取服务”：为上层提供稳定的只读接口，屏蔽底层反射/字段名差异。
    /// - Core 字段：名称、TypeId、品质、显示品质、价值
    /// - 集合：变量/常量/修饰器/标签/槽位
    /// - 推导信息：堆叠、耐久、检查状态、排序、重量、关系、标志、声音键
    /// - 高级：统计快照、子背包信息/枚举、效果列表、贴图/Agent 等
    /// </summary>
    internal sealed partial class ReadService : IReadService
    {
        private readonly IItemAdapter _item;
        public ReadService(IItemAdapter item) { _item = item; }

        #region Core fields & basic collections
        /// <summary>读取核心字段。</summary>
        public RichResult<CoreFields> TryReadCoreFields(object item)
        {
            try
            {
                if (item == null) return RichResult<CoreFields>.Fail(ErrorCode.InvalidArgument, "item is null");
                var t = item.GetType();
                var name = _item.GetName(item);
                var rawName = _item.GetDisplayNameRaw(item);
                var typeId = _item.GetTypeId(item);
                var quality = _item.GetQuality(item);
                var dispQ = _item.GetDisplayQuality(item);
                var value = _item.GetValue(item);
                return RichResult<CoreFields>.Success(new CoreFields { Name = name, RawName = rawName, TypeId = typeId, Quality = quality, DisplayQuality = dispQ, Value = value });
            }
            catch (Exception ex) { Log.Error("TryReadCoreFields failed", ex); return RichResult<CoreFields>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>读取变量表。</summary>
        public RichResult<VariableEntry[]> TryReadVariables(object item)
        {
            try { if (item == null) return RichResult<VariableEntry[]>.Fail(ErrorCode.InvalidArgument, "item is null"); return RichResult<VariableEntry[]>.Success(_item.GetVariables(item)); }
            catch (Exception ex) { Log.Error("TryReadVariables failed", ex); return RichResult<VariableEntry[]>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>读取常量表。</summary>
        public RichResult<VariableEntry[]> TryReadConstants(object item)
        {
            try { if (item == null) return RichResult<VariableEntry[]>.Fail(ErrorCode.InvalidArgument, "item is null"); return RichResult<VariableEntry[]>.Success(_item.GetConstants(item)); }
            catch (Exception ex) { Log.Error("TryReadConstants failed", ex); return RichResult<VariableEntry[]>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>读取标签列表。</summary>
        public RichResult<string[]> TryReadTags(object item)
        {
            try { if (item == null) return RichResult<string[]>.Fail(ErrorCode.InvalidArgument, "item is null"); return RichResult<string[]>.Success(_item.GetTags(item)); }
            catch (Exception ex) { Log.Error("TryReadTags failed", ex); return RichResult<string[]>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        #endregion

        #region Derived infos
        /// <summary>读取堆叠信息：最大堆叠与当前数量。</summary>
        public RichResult<StackInfo> TryReadStackInfo(object item)
        {
            try
            {
                if (item == null) return RichResult<StackInfo>.Fail(ErrorCode.InvalidArgument, "item is null");
                var t = item.GetType();
                var getMax = DuckovReflectionCache.GetGetter(t, EngineKeys.Property.MaxStackCount, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                int max = 1; try { var v = getMax?.Invoke(item); if (v != null) max = Convert.ToInt32(v); } catch { }
                int count = 1; try { var v = _item.GetVariable(item, EngineKeys.Variable.Count); if (v is int i) count = i; else if (v != null) count = Convert.ToInt32(v); } catch { }
                return RichResult<StackInfo>.Success(new StackInfo { Max = max, Count = count });
            }
            catch (Exception ex) { Log.Error("TryReadStackInfo failed", ex); return RichResult<StackInfo>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>读取耐久信息：最大/当前/损耗与是否需要检视。</summary>
        public RichResult<DurabilityInfo> TryReadDurabilityInfo(object item)
        {
            try
            {
                if (item == null) return RichResult<DurabilityInfo>.Fail(ErrorCode.InvalidArgument, "item is null");
                float max = 0f; try { var c = _item.GetConstant(item, EngineKeys.Constant.MaxDurability); if (c is float f) max = f; else if (c != null) max = Convert.ToSingle(c); } catch { }
                float cur = 0f; try { var v = _item.GetVariable(item, EngineKeys.Variable.Durability); if (v is float f) cur = f; else if (v != null) cur = Convert.ToSingle(v); } catch { }
                float loss = 0f; try { var v = _item.GetVariable(item, EngineKeys.Variable.DurabilityLoss); if (v is float f) loss = f; else if (v != null) loss = Convert.ToSingle(v); } catch { }
                bool needInsp = false; try { var gi = DuckovReflectionCache.GetGetter(item.GetType(), "NeedInspection", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); var v = gi?.Invoke(item); if (v != null) needInsp = Convert.ToBoolean(v); } catch { }
                return RichResult<DurabilityInfo>.Success(new DurabilityInfo { Max = max, Current = cur, Loss = loss, NeedInspection = needInsp });
            }
            catch (Exception ex) { Log.Error("TryReadDurabilityInfo failed", ex); return RichResult<DurabilityInfo>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>读取检视信息：已检视/正在检视/是否需要检视。</summary>
        public RichResult<InspectionInfo> TryReadInspectionInfo(object item)
        {
            try
            {
                if (item == null) return RichResult<InspectionInfo>.Fail(ErrorCode.InvalidArgument, "item is null");
                bool inspected = false; try { var v = _item.GetVariable(item, EngineKeys.Variable.Inspected); if (v is bool b) inspected = b; else if (v != null) inspected = Convert.ToBoolean(v); } catch { }
                var getInspecting = DuckovReflectionCache.GetGetter(item.GetType(), EngineKeys.Property.Inspecting, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                bool inspecting = false; try { var v = getInspecting?.Invoke(item); if (v is bool bb) inspecting = bb; else if (v != null) inspecting = Convert.ToBoolean(v); } catch { }
                bool needInsp = false; try { var gi = DuckovReflectionCache.GetGetter(item.GetType(), "NeedInspection", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); var v = gi?.Invoke(item); if (v != null) needInsp = Convert.ToBoolean(v); } catch { }
                return RichResult<InspectionInfo>.Success(new InspectionInfo { Inspected = inspected, Inspecting = inspecting, NeedInspection = needInsp });
            }
            catch (Exception ex) { Log.Error("TryReadInspectionInfo failed", ex); return RichResult<InspectionInfo>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>读取排序信息（Order）。</summary>
        public RichResult<OrderingInfo> TryReadOrderingInfo(object item)
        {
            try
            {
                if (item == null) return RichResult<OrderingInfo>.Fail(ErrorCode.InvalidArgument, "item is null");
                var get = DuckovReflectionCache.GetGetter(item.GetType(), "Order", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                int order = 0; try { var v = get?.Invoke(item); if (v != null) order = Convert.ToInt32(v); } catch { }
                return RichResult<OrderingInfo>.Success(new OrderingInfo { Order = order });
            }
            catch (Exception ex) { Log.Error("TryReadOrderingInfo failed", ex); return RichResult<OrderingInfo>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>读取重量信息：单件/自身/总重/基础重量。</summary>
        public RichResult<WeightInfo> TryReadWeightInfo(object item)
        {
            try
            {
                if (item == null) return RichResult<WeightInfo>.Fail(ErrorCode.InvalidArgument, "item is null");
                var t = item.GetType();
                var getUnit = DuckovReflectionCache.GetGetter(t, "UnitSelfWeight", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var getSelf = DuckovReflectionCache.GetGetter(t, "SelfWeight", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var getTotal = DuckovReflectionCache.GetGetter(t, "TotalWeight", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var getBase = DuckovReflectionCache.GetGetter(t, "weight", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                float unit = 0f, self = 0f, total = 0f, baseW = 0f;
                try { var v = getUnit?.Invoke(item); if (v != null) unit = Convert.ToSingle(v); } catch { }
                try { var v = getSelf?.Invoke(item); if (v != null) self = Convert.ToSingle(v); } catch { }
                try { var v = getTotal?.Invoke(item); if (v != null) total = Convert.ToSingle(v); } catch { }
                try
                {
                    var v = getBase?.Invoke(item);
                    if (v == null)
                    {
                        var f = DuckovReflectionCache.GetField(t, "weight", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (f != null) v = f.GetValue(item);
                    }
                    if (v != null) baseW = Convert.ToSingle(v);
                }
                catch { }
                return RichResult<WeightInfo>.Success(new WeightInfo { UnitSelfWeight = unit, SelfWeight = self, TotalWeight = total, BaseWeight = baseW });
            }
            catch (Exception ex) { Log.Error("TryReadWeightInfo failed", ex); return RichResult<WeightInfo>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>读取父子关系与位置：父物品/是否在背包/是否在槽位。</summary>
        public RichResult<RelationInfo> TryReadRelations(object item)
        {
            try
            {
                if (item == null) return RichResult<RelationInfo>.Fail(ErrorCode.InvalidArgument, "item is null");
                var t = item.GetType();
                var getParent = DuckovReflectionCache.GetGetter(t, "ParentItem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var getInv = DuckovReflectionCache.GetGetter(t, "InInventory", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var getSlot = DuckovReflectionCache.GetGetter(t, "PluggedIntoSlot", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                object parent = null; bool inInv = false; bool inSlot = false;
                try { parent = getParent?.Invoke(item); } catch { }
                try { inInv = getInv?.Invoke(item) != null; } catch { }
                try { inSlot = getSlot?.Invoke(item) != null; } catch { }
                return RichResult<RelationInfo>.Success(new RelationInfo { ParentItem = parent, InInventory = inInv, InSlot = inSlot });
            }
            catch (Exception ex) { Log.Error("TryReadRelations failed", ex); return RichResult<RelationInfo>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>读取标志位：粘性/可售/可丢弃/可修理/是否角色物品。</summary>
        public RichResult<FlagsInfo> TryReadFlags(object item)
        {
            try
            {
                if (item == null) return RichResult<FlagsInfo>.Fail(ErrorCode.InvalidArgument, "item is null");
                var t = item.GetType();
                var getSticky = DuckovReflectionCache.GetGetter(t, "Sticky", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var getSold = DuckovReflectionCache.GetGetter(t, "CanBeSold", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var getDrop = DuckovReflectionCache.GetGetter(t, "CanDrop", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var getRepair = DuckovReflectionCache.GetGetter(t, "Repairable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var getIsChar = DuckovReflectionCache.GetGetter(t, "IsCharacter", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                bool sticky = false, canSold = false, canDrop = false, repairable = false, isChar = false;
                try { var v = getSticky?.Invoke(item); if (v != null) sticky = Convert.ToBoolean(v); } catch { }
                try { var v = getSold?.Invoke(item); if (v != null) canSold = Convert.ToBoolean(v); } catch { }
                try { var v = getDrop?.Invoke(item); if (v != null) canDrop = Convert.ToBoolean(v); } catch { }
                try { var v = getRepair?.Invoke(item); if (v != null) repairable = Convert.ToBoolean(v); } catch { }
                try { var v = getIsChar?.Invoke(item); if (v != null) isChar = Convert.ToBoolean(v); } catch { }
                return RichResult<FlagsInfo>.Success(new FlagsInfo { Sticky = sticky, CanBeSold = canSold, CanDrop = canDrop, Repairable = repairable, IsCharacter = isChar });
            }
            catch (Exception ex) { Log.Error("TryReadFlags failed", ex); return RichResult<FlagsInfo>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>读取声音键（用于音效播放）。</summary>
        public RichResult<string> TryReadSoundKey(object item)
        {
            try
            {
                if (item == null) return RichResult<string>.Fail(ErrorCode.InvalidArgument, "item is null");
                string result = null;
                var t = item.GetType();
                var get = DuckovReflectionCache.GetGetter(t, "SoundKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ?? DuckovReflectionCache.GetGetter(t, "soundKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                try
                {
                    var v = get?.Invoke(item);
                    if (v == null)
                    {
                        var f = DuckovReflectionCache.GetField(t, "soundKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (f != null) v = f.GetValue(item);
                    }
                    if (v != null) result = Convert.ToString(v);
                }
                catch { }
                return RichResult<string>.Success(result);
            }
            catch (Exception ex) { Log.Error("TryReadSoundKey failed", ex); return RichResult<string>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        #endregion

        #region Advanced infos
        /// <summary>读取数值统计快照（遍历 Stats 集合）。</summary>
        /// <summary>读取子背包的容量与数量。</summary>
        public RichResult<InventorySnapshot> TryReadChildInventoryInfo(object item)
        {
            try
            {
                if (item == null) return RichResult<InventorySnapshot>.Fail(ErrorCode.InvalidArgument, "item is null");
                var getInvProp = DuckovReflectionCache.GetGetter(item.GetType(), "Inventory", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var inv = getInvProp?.Invoke(item);
                if (inv == null) return RichResult<InventorySnapshot>.Success(new InventorySnapshot { Capacity = 0, Count = 0 });
                int cap = 0; int cnt = 0;
                try { cap = Convert.ToInt32(DuckovReflectionCache.GetGetter(inv.GetType(), "Capacity", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(inv) ?? 0); } catch { }
                try
                {
                    var en = inv as System.Collections.IEnumerable;
                    if (en != null) { foreach (var it in en) { if (it != null) cnt++; } }
                    else { for (int i = 0; i < cap; i++) { var it = DuckovInventoryAdapter_GetItem(inv, i); if (it != null) cnt++; } }
                }
                catch { }
                return RichResult<InventorySnapshot>.Success(new InventorySnapshot { Capacity = cap, Count = cnt });
            }
            catch (Exception ex) { Log.Error("TryReadChildInventoryInfo failed", ex); return RichResult<InventorySnapshot>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>枚举子背包内的所有物品。</summary>
        public RichResult<object[]> TryEnumerateChildInventoryItems(object item)
        {
            try
            {
                if (item == null) return RichResult<object[]>.Fail(ErrorCode.InvalidArgument, "item is null");
                var inv = DuckovReflectionCache.GetGetter(item.GetType(), "Inventory", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item);
                if (inv == null) return RichResult<object[]>.Success(Array.Empty<object>());
                var list = new List<object>();
                try
                {
                    var en = inv as System.Collections.IEnumerable;
                    if (en != null) { foreach (var it in en) if (it != null) list.Add(it); }
                    else { int cap = 0; try { cap = Convert.ToInt32(DuckovReflectionCache.GetGetter(inv.GetType(), "Capacity", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(inv) ?? 0); } catch { } for (int i = 0; i < cap; i++) { var it = DuckovInventoryAdapter_GetItem(inv, i); if (it != null) list.Add(it); } }
                }
                catch { }
                return RichResult<object[]>.Success(list.ToArray());
            }
            catch (Exception ex) { Log.Error("TryEnumerateChildInventoryItems failed", ex); return RichResult<object[]>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>读取 ItemGraphic（是否存在、名称）。</summary>
        public RichResult<ItemGraphicSnapshot> TryReadItemGraphic(object item)
        {
            try
            {
                if (item == null) return RichResult<ItemGraphicSnapshot>.Fail(ErrorCode.InvalidArgument, "item is null");
                bool has = false; string name = null;
                try { var ig = DuckovReflectionCache.GetGetter(item.GetType(), "ItemGraphic", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item); if (ig != null) { has = true; try { name = Convert.ToString(DuckovTypeUtils.GetMaybe(ig, new[] { "name", "Name" })); } catch { name = ig.ToString(); } } } catch { }
                return RichResult<ItemGraphicSnapshot>.Success(new ItemGraphicSnapshot { HasGraphic = has, GraphicName = name });
            }
            catch (Exception ex) { Log.Error("TryReadItemGraphic failed", ex); return RichResult<ItemGraphicSnapshot>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>读取 AgentUtilities（是否存在激活 Agent、其名称）。</summary>
        public RichResult<AgentUtilitiesSnapshot> TryReadAgentUtilities(object item)
        {
            try
            {
                if (item == null) return RichResult<AgentUtilitiesSnapshot>.Fail(ErrorCode.InvalidArgument, "item is null");
                bool active = false; string name = null;
                try { var au = DuckovReflectionCache.GetGetter(item.GetType(), "ActiveAgent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item); if (au != null) { active = true; try { name = Convert.ToString(DuckovTypeUtils.GetMaybe(au, new[] { "name", "Name" })); } catch { name = au.ToString(); } } } catch { }
                return RichResult<AgentUtilitiesSnapshot>.Success(new AgentUtilitiesSnapshot { HasActiveAgent = active, ActiveAgentName = name });
            }
            catch (Exception ex) { Log.Error("TryReadAgentUtilities failed", ex); return RichResult<AgentUtilitiesSnapshot>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        #endregion

        /// <summary>捕获完整物品快照（委托 Snapshot 组件）。</summary>
        public RichResult<ItemSnapshot> Snapshot(object item)
        {
            try { if (item == null) return RichResult<ItemSnapshot>.Fail(ErrorCode.InvalidArgument, "item is null"); return RichResult<ItemSnapshot>.Success(ItemSnapshot.Capture(_item, item)); }
            catch (Exception ex) { Log.Error("Snapshot failed", ex); return RichResult<ItemSnapshot>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        private static object DuckovInventoryAdapter_GetItem(object inventory, int index)
        {
            try { var m = DuckovReflectionCache.GetMethod(inventory?.GetType(), "get_Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); return m?.Invoke(inventory, new object[] { index }); } catch { return null; }
        }
    }
}
