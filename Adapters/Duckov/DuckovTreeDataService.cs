using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// TreeData 工具：提供 Duckov ItemTreeData 的导出、最小导入与基于引擎 API 的整树克隆能力。
    /// 注意：为实验性特性，未来可能调整或移除。
    /// </summary>
    public static class DuckovTreeDataService
    {
        private sealed class ImportedTreeEntry
        {
            public int InstanceId { get; set; }
            public int TypeId { get; set; }
            public List<KeyValuePair<string, object>> Variables { get; } = new List<KeyValuePair<string, object>>();
            public List<KeyValuePair<string, int>> Slots { get; } = new List<KeyValuePair<string, int>>();
            public List<KeyValuePair<int, int>> Inventory { get; } = new List<KeyValuePair<int, int>>();
        }

        internal sealed class TreeImportDiagnostics
        {
            public string ImportMode { get; set; } = "minimal";
            public bool FallbackUsed { get; set; }
            public string FallbackStage { get; set; }
            public string FallbackReason { get; set; }
            public int EntriesRequested { get; set; }
            public int EntriesImported { get; set; }
        }

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
                miFromItem = tTree.GetMethod("FromItem", BindingFlags.Public | BindingFlags.Static);
                miInstantiateAsync = tTree.GetMethod("InstantiateAsync", BindingFlags.Public | BindingFlags.Static);
                fiEntries = tTree.GetField("entries", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                piRootTypeId = tTree.GetProperty("RootTypeID", BindingFlags.Public | BindingFlags.Instance);
                piRootData = tTree.GetProperty("RootData", BindingFlags.Public | BindingFlags.Instance);
                tDataEntry = DuckovTypeUtils.FindType("ItemStatsSystem.Data.ItemTreeData+DataEntry");
                if (tDataEntry != null)
                {
                    fiDE_InstanceID = tDataEntry.GetField("instanceID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    fiDE_TypeID = tDataEntry.GetField("typeID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    fiDE_Variables = tDataEntry.GetField("variables", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    fiDE_SlotContents = tDataEntry.GetField("slotContents", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    fiDE_Inventory = tDataEntry.GetField("inventory", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    fiDE_InventorySortLocks = tDataEntry.GetField("inventorySortLocks", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                tCustomData = DuckovTypeUtils.FindType("Duckov.Utilities.CustomData");
                if (tCustomData != null)
                {
                    piCD_Key = tCustomData.GetProperty("Key", BindingFlags.Public | BindingFlags.Instance);
                    piCD_DataType = tCustomData.GetProperty("DataType", BindingFlags.Public | BindingFlags.Instance);
                    piCD_DisplayName = tCustomData.GetProperty("DisplayName", BindingFlags.Public | BindingFlags.Instance);
                    piCD_Display = tCustomData.GetProperty("Display", BindingFlags.Public | BindingFlags.Instance);
                    miCD_GetInt = tCustomData.GetMethod("GetInt", BindingFlags.Public | BindingFlags.Instance);
                    miCD_GetFloat = tCustomData.GetMethod("GetFloat", BindingFlags.Public | BindingFlags.Instance);
                    miCD_GetBool = tCustomData.GetMethod("GetBool", BindingFlags.Public | BindingFlags.Instance);
                    miCD_GetString = tCustomData.GetMethod("GetString", BindingFlags.Public | BindingFlags.Instance);
                    miCD_GetRawCopied = tCustomData.GetMethod("GetRawCopied", BindingFlags.Public | BindingFlags.Instance);
                }
            }
            catch { }
        }

        /// <summary>
        /// 导出指定物品的 TreeData 表示。
        /// </summary>
        /// <param name="item">源物品。</param>
        /// <param name="maxEntries">最大导出条目数上限，超出返回失败。</param>
        /// <returns>包含 version/rootTypeId/entries 的对象映射。</returns>
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
                var rootData = SafeGet(piRootData, tree);
                var rootInstanceId = 0;
                try
                {
                    rootInstanceId = rootData != null && fiDE_InstanceID != null
                        ? Convert.ToInt32(fiDE_InstanceID.GetValue(rootData))
                        : 0;
                }
                catch { rootInstanceId = 0; }
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
                    ["rootInstanceId"] = rootInstanceId,
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
                var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null)
                {
                    var v = f.GetValue(obj);
                    if (v is T tv) return tv;
                    if (v == null) return default;
                    try { return (T)Convert.ChangeType(v, typeof(T)); } catch { return default; }
                }
                var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.CanRead && p.GetIndexParameters().Length == 0)
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

        /// <summary>
        /// 最小导入：仅创建根类型并写入首条条目的变量，忽略层级结构。
        /// </summary>
        /// <param name="exportData">由 TryExport 产出的 JSON 对象。</param>
        /// <returns>成功包含新物品实例。</returns>
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
                        var values = new List<KeyValuePair<string, object>>();
                        foreach (var varToken in vars)
                        {
                            if (!(varToken is JObject variableObject)) continue;
                            var key = variableObject.Value<string>("k");
                            if (string.IsNullOrEmpty(key)) continue;
                            if (variableObject.TryGetValue("v", out var valueToken))
                            {
                                values.Add(new KeyValuePair<string, object>(key, ConvertJTokenValue(valueToken)));
                            }
                        }

                        if (values.Count > 0)
                        {
                            try { IMKDuckov.Write.TryWriteVariables(newItem, values, overwrite: true); } catch { }
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

        internal static RichResult<object> TryImportTree(JObject exportData, out TreeImportDiagnostics diagnostics)
        {
            diagnostics = new TreeImportDiagnostics();
            try
            {
                if (exportData == null) return RichResult<object>.Fail(ErrorCode.InvalidArgument, "json null");
                var rootTypeId = exportData.Value<int?>("rootTypeId") ?? 0;
                if (rootTypeId <= 0) return RichResult<object>.Fail(ErrorCode.InvalidArgument, "rootTypeId invalid");

                var parsed = ParseImportedEntries(exportData);
                diagnostics.EntriesRequested = parsed?.Count ?? 0;
                if (parsed == null || parsed.Count == 0)
                {
                    return FallbackToMinimal(exportData, diagnostics, "parse", "entries missing or invalid");
                }

                var instanceMap = new Dictionary<int, object>();
                for (int i = 0; i < parsed.Count; i++)
                {
                    var entry = parsed[i];
                    if (entry == null || entry.TypeId <= 0 || entry.InstanceId == 0)
                    {
                        return FallbackToMinimal(exportData, diagnostics, "parse", "entry typeId/instanceId invalid");
                    }

                    object created = null;
                    try
                    {
                        var gen = IMKDuckov.Factory.TryGenerateByTypeId(entry.TypeId);
                        if (gen.Ok) created = gen.Value;
                    }
                    catch { }

                    if (created == null)
                    {
                        var inst = IMKDuckov.Factory.TryInstantiateByTypeId(entry.TypeId);
                        if (inst.Ok) created = inst.Value;
                    }

                    if (created == null)
                    {
                        return FallbackToMinimal(exportData, diagnostics, "instantiate", "node creation failed for typeId=" + entry.TypeId);
                    }

                    instanceMap[entry.InstanceId] = created;
                }

                diagnostics.EntriesImported = instanceMap.Count;

                foreach (var entry in parsed)
                {
                    if (!instanceMap.TryGetValue(entry.InstanceId, out var item) || item == null) continue;
                    if (entry.Variables.Count <= 0) continue;

                    var write = IMKDuckov.Write.TryWriteVariables(item, entry.Variables, overwrite: true);
                    if (!write.Ok)
                    {
                        return FallbackToMinimal(exportData, diagnostics, "hydrate", write.Error ?? "variable write failed");
                    }
                }

                foreach (var entry in parsed)
                {
                    if (!instanceMap.TryGetValue(entry.InstanceId, out var owner) || owner == null) continue;

                    if (entry.Inventory.Count > 0)
                    {
                        var inventory = IMKDuckov.Inventory.GetInventory(owner);
                        if (inventory == null)
                        {
                            return FallbackToMinimal(exportData, diagnostics, "connect.inventory", "inventory missing for owner instanceId=" + entry.InstanceId);
                        }

                        entry.Inventory.Sort((left, right) => left.Key.CompareTo(right.Key));
                        foreach (var childRef in entry.Inventory)
                        {
                            if (!instanceMap.TryGetValue(childRef.Value, out var child) || child == null)
                            {
                                return FallbackToMinimal(exportData, diagnostics, "connect.inventory", "inventory child missing instanceId=" + childRef.Value);
                            }

                            if (!IMKDuckov.Inventory.AddAt(inventory, child, childRef.Key))
                            {
                                return FallbackToMinimal(exportData, diagnostics, "connect.inventory", "inventory add failed at index=" + childRef.Key);
                            }
                        }
                    }

                    if (entry.Slots.Count > 0)
                    {
                        foreach (var childRef in entry.Slots)
                        {
                            if (string.IsNullOrEmpty(childRef.Key)) continue;
                            if (!instanceMap.TryGetValue(childRef.Value, out var child) || child == null)
                            {
                                return FallbackToMinimal(exportData, diagnostics, "connect.slot", "slot child missing instanceId=" + childRef.Value);
                            }

                            var plug = IMKDuckov.Write.TryPlugIntoSlot(owner, childRef.Key, child);
                            if (!plug.Ok)
                            {
                                return FallbackToMinimal(exportData, diagnostics, "connect.slot", plug.Error ?? "slot plug failed: " + childRef.Key);
                            }
                        }
                    }
                }

                diagnostics.ImportMode = "tree";
                diagnostics.FallbackUsed = false;
                diagnostics.FallbackStage = string.Empty;
                diagnostics.FallbackReason = string.Empty;
                var rootInstanceId = exportData.Value<int?>("rootInstanceId") ?? parsed[0].InstanceId;
                var rootEntry = parsed.Find(e => e.InstanceId == rootInstanceId) ?? parsed[0];
                return instanceMap.TryGetValue(rootEntry.InstanceId, out var root) && root != null
                    ? RichResult<object>.Success(root)
                    : FallbackToMinimal(exportData, diagnostics, "finalize", "root resolve failed");
            }
            catch (Exception ex)
            {
                Log.Error("TreeData tree import failed", ex);
                return FallbackToMinimal(exportData, diagnostics, "exception", ex.Message);
            }
        }

        private static RichResult<object> FallbackToMinimal(JObject exportData, TreeImportDiagnostics diagnostics, string stage, string reason)
        {
            if (diagnostics != null)
            {
                diagnostics.ImportMode = "minimal";
                diagnostics.FallbackUsed = true;
                diagnostics.FallbackStage = stage ?? string.Empty;
                diagnostics.FallbackReason = reason ?? string.Empty;
            }

            return TryImportMinimal(exportData);
        }

        /// <summary>
        /// 基于引擎的 ItemTreeData.InstantiateAsync 重建整棵子树。
        /// </summary>
        /// <param name="source">源物品。</param>
        /// <returns>成功返回新物品。</returns>
        public static RichResult<object> TryCloneFromSource(object source)
        {
            var request = new RestoreRequest
            {
                Source = source,
                SourceKind = RestoreSourceKind.VanillaTreeData,
                Target = null,
                TargetMode = RestoreTargetMode.DetachedTree,
                Strategy = CloneStrategy.TreeData,
                VariableMergeMode = VariableMergeMode.None,
                CopyTags = false,
                AllowDegraded = false,
                PublishEvents = false,
                RefreshUI = false,
                MarkDirty = false,
                DiagnosticsEnabled = false,
                CallerTag = "treedata.clone",
            };

            var restore = DuckovTreeRestoreOrchestrator.Shared.Execute(request);
            if (!restore.Ok || restore.Value == null) return RichResult<object>.Fail(restore.Code, restore.Error);
            return restore.Value.RootItem != null
                ? RichResult<object>.Success(restore.Value.RootItem)
                : RichResult<object>.Fail(ErrorCode.OperationFailed, "restore root null");
        }

        internal static RichResult<object> TryInstantiateTreeFromSource(object source)
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
            catch (Exception ex) { Log.Error("TryInstantiateTreeFromSource failed", ex); return RichResult<object>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        private static List<ImportedTreeEntry> ParseImportedEntries(JObject exportData)
        {
            var entries = exportData?["entries"] as JArray;
            if (entries == null || entries.Count == 0) return null;

            var result = new List<ImportedTreeEntry>();
            foreach (var token in entries)
            {
                if (!(token is JObject entryObject)) continue;
                var entry = new ImportedTreeEntry
                {
                    InstanceId = entryObject.Value<int?>("instanceId") ?? 0,
                    TypeId = entryObject.Value<int?>("typeId") ?? 0,
                };

                if (entry.InstanceId == 0 || entry.TypeId <= 0) return null;

                if (entryObject["vars"] is JArray vars)
                {
                    foreach (var varToken in vars)
                    {
                        if (!(varToken is JObject varObject)) continue;
                        var key = varObject.Value<string>("k");
                        if (string.IsNullOrEmpty(key)) continue;
                        if (varObject.TryGetValue("v", out var valueToken))
                        {
                            entry.Variables.Add(new KeyValuePair<string, object>(key, ConvertJTokenValue(valueToken)));
                        }
                    }
                }

                if (entryObject["slots"] is JArray slots)
                {
                    foreach (var slotToken in slots)
                    {
                        if (!(slotToken is JObject slotObject)) continue;
                        var slotKey = slotObject.Value<string>("slot");
                        var childInstanceId = slotObject.Value<int?>("instanceId") ?? 0;
                        if (!string.IsNullOrEmpty(slotKey) && childInstanceId != 0)
                        {
                            entry.Slots.Add(new KeyValuePair<string, int>(slotKey, childInstanceId));
                        }
                    }
                }

                if (entryObject["inventory"] is JArray inventory)
                {
                    foreach (var inventoryToken in inventory)
                    {
                        if (!(inventoryToken is JObject inventoryObject)) continue;
                        var position = inventoryObject.Value<int?>("position") ?? -1;
                        var childInstanceId = inventoryObject.Value<int?>("instanceId") ?? 0;
                        if (position >= 0 && childInstanceId != 0)
                        {
                            entry.Inventory.Add(new KeyValuePair<int, int>(position, childInstanceId));
                        }
                    }
                }

                result.Add(entry);
            }

            return result.Count > 0 ? result : null;
        }

        private static object ConvertJTokenValue(JToken token)
        {
            if (token == null) return null;
            if (token is JValue value) return value.Value;
            return token.ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <summary>
        /// 克隆并放入玩家背包。
        /// </summary>
        /// <param name="source">源物品。</param>
        /// <returns>成功返回新物品；失败携带错误码。</returns>
        [Obsolete("Use IMKDuckov.Clone.TryCloneToInventory targeting character.", false)]
        public static RichResult<object> TryCloneIntoPlayerInventory(object source)
        {
            return ForwardCompatTreeClone(source, "character", diagnostics: false);
        }

        /// <summary>
        /// 高级克隆（过渡保留）：改为转发到总线化克隆管线。
        /// </summary>
        /// <param name="source">源物品。</param>
        /// <param name="target">目标标识（character/storage/null）。</param>
        /// <returns>成功返回新物品。</returns>
        [Obsolete("Use IMKDuckov.Clone.TryCloneToInventory (bus-oriented pipeline).", false)]
        public static RichResult<object> TryCloneIntoInventoryAdvanced(object source, string target = null)
        {
            return ForwardCompatTreeClone(source, target, diagnostics: false);
        }

        /// <summary>
        /// 高级克隆 + 诊断（过渡保留）：转发到总线化管线并附带诊断数据。
        /// </summary>
        /// <param name="source">源物品。</param>
        /// <param name="target">目标标识。</param>
        /// <param name="sampleLimit">样本限制（保留字段）。</param>
        /// <returns>返回 (结果, 诊断字典)。</returns>
        [Obsolete("Use IMKDuckov.Clone.TryCloneToInventory (bus-oriented pipeline).", false)]
        public static (RichResult<object> result, Dictionary<string, object> diag) TryCloneIntoInventoryAdvancedWithDiag(object source, string target = null, int sampleLimit = 32)
        {
            var opts = CreateCompatTreeCloneOptions(target, diagnostics: true);
            var r = IMKDuckov.Clone.TryCloneToInventory(source, opts);
            var diag = r.Ok && r.Value != null ? (r.Value.Diagnostics ?? new Dictionary<string, object>()) : new Dictionary<string, object>();
            diag["strategyRequested"] = "TreeData";
            diag["targetRequested"] = opts.Target;
            diag["sampleLimit"] = sampleLimit;
            if (!r.Ok) diag["error"] = r.Error ?? "clone failed";
            return (ToCompatCloneResult(r), diag);
        }

        private static ClonePipelineOptions CreateCompatTreeCloneOptions(string target, bool diagnostics)
        {
            return new ClonePipelineOptions
            {
                Strategy = CloneStrategy.TreeData,
                VariableMerge = VariableMergeMode.OnlyMissing,
                CopyTags = true,
                Target = string.IsNullOrEmpty(target) ? "character" : target,
                RefreshUI = true,
                Diagnostics = diagnostics
            };
        }

        private static RichResult<object> ForwardCompatTreeClone(object source, string target, bool diagnostics)
        {
            var opts = CreateCompatTreeCloneOptions(target, diagnostics);
            var r = IMKDuckov.Clone.TryCloneToInventory(source, opts);
            return ToCompatCloneResult(r);
        }

        private static RichResult<object> ToCompatCloneResult(RichResult<ClonePipelineResult> result)
        {
            if (!result.Ok || result.Value == null) return RichResult<object>.Fail(result.Code, result.Error);
            return RichResult<object>.Success(result.Value.NewItem);
        }

        // helpers restored after refactor
        private static object AwaitUniTask(object uniTask)
        {
            if (uniTask == null) return null;
            try
            {
                var getAwaiter = uniTask.GetType().GetMethod("GetAwaiter", BindingFlags.Public | BindingFlags.Instance);
                var awaiter = getAwaiter?.Invoke(uniTask, null);
                var isCompletedProp = awaiter?.GetType().GetProperty("IsCompleted", BindingFlags.Public | BindingFlags.Instance);
                int spins = 0;
                while (isCompletedProp != null && !(bool)(isCompletedProp.GetValue(awaiter, null) ?? false) && spins++ < 200)
                {
                    try { System.Threading.Thread.Sleep(1); } catch { }
                }
                var getResult = awaiter?.GetType().GetMethod("GetResult", BindingFlags.Public | BindingFlags.Instance);
                return getResult?.Invoke(awaiter, null);
            }
            catch { return null; }
        }

    }
}
