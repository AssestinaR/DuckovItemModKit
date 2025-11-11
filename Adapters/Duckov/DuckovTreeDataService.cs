using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    // TreeData 风味的导出/基础导入/完整克隆到背包（临时实验版，可随时移除或扩展）
    public static class DuckovTreeDataService
    {
        private static bool s_inited;
        private static Type tTree;              // ItemStatsSystem.Data.ItemTreeData
        private static Type tDataEntry;         // nested DataEntry
        private static Type tCustomData;        // Duckov.Utilities.CustomData
        private static MethodInfo miFromItem;   // static FromItem(Item)
        private static MethodInfo miInstantiateAsync; // static InstantiateAsync(ItemTreeData)
        private static PropertyInfo piRootTypeId;
        private static PropertyInfo piRootData;
        private static FieldInfo fiEntries;
        private static FieldInfo fiDE_InstanceID;
        private static FieldInfo fiDE_TypeID;
        private static FieldInfo fiDE_Variables;
        private static FieldInfo fiDE_SlotContents;
        private static FieldInfo fiDE_Inventory;
        private static FieldInfo fiDE_InventorySortLocks;
        private static PropertyInfo piCD_Key;
        private static PropertyInfo piCD_DataType;
        private static PropertyInfo piCD_DisplayName;
        private static PropertyInfo piCD_Display;
        private static MethodInfo miCD_GetInt;
        private static MethodInfo miCD_GetFloat;
        private static MethodInfo miCD_GetBool;
        private static MethodInfo miCD_GetString;
        private static MethodInfo miCD_GetRawCopied;

        private static void Ensure()
        {
            if (s_inited) return; s_inited = true;
            try
            {
                tTree = DuckovTypeUtils.FindType("ItemStatsSystem.Data.ItemTreeData");
                if (tTree == null) return;
                miFromItem = tTree.GetMethod("FromItem", BindingFlags.Public|BindingFlags.Static);
                miInstantiateAsync = tTree.GetMethod("InstantiateAsync", BindingFlags.Public|BindingFlags.Static);
                fiEntries = tTree.GetField("entries", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                piRootTypeId = tTree.GetProperty("RootTypeID", BindingFlags.Public|BindingFlags.Instance);
                piRootData = tTree.GetProperty("RootData", BindingFlags.Public|BindingFlags.Instance);
                tDataEntry = DuckovTypeUtils.FindType("ItemStatsSystem.Data.ItemTreeData+DataEntry");
                if (tDataEntry != null)
                {
                    fiDE_InstanceID = tDataEntry.GetField("instanceID", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                    fiDE_TypeID = tDataEntry.GetField("typeID", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                    fiDE_Variables = tDataEntry.GetField("variables", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                    fiDE_SlotContents = tDataEntry.GetField("slotContents", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                    fiDE_Inventory = tDataEntry.GetField("inventory", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                    fiDE_InventorySortLocks = tDataEntry.GetField("inventorySortLocks", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                }
                tCustomData = DuckovTypeUtils.FindType("Duckov.Utilities.CustomData");
                if (tCustomData != null)
                {
                    piCD_Key = tCustomData.GetProperty("Key", BindingFlags.Public|BindingFlags.Instance);
                    piCD_DataType = tCustomData.GetProperty("DataType", BindingFlags.Public|BindingFlags.Instance);
                    piCD_DisplayName = tCustomData.GetProperty("DisplayName", BindingFlags.Public|BindingFlags.Instance);
                    piCD_Display = tCustomData.GetProperty("Display", BindingFlags.Public|BindingFlags.Instance);
                    miCD_GetInt = tCustomData.GetMethod("GetInt", BindingFlags.Public|BindingFlags.Instance);
                    miCD_GetFloat = tCustomData.GetMethod("GetFloat", BindingFlags.Public|BindingFlags.Instance);
                    miCD_GetBool = tCustomData.GetMethod("GetBool", BindingFlags.Public|BindingFlags.Instance);
                    miCD_GetString = tCustomData.GetMethod("GetString", BindingFlags.Public|BindingFlags.Instance);
                    miCD_GetRawCopied = tCustomData.GetMethod("GetRawCopied", BindingFlags.Public|BindingFlags.Instance);
                }
            }
            catch { }
        }

        public static RichResult<object> TryExport(object item, int maxEntries = 512)
        {
            try
            {
                Ensure();
                if (tTree == null || miFromItem == null || fiEntries == null)
                    return RichResult<object>.Fail(ErrorCode.DependencyMissing, "TreeData API missing");
                if (item == null) return RichResult<object>.Fail(ErrorCode.InvalidArgument, "item null");
                var tree = miFromItem.Invoke(null, new[] { item });
                if (tree == null) return RichResult<object>.Fail(ErrorCode.OperationFailed, "FromItem null");
                var rootTypeId = SafeGet(piRootTypeId, tree) ?? 0;
                var entriesObj = fiEntries.GetValue(tree) as System.Collections.IEnumerable;
                var outEntries = new List<object>();
                if (entriesObj != null)
                {
                    int count = 0;
                    foreach (var e in entriesObj)
                    {
                        if (e == null) continue;
                        if (++count > maxEntries) return RichResult<object>.Fail(ErrorCode.OperationFailed, "entries limit");
                        outEntries.Add(ExportEntry(e));
                    }
                }
                var root = new Dictionary<string, object>
                {
                    ["version"] = 1,
                    ["rootTypeId"] = rootTypeId,
                    ["entries"] = outEntries
                };
                return RichResult<object>.Success(root);
            }
            catch (Exception ex)
            {
                Log.Error("TreeData export failed", ex);
                return RichResult<object>.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        private static object ExportEntry(object entry)
        {
            var map = new Dictionary<string, object>();
            try { map["instanceId"] = fiDE_InstanceID?.GetValue(entry); } catch { }
            try { map["typeId"] = fiDE_TypeID?.GetValue(entry); } catch { }
            // variables
            var varsList = new List<object>();
            try
            {
                var vars = fiDE_Variables?.GetValue(entry) as System.Collections.IEnumerable;
                if (vars != null)
                {
                    foreach (var v in vars)
                    {
                        var vm = ExportVariable(v);
                        if (vm != null) varsList.Add(vm);
                    }
                }
            }
            catch { }
            map["vars"] = varsList;
            // slot contents
            var slotsArr = new List<object>();
            try
            {
                var sc = fiDE_SlotContents?.GetValue(entry) as System.Collections.IEnumerable;
                if (sc != null)
                {
                    foreach (var s in sc)
                    {
                        if (s == null) continue;
                        var t = s.GetType();
                        var slot = SafeFieldOrProp<string>(t, s, "slot");
                        var iid = SafeFieldOrProp<int?>(t, s, "instanceID");
                        var obj = new Dictionary<string, object>();
                        if (slot != null) obj["slot"] = slot;
                        if (iid.HasValue) obj["instanceId"] = iid.Value;
                        slotsArr.Add(obj);
                    }
                }
            }
            catch { }
            map["slots"] = slotsArr;
            // inventory entries
            var invArr = new List<object>();
            try
            {
                var inv = fiDE_Inventory?.GetValue(entry) as System.Collections.IEnumerable;
                if (inv != null)
                {
                    foreach (var it in inv)
                    {
                        if (it == null) continue;
                        var t = it.GetType();
                        var pos = SafeFieldOrProp<int?>(t, it, "position");
                        var iid = SafeFieldOrProp<int?>(t, it, "instanceID");
                        var obj = new Dictionary<string, object>();
                        if (pos.HasValue) obj["position"] = pos.Value;
                        if (iid.HasValue) obj["instanceId"] = iid.Value;
                        invArr.Add(obj);
                    }
                }
            }
            catch { }
            map["inventory"] = invArr;
            // sort locks (int list)
            var sortLocks = new List<int>();
            try
            {
                var sl = fiDE_InventorySortLocks?.GetValue(entry) as System.Collections.IEnumerable;
                if (sl != null)
                {
                    foreach (var x in sl)
                    {
                        try { if (x is int i) sortLocks.Add(i); } catch { }
                    }
                }
            }
            catch { }
            map["inventorySortLocks"] = sortLocks;
            return map;
        }

        private static object ExportVariable(object cd)
        {
            if (cd == null || tCustomData == null) return null;
            try
            {
                var key = piCD_Key?.GetValue(cd, null) as string;
                var dt = piCD_DataType?.GetValue(cd, null);
                var displayName = piCD_DisplayName?.GetValue(cd, null) as string;
                var display = piCD_Display?.GetValue(cd, null) as bool?;
                var entry = new Dictionary<string, object>();
                if (key != null) entry["k"] = key;
                if (dt != null) entry["type"] = dt.ToString();
                if (!string.IsNullOrEmpty(displayName)) entry["dn"] = displayName;
                if (display == true) entry["disp"] = true;
                // heuristic to retrieve value
                object val = null;
                try { val = miCD_GetString?.Invoke(cd, null); if (val is string && !string.IsNullOrEmpty((string)val)) { entry["v"] = val; return entry; } } catch { }
                try { val = miCD_GetInt?.Invoke(cd, null); if (val is int) { entry["v"] = val; return entry; } } catch { }
                try { val = miCD_GetFloat?.Invoke(cd, null); if (val is float f) { entry["v"] = f; return entry; } } catch { }
                try { val = miCD_GetBool?.Invoke(cd, null); if (val is bool b) { entry["v"] = b; return entry; } } catch { }
                // raw fallback
                try
                {
                    var raw = miCD_GetRawCopied?.Invoke(cd, null) as byte[];
                    if (raw != null && raw.Length > 0)
                        entry["raw"] = Convert.ToBase64String(raw);
                }
                catch { }
                return entry;
            }
            catch { return null; }
        }

        private static T SafeFieldOrProp<T>(Type t, object obj, string name)
        {
            try
            {
                var f = t.GetField(name, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (f != null)
                {
                    var v = f.GetValue(obj);
                    if (v is T tv) return tv;
                    if (v == null) return default;
                    try { return (T)Convert.ChangeType(v, typeof(T)); } catch { return default; }
                }
                var p = t.GetProperty(name, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (p != null && p.CanRead && p.GetIndexParameters().Length==0)
                {
                    var v = p.GetValue(obj, null);
                    if (v is T tv) return tv;
                    if (v == null) return default;
                    try { return (T)Convert.ChangeType(v, typeof(T)); } catch { return default; }
                }
            }
            catch { }
            return default;
        }
        private static object SafeGet(PropertyInfo p, object obj)
        { try { return p?.GetValue(obj, null); } catch { return null; } }

        // 基础导入（实验版）：仅创建 rootTypeId 的空物品并写入变量（忽略子节点结构）
        public static RichResult<object> TryImportMinimal(JObject exportData)
        {
            try
            {
                if (exportData == null) return RichResult<object>.Fail(ErrorCode.InvalidArgument, "json null");
                var rootTypeId = exportData.Value<int?>("rootTypeId") ?? 0;
                if (rootTypeId <= 0) return RichResult<object>.Fail(ErrorCode.InvalidArgument, "rootTypeId invalid");
                object newItem = null;
                try
                {
                    var gen = IMKDuckov.Factory.TryGenerateByTypeId(rootTypeId);
                    if (gen.Ok) newItem = gen.Value;
                }
                catch { }
                if (newItem == null)
                {
                    var inst = IMKDuckov.Factory.TryInstantiateByTypeId(rootTypeId);
                    if (inst.Ok) newItem = inst.Value;
                }
                if (newItem == null) return RichResult<object>.Fail(ErrorCode.OperationFailed, "create item failed");
                var entries = exportData["entries"] as JArray;
                if (entries != null && entries.Count > 0)
                {
                    var first = entries[0] as JObject;
                    var vars = first?["vars"] as JArray;
                    if (vars != null && vars.Count > 0)
                    {
                        var kvList = new List<KeyValuePair<string, object>>();
                        foreach (var v in vars)
                        {
                            if (v is JObject o)
                            {
                                var k = o.Value<string>("k");
                                if (string.IsNullOrEmpty(k)) continue;
                                if (o.TryGetValue("v", out var valToken))
                                {
                                    kvList.Add(new KeyValuePair<string, object>(k, ((JValue)valToken).Value));
                                }
                                else if (o.TryGetValue("raw", out var rawToken))
                                {
                                    // raw 暂不支持直接写入
                                }
                            }
                        }
                        if (kvList.Count > 0)
                        {
                            try { IMKDuckov.Write.TryWriteVariables(newItem, kvList, overwrite: true); } catch { }
                        }
                    }
                }
                return RichResult<object>.Success(newItem);
            }
            catch (Exception ex)
            {
                Log.Error("TreeData import failed", ex);
                return RichResult<object>.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        // 完整导入：直接用 ItemTreeData.InstantiateAsync 重建整棵树（不进行 JSON 重建，源自现有 item）
        public static RichResult<object> TryCloneFromSource(object source)
        {
            try
            {
                Ensure();
                if (tTree == null || miFromItem == null || miInstantiateAsync == null)
                    return RichResult<object>.Fail(ErrorCode.DependencyMissing, "TreeData API missing");
                if (source == null) return RichResult<object>.Fail(ErrorCode.InvalidArgument, "source null");
                var tree = miFromItem.Invoke(null, new[] { source });
                if (tree == null) return RichResult<object>.Fail(ErrorCode.OperationFailed, "FromItem null");
                var task = miInstantiateAsync.Invoke(null, new[] { tree });
                var newItem = AwaitUniTask(task);
                return (newItem != null) ? RichResult<object>.Success(newItem) : RichResult<object>.Fail(ErrorCode.OperationFailed, "InstantiateAsync null");
            }
            catch (Exception ex) { Log.Error("TryCloneFromSource failed", ex); return RichResult<object>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        // 克隆并放入玩家背包
        public static RichResult<object> TryCloneIntoPlayerInventory(object source)
        {
            var clone = TryCloneFromSource(source);
            if (!clone.Ok) return clone;
            try
            {
                var inv = ResolvePlayerInventory();
                if (inv == null) return RichResult<object>.Fail(ErrorCode.NotFound, "player inventory not found");
                var ok = IMKDuckov.Inventory.AddAndMerge(inv, clone.Value);
                if (!ok) return RichResult<object>.Fail(ErrorCode.OperationFailed, "add to inventory failed");
                try { IMKDuckov.PublishItemAdded(clone.Value, new ItemEventContext { Source = ItemEventSourceType.Backpack, Cause = ItemEventCause.Loot }); } catch { }
                return clone;
            }
            catch (Exception ex) { Log.Error("TryCloneIntoPlayerInventory failed", ex); return RichResult<object>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        // 高级克隆（过渡保留）：改为转发到总线化管线
        [Obsolete("Use IMKDuckov.Clone.TryCloneToInventory (bus-oriented pipeline).", false)]
        public static RichResult<object> TryCloneIntoInventoryAdvanced(object source, string target = null)
        {
            var opts = new ClonePipelineOptions
            {
                Strategy = CloneStrategy.TreeData,
                VariableMerge = VariableMergeMode.OnlyMissing,
                CopyTags = true,
                Target = string.IsNullOrEmpty(target) ? "character" : target,
                RefreshUI = true,
                Diagnostics = false
            };
            var r = IMKDuckov.Clone.TryCloneToInventory(source, opts);
            if (!r.Ok || r.Value == null) return RichResult<object>.Fail(r.Code, r.Error);
            return RichResult<object>.Success(r.Value.NewItem);
        }

        // 高级克隆+诊断（过渡保留）：转发到总线化管线并返回诊断
        [Obsolete("Use IMKDuckov.Clone.TryCloneToInventory (bus-oriented pipeline).", false)]
        public static (RichResult<object> result, Dictionary<string, object> diag) TryCloneIntoInventoryAdvancedWithDiag(object source, string target = null, int sampleLimit = 32)
        {
            var opts = new ClonePipelineOptions
            {
                Strategy = CloneStrategy.TreeData,
                VariableMerge = VariableMergeMode.OnlyMissing,
                CopyTags = true,
                Target = string.IsNullOrEmpty(target) ? "character" : target,
                RefreshUI = true,
                Diagnostics = true
            };
            var r = IMKDuckov.Clone.TryCloneToInventory(source, opts);
            var diag = r.Ok && r.Value != null ? (r.Value.Diagnostics ?? new Dictionary<string, object>()) : new Dictionary<string, object>();
            diag["strategyRequested"] = "TreeData";
            diag["targetRequested"] = opts.Target;
            if (!r.Ok) diag["error"] = r.Error ?? "clone failed";
            var res = r.Ok && r.Value != null ? RichResult<object>.Success(r.Value.NewItem) : RichResult<object>.Fail(r.Code, r.Error);
            return (res, diag);
        }

        // helpers restored after refactor
        private static object AwaitUniTask(object uniTask)
        {
            if (uniTask == null) return null;
            try
            {
                var getAwaiter = uniTask.GetType().GetMethod("GetAwaiter", BindingFlags.Public|BindingFlags.Instance);
                var awaiter = getAwaiter?.Invoke(uniTask, null);
                var isCompletedProp = awaiter?.GetType().GetProperty("IsCompleted", BindingFlags.Public|BindingFlags.Instance);
                int spins = 0;
                while (isCompletedProp != null && !(bool)(isCompletedProp.GetValue(awaiter, null) ?? false) && spins++ < 200)
                {
                    try { System.Threading.Thread.Sleep(1); } catch { }
                }
                var getResult = awaiter?.GetType().GetMethod("GetResult", BindingFlags.Public|BindingFlags.Instance);
                return getResult?.Invoke(awaiter, null);
            }
            catch { return null; }
        }

        private static object ResolvePlayerInventory()
        {
            try
            {
                var tPS = DuckovTypeUtils.FindType("TeamSoda.Duckov.Core.PlayerStorage") ?? DuckovTypeUtils.FindType("PlayerStorage");
                var pInv = tPS?.GetProperty("Inventory", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
                var inv = pInv?.GetValue(null, null);
                if (inv != null) return inv;
            }
            catch { }
            try
            {
                var tLM = DuckovTypeUtils.FindType("TeamSoda.Duckov.Core.LevelManager") ?? DuckovTypeUtils.FindType("LevelManager");
                var pInst = tLM?.GetProperty("Instance", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
                var lm = pInst?.GetValue(null, null);
                if (lm != null)
                {
                    var pMain = lm.GetType().GetProperty("MainCharacter", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                    var main = pMain?.GetValue(lm, null);
                    if (main != null)
                    {
                        var pCharItem = main.GetType().GetProperty("CharacterItem", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                        var chItem = pCharItem?.GetValue(main, null);
                        if (chItem != null)
                        {
                            var pInv = chItem.GetType().GetProperty("Inventory", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                            var inv = pInv?.GetValue(chItem, null);
                            if (inv != null) return inv;
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
