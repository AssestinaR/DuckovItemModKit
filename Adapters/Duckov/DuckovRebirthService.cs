using System;
using ItemModKit.Core;
using ItemStatsSystem;
using UnityEngine;
using static ItemModKit.Adapters.Duckov.DuckovTypeUtils;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 重生服务：根据旧物品与元数据生成新物品，并尽量保持原位置（背包或角色槽位）。
    /// - 若 keepLocation=true 且旧物品在背包：尝试原索引替换，否则合并放入，失败则发给玩家
    /// - 若 keepLocation=false：尝试插入角色槽位，否则发给玩家
    /// 最后销毁旧物品，刷新相关背包，并立即持久化新物品的核心/变量/标签
    /// </summary>
    internal sealed class DuckovRebirthService : IRebirthService
    {
        private readonly IItemAdapter _item; private readonly IInventoryAdapter _inv; private readonly ISlotAdapter _slot; private readonly IItemPersistence _persist;
        /// <summary>构造函数：注入物品/背包/槽位/持久化适配器。</summary>
        public DuckovRebirthService(IItemAdapter item, IInventoryAdapter inv, ISlotAdapter slot, IItemPersistence persist) { _item = item; _inv = inv; _slot = slot; _persist = persist; }
        /// <summary>
        /// 用指定元数据替换旧物品并生成新物品（若 meta 为空则从旧物品推导）。
        /// keepLocation 控制是否尝试保持原位置。
        /// </summary>
        public RichResult<object> ReplaceRebirth(object oldItem, ItemMeta meta, bool keepLocation = true)
        {
            try
            {
                var oldComp = UnwrapToItem(oldItem);
                var eff = EnsureMetaFromObject(meta, oldItem);
                int typeId = eff?.TypeId > 0 ? eff.TypeId : SafeTypeId(oldItem);
                var newItem = ItemAssetsCollection.InstantiateSync(typeId);
                if (!newItem) return RichResult<object>.Fail(ErrorCode.OperationFailed, "instantiate failed");

                _persist?.RecordMeta(newItem, eff, writeVariables: true);
                try { var tags = _item.GetTags(oldItem); if (tags != null && tags.Length > 0) _item.SetTags(newItem, tags); } catch { }
                try
                {
                    foreach (var v in _item.GetVariables(oldItem) ?? System.Array.Empty<VariableEntry>())
                    {
                        if (string.IsNullOrEmpty(v.Key)) continue;
                        if (!v.Key.StartsWith("IMK_", System.StringComparison.Ordinal)) continue;
                        _item.SetVariable(newItem, v.Key, v.Value, true);
                    }
                }
                catch { }
                TrySet(newItem, EngineKeys.Property.Inspected, true);

                if (keepLocation && _inv.IsInInventory(oldItem))
                {
                    var inv = _inv.GetInventory(oldItem);
                    int idx = _inv.IndexOf(inv, oldItem);
                    _inv.Detach(oldItem);
                    bool added = false;
                    if (idx >= 0) added = _inv.AddAt(inv, newItem, idx);
                    if (!added) added = _inv.AddAndMerge(inv, newItem);
                    if (!added) SendToPlayer(newItem);
                }
                else
                {
                    bool plugged = _slot.TryPlugToCharacter(newItem, 0);
                    if (!plugged) SendToPlayer(newItem);
                    try { _inv.Detach(oldItem); } catch { }
                }

                try
                {
                    if (oldComp) UnityEngine.Object.DestroyImmediate(oldComp.gameObject);
                    else if (oldItem is Component c) UnityEngine.Object.DestroyImmediate(c.gameObject);
                    else if (oldItem is GameObject go) UnityEngine.Object.DestroyImmediate(go);
                }
                catch { }

                TryRefreshInventories();
                // 持久化：标记并强制刷新
                try { IMKDuckov.MarkDirty(newItem, DirtyKind.Core | DirtyKind.Tags | DirtyKind.Variables, immediate: true); IMKDuckov.FlushDirty(newItem, force: true); } catch { }
                return RichResult<object>.Success(newItem);
            }
            catch (Exception ex)
            {
                Log.Error("ReplaceRebirth failed", ex);
                return RichResult<object>.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>安全获取类型 ID（失败返回 0）。</summary>
        private static int SafeTypeId(object obj)
        {
            try { return IMKDuckov.Item.GetTypeId(obj); } catch { return 0; }
        }

        /// <summary>若未提供 meta，则从旧物品（嵌入/变量/直接读取）推导一个。</summary>
        private ItemMeta EnsureMetaFromObject(ItemMeta meta, object old)
        {
            try
            {
                if (meta != null) return meta;
                try { if (_persist != null && _persist.TryExtractMeta(old, out var m) && m != null) return m; } catch { }
                return new ItemMeta
                {
                    NameKey = _item.GetDisplayNameRaw(old) ?? _item.GetName(old),
                    RemarkKey = null,
                    TypeId = _item.GetTypeId(old),
                    Quality = _item.GetQuality(old),
                    DisplayQuality = _item.GetDisplayQuality(old),
                    Value = _item.GetValue(old),
                    OwnerId = null
                };
            }
            catch { return meta; }
        }

        /// <summary>将任意包装对象解包为 Item 组件。</summary>
        private static Item UnwrapToItem(object obj)
        {
            if (obj is Item it) return it;
            try
            {
                if (obj is Component c) return c.GetComponent<Item>();
                if (obj is GameObject go) return go.GetComponent<Item>();
                var t = obj?.GetType(); if (t == null) return null;
                var p = t.GetProperty("Item", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (p != null) { var v = p.GetValue(obj, null) as Item; if (v) return v; }
                var f = t.GetField("item", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null) { var v = f.GetValue(obj) as Item; if (v) return v; }
            }
            catch { }
            return null;
        }

        private static void TrySet(object obj, string prop, object val) { try { obj.GetType().GetProperty(prop, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(obj, val, null); } catch { } }
        /// <summary>将物品发送给玩家（根据可用重载匹配调用）。</summary>
        private static void SendToPlayer(Item item)
        {
            try
            {
                var util = FindType("TeamSoda.Duckov.Core.ItemUtilities") ?? FindType("ItemUtilities");
                var m = util?.GetMethod(EngineKeys.Method.SendToPlayer, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null, new[] { typeof(Item), typeof(bool), typeof(bool) }, null)
                    ?? util?.GetMethod(EngineKeys.Method.SendToPlayer, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null, new[] { typeof(Item) }, null);
                if (m == null) return; var ps = m.GetParameters(); if (ps.Length == 3) m.Invoke(null, new object[] { item, false, true }); else m.Invoke(null, new object[] { item });
            }
            catch { }
        }
        /// <summary>刷新主角背包与仓库的 UI。</summary>
        private static void TryRefreshInventories()
        {
            try
            {
                var cmcT = FindType("CharacterMainControl") ?? FindType("TeamSoda.Duckov.Core.CharacterMainControl");
                var main = cmcT?.GetProperty(EngineKeys.Property.Main, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null, null);
                var inv = main?.GetType().GetProperty(EngineKeys.Property.CharacterItem)?.GetValue(main, null)?.GetType().GetProperty(EngineKeys.Property.Inventory)?.GetValue(main.GetType().GetProperty(EngineKeys.Property.CharacterItem)?.GetValue(main, null), null);
                if (inv != null)
                {
                    try { var p = inv.GetType().GetProperty(EngineKeys.Property.NeedInspection); p?.SetValue(inv, true, null); } catch { }
                    try { var m = inv.GetType().GetMethod(EngineKeys.Method.Refresh); m?.Invoke(inv, null); } catch { }
                }
                var psT = FindType("TeamSoda.Duckov.Core.PlayerStorage") ?? FindType("PlayerStorage");
                var st = psT?.GetProperty(EngineKeys.Property.Inventory, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null, null);
                if (st != null)
                {
                    try { var p = st.GetType().GetProperty(EngineKeys.Property.NeedInspection); p?.SetValue(st, true, null); } catch { }
                    try { var m = st.GetType().GetMethod(EngineKeys.Method.Refresh); m?.Invoke(st, null); } catch { }
                }
            }
            catch { }
        }
    }
}
