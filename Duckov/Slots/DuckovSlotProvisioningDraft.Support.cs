using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using ItemModKit.Core;
using Newtonsoft.Json;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// Duckov 侧补槽草案（支持逻辑）：
    /// 负责模板解析、槽位集合操作、持久化草案读写以及补槽诊断辅助。
    /// </summary>
    public static partial class DuckovSlotProvisioningDraft
    {
        /// <summary>
        /// 根据草案定义和模板槽位构建最终的槽位创建选项。
        /// </summary>
        /// <param name="definition">草案中的单个槽位定义。</param>
        /// <param name="templateSlot">可选的模板槽位对象。</param>
        /// <returns>用于运行时创建槽位的选项对象。</returns>
        private static SlotCreateOptions BuildCreateOptions(SlotProvisionDefinition definition, object templateSlot)
        {
            var requireTags = definition.RequireTags;
            var excludeTags = definition.ExcludeTags;
            var slotIcon = definition.SlotIcon;
            var forbidSameId = definition.ForbidItemsWithSameID;

            if (templateSlot != null && definition.Template != null)
            {
                if (definition.Template.CloneFilters)
                {
                    if (requireTags == null || requireTags.Length == 0)
                    {
                        requireTags = ReadStringArray(templateSlot, "requireTags") ?? ReadStringArray(templateSlot, "RequireTags");
                    }

                    if (excludeTags == null || excludeTags.Length == 0)
                    {
                        excludeTags = ReadStringArray(templateSlot, "excludeTags") ?? ReadStringArray(templateSlot, "ExcludeTags");
                    }
                }

                if (definition.Template.CloneIcon && slotIcon == null)
                {
                    slotIcon = DuckovTypeUtils.GetMaybe(templateSlot, new[] { "SlotIcon", "slotIcon" });
                }

                if (!forbidSameId.HasValue)
                {
                    forbidSameId = ReadBool(templateSlot, "forbidItemsWithSameID") ?? ReadBool(templateSlot, "ForbidItemsWithSameID");
                }
            }

            return new SlotCreateOptions
            {
                Key = definition.Key,
                DisplayName = definition.DisplayName,
                SlotIcon = slotIcon,
                RequireTags = requireTags,
                ExcludeTags = excludeTags,
                ForbidItemsWithSameID = forbidSameId,
            };
        }

        /// <summary>
        /// 获取宿主物品当前持有的槽位集合对象。
        /// </summary>
        /// <param name="ownerItem">目标宿主物品。</param>
        /// <returns>成功返回槽位集合对象；否则返回 null。</returns>
        private static object GetSlotsObject(object ownerItem)
        {
            if (ownerItem == null)
            {
                return null;
            }

            try
            {
                return DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 根据模板引用解析实际使用的模板槽位。
        /// </summary>
        /// <param name="slots">宿主物品上的槽位集合对象。</param>
        /// <param name="template">模板引用信息。</param>
        /// <returns>成功解析则返回模板槽位；否则返回 null。</returns>
        private static object ResolveTemplateSlot(object slots, SlotProvisionTemplateReference template)
        {
            if (template == null)
            {
                return null;
            }

            if (template.TemplateSlot != null)
            {
                return template.TemplateSlot;
            }

            if (slots == null || string.IsNullOrEmpty(template.TemplateSlotKey))
            {
                return null;
            }

            return ResolveSlot(slots, template.TemplateSlotKey);
        }

        /// <summary>
        /// 在草案内部解析指定键对应的运行时槽位对象。
        /// </summary>
        /// <param name="slots">宿主物品上的槽位集合对象。</param>
        /// <param name="slotKey">目标槽位键。</param>
        /// <returns>找到则返回槽位对象；否则返回 null。</returns>
        private static object ResolveSlot(object slots, string slotKey)
        {
            if (slots == null || string.IsNullOrEmpty(slotKey))
            {
                return null;
            }

            var slotsType = slots.GetType();
            var getSlotStr = DuckovReflectionCache.GetMethod(slotsType, "GetSlot", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(string) });
            if (getSlotStr != null)
            {
                try
                {
                    var value = getSlotStr.Invoke(slots, new object[] { slotKey });
                    if (value != null)
                    {
                        return value;
                    }
                }
                catch
                {
                }
            }

            try
            {
                if (slots is System.Collections.IEnumerable enumerable)
                {
                    foreach (var slot in enumerable)
                    {
                        if (slot == null)
                        {
                            continue;
                        }

                        var key = DuckovTypeUtils.GetMaybe(slot, new[] { "Key", "key" }) as string;
                        if (!string.IsNullOrEmpty(key) && string.Equals(key, slotKey, StringComparison.OrdinalIgnoreCase))
                        {
                            return slot;
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        /// <summary>
        /// 尝试调用宿主物品的 CreateSlotsComponent 来创建槽位宿主。
        /// </summary>
        /// <param name="ownerItem">目标宿主物品。</param>
        /// <returns>创建成功且后续可读到槽位集合时返回 true；否则返回 false。</returns>
        private static bool TryInvokeCreateSlotsComponent(object ownerItem)
        {
            if (ownerItem == null)
            {
                return false;
            }

            try
            {
                var method = ownerItem.GetType().GetMethod("CreateSlotsComponent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (method == null)
                {
                    return false;
                }

                method.Invoke(ownerItem, null);
                return GetSlotsObject(ownerItem) != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 按持久化 tombstone 回放原版槽位移除。
        /// 只有在槽位当前为空时才允许真正删除对应原版槽位。
        /// </summary>
        /// <param name="ownerItem">目标宿主物品。</param>
        /// <param name="removedBuiltinSlotKeys">已记录为移除的原版槽位键集合。</param>
        /// <returns>至少成功应用一项移除时返回 true；否则返回 false。</returns>
        private static bool TryApplyRemovedBuiltinSlots(object ownerItem, List<string> removedBuiltinSlotKeys)
        {
            if (ownerItem == null || removedBuiltinSlotKeys == null || removedBuiltinSlotKeys.Count == 0)
            {
                return false;
            }

            var slots = GetSlotsObject(ownerItem);
            if (slots == null)
            {
                return false;
            }

            var applied = false;
            foreach (var slotKey in removedBuiltinSlotKeys)
            {
                if (string.IsNullOrWhiteSpace(slotKey))
                {
                    continue;
                }

                var slot = ResolveSlot(slots, slotKey);
                if (slot == null)
                {
                    continue;
                }

                if (TryGetSlotContent(slot) != null)
                {
                    return false;
                }

                if (!TryRemoveSlotFromCollection(slots, slot))
                {
                    return false;
                }

                applied = true;
            }

            return applied;
        }

        /// <summary>
        /// 读取槽位当前内容物。
        /// </summary>
        /// <param name="slot">目标槽位对象。</param>
        /// <returns>成功时返回当前内容物；否则返回 null。</returns>
        private static object TryGetSlotContent(object slot)
        {
            try
            {
                return DuckovReflectionCache.GetProp(slot.GetType(), "Content", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(slot, null);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从运行时槽位集合中移除指定槽位对象。
        /// </summary>
        /// <param name="slots">宿主上的槽位集合对象。</param>
        /// <param name="slot">待移除的槽位对象。</param>
        /// <returns>成功移除时返回 true；否则返回 false。</returns>
        private static bool TryRemoveSlotFromCollection(object slots, object slot)
        {
            if (slots == null || slot == null)
            {
                return false;
            }

            try
            {
                var remove = DuckovReflectionCache.GetMethod(slots.GetType(), "Remove", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { slot.GetType() })
                    ?? DuckovReflectionCache.GetMethod(slots.GetType(), "Remove", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (remove != null)
                {
                    var result = remove.Invoke(slots, new[] { slot });
                    if (!(result is bool removed) || removed)
                    {
                        InvalidateSlotCollectionCache(slots);
                        return true;
                    }
                }
            }
            catch
            {
            }

            try
            {
                var listField = DuckovReflectionCache.GetField(slots.GetType(), "list", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var list = listField?.GetValue(slots);
                if (list != null)
                {
                    var remove = DuckovReflectionCache.GetMethod(list.GetType(), "Remove", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { slot.GetType() })
                        ?? DuckovReflectionCache.GetMethod(list.GetType(), "Remove", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (remove != null)
                    {
                        var result = remove.Invoke(list, new[] { slot });
                        if (!(result is bool removed) || removed)
                        {
                            InvalidateSlotCollectionCache(slots);
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        /// <summary>
        /// 使槽位集合的内部键缓存失效。
        /// </summary>
        /// <param name="slots">宿主上的槽位集合对象。</param>
        private static void InvalidateSlotCollectionCache(object slots)
        {
            try
            {
                var cacheField = DuckovReflectionCache.GetField(slots.GetType(), "_cachedSlotsDictionary", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                cacheField?.SetValue(slots, null);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 把请求中的动态槽位定义写入持久化草案。
        /// </summary>
        /// <param name="ownerItem">目标宿主物品。</param>
        /// <param name="request">当前补槽请求。</param>
        /// <returns>写入成功时返回 true；否则返回 false。</returns>
        private static bool TryPersistDefinitions(object ownerItem, EnsureSlotsRequest request)
        {
            try
            {
                var variableKey = string.IsNullOrEmpty(request.PersistenceVariableKey)
                    ? DefaultPersistenceVariableKey
                    : request.PersistenceVariableKey;
                var payload = ReadPersistedData(ownerItem, variableKey) ?? new SlotPersistenceDraftData();
                var byKey = payload.Slots.ToDictionary(entry => entry.Key ?? string.Empty, StringComparer.OrdinalIgnoreCase);

                foreach (var definition in request.DesiredSlots)
                {
                    if (definition == null || string.IsNullOrEmpty(definition.Key))
                    {
                        continue;
                    }

                    byKey[definition.Key] = new SlotPersistenceSlotDefinition
                    {
                        Key = definition.Key,
                        DisplayName = definition.DisplayName,
                        TemplateSlotKey = definition.Template?.TemplateSlotKey,
                        RequireTags = definition.RequireTags,
                        ExcludeTags = definition.ExcludeTags,
                        ForbidItemsWithSameID = definition.ForbidItemsWithSameID,
                        OriginHint = SlotPersistenceOriginHint.Dynamic,
                    };
                }

                payload.SchemaVersion = SlotPersistenceDraftSchema.CurrentVersion;
                payload.Slots = byKey.Values.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase).ToList();
                return WritePersistedData(ownerItem, variableKey, payload);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 按指定变量键写回补槽草案持久化数据。
        /// </summary>
        /// <param name="ownerItem">目标宿主物品。</param>
        /// <param name="variableKey">持久化变量键。</param>
        /// <param name="payload">待写入的草案对象。</param>
        /// <returns>写入或删除成功时返回 true；否则返回 false。</returns>
        private static bool WritePersistedData(object ownerItem, string variableKey, SlotPersistenceDraftData payload)
        {
            try
            {
                NormalizePersistedData(payload);
                if (payload == null || (payload.Slots.Count == 0 && payload.RemovedBuiltinSlotKeys.Count == 0 && payload.Mutations.Count == 0))
                {
                    return IMKDuckov.Item.RemoveVariable(ownerItem, variableKey);
                }

                payload.SchemaVersion = SlotPersistenceDraftSchema.CurrentVersion;
                var json = JsonConvert.SerializeObject(payload, Formatting.None);
                IMKDuckov.Item.SetVariable(ownerItem, variableKey, json, constant: false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 按指定变量键读取补槽草案持久化数据。
        /// </summary>
        /// <param name="ownerItem">目标宿主物品。</param>
        /// <param name="variableKey">持久化变量键。</param>
        /// <returns>成功解析则返回草案对象；否则返回 null。</returns>
        private static SlotPersistenceDraftData ReadPersistedData(object ownerItem, string variableKey)
        {
            try
            {
                var raw = IMKDuckov.Item.GetVariable(ownerItem, variableKey) as string;
                if (string.IsNullOrEmpty(raw))
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<SlotPersistenceDraftData>(raw);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 标准化补槽草案持久化数据。
        /// 该过程会初始化集合、移除无效条目并对原版移除键去重排序。
        /// </summary>
        /// <param name="payload">待标准化的草案对象。</param>
        private static void NormalizePersistedData(SlotPersistenceDraftData payload)
        {
            if (payload == null)
            {
                return;
            }

            payload.Slots ??= new List<SlotPersistenceSlotDefinition>();
            payload.RemovedBuiltinSlotKeys ??= new List<string>();
            payload.Mutations ??= new List<SlotPersistenceSlotMutation>();

            payload.Slots.RemoveAll(entry => entry == null || string.IsNullOrWhiteSpace(entry.Key));
            payload.RemovedBuiltinSlotKeys.RemoveAll(string.IsNullOrWhiteSpace);
            payload.Mutations.RemoveAll(entry => entry == null || string.IsNullOrWhiteSpace(entry.Key) || entry.Kind == SlotPersistenceMutationKind.None);

            payload.RemovedBuiltinSlotKeys = payload.RemovedBuiltinSlotKeys
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

            /// <summary>
            /// 尝试刷新宿主物品所在背包的 UI 表现。
            /// </summary>
            /// <param name="ownerItem">目标宿主物品。</param>
        private static void TryRefreshOwnerInventory(object ownerItem)
        {
            try
            {
                var inventory = ownerItem.GetType().GetProperty("InInventory", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(ownerItem, null);
                if (inventory != null)
                {
                    IMKDuckov.UIRefresh.RefreshInventory(inventory, markNeedInspection: true);
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 在补槽流程失败时回滚已创建的槽位。
        /// 该回滚按逆序执行，尽量恢复到补槽前的结构状态。
        /// </summary>
        /// <param name="ownerItem">目标宿主物品。</param>
        /// <param name="createdKeys">本次流程中已创建的槽位键集合。</param>
        private static void RollbackCreatedSlots(object ownerItem, List<string> createdKeys)
        {
            if (ownerItem == null || createdKeys == null || createdKeys.Count == 0)
            {
                return;
            }

            for (var index = createdKeys.Count - 1; index >= 0; index--)
            {
                var key = createdKeys[index];
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                try
                {
                    IMKDuckov.Write.TryRemoveSlot(ownerItem, key);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// 从目标对象的指定成员中读取字符串数组。
        /// 支持直接字符串数组、可枚举字符串集合以及通用可枚举对象集合。
        /// </summary>
        /// <param name="source">目标对象。</param>
        /// <param name="memberName">成员名。</param>
        /// <returns>成功解析则返回字符串数组；否则返回 null。</returns>
        private static string[] ReadStringArray(object source, string memberName)
        {
            try
            {
                var value = DuckovTypeUtils.GetMaybe(source, new[] { memberName });
                if (value == null)
                {
                    return null;
                }

                if (value is string[] direct)
                {
                    return direct;
                }

                if (value is IEnumerable<string> strings)
                {
                    return strings.Where(entry => !string.IsNullOrEmpty(entry)).ToArray();
                }

                if (value is System.Collections.IEnumerable enumerable)
                {
                    var list = new List<string>();
                    foreach (var entry in enumerable)
                    {
                        if (entry == null)
                        {
                            continue;
                        }

                        var name = DuckovTypeUtils.GetMaybe(entry, new[] { "name", "Name" }) as string;
                        if (!string.IsNullOrEmpty(name))
                        {
                            list.Add(name);
                            continue;
                        }

                        var text = entry as string ?? entry.ToString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            list.Add(text);
                        }
                    }

                    return list.Count == 0 ? null : list.ToArray();
                }
            }
            catch
            {
            }

            return null;
        }

        /// <summary>
        /// 从目标对象的指定成员中读取布尔值。
        /// </summary>
        /// <param name="source">目标对象。</param>
        /// <param name="memberName">成员名。</param>
        /// <returns>成功解析则返回布尔值；否则返回 null。</returns>
        private static bool? ReadBool(object source, string memberName)
        {
            try
            {
                var value = DuckovTypeUtils.GetMaybe(source, new[] { memberName });
                if (value == null)
                {
                    return null;
                }

                if (value is bool flag)
                {
                    return flag;
                }

                if (bool.TryParse(value.ToString(), out var parsed))
                {
                    return parsed;
                }
            }
            catch
            {
            }

            return null;
        }

        /// <summary>
        /// 开始记录某个补槽阶段的耗时。
        /// </summary>
        /// <param name="phase">当前阶段。</param>
        /// <param name="timings">阶段计时器字典。</param>
        private static void StartPhase(SlotProvisioningPhase phase, Dictionary<SlotProvisioningPhase, Stopwatch> timings)
        {
            if (timings == null)
            {
                return;
            }

            if (!timings.TryGetValue(phase, out var stopwatch))
            {
                stopwatch = new Stopwatch();
                timings[phase] = stopwatch;
            }

            stopwatch.Restart();
        }

        /// <summary>
        /// 结束某个补槽阶段的耗时记录，并把结果写入诊断对象。
        /// 记录单位为毫秒。
        /// </summary>
        /// <param name="phase">当前阶段。</param>
        /// <param name="diagnostics">补槽流程诊断对象。</param>
        /// <param name="timings">阶段计时器字典。</param>
        private static void CompletePhase(SlotProvisioningPhase phase, EnsureSlotsDiagnostics diagnostics, Dictionary<SlotProvisioningPhase, Stopwatch> timings)
        {
            if (diagnostics == null || timings == null)
            {
                return;
            }

            if (!timings.TryGetValue(phase, out var stopwatch))
            {
                return;
            }

            stopwatch.Stop();
            diagnostics.PhaseTimings[phase] = stopwatch.ElapsedMilliseconds;
        }
    }
}