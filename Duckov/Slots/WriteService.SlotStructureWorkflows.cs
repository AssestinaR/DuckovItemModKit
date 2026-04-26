using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Duckov.Utilities;
using ItemModKit.Core;
using UnityEngine;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 写入服务（槽位/结构流程）：
    /// 负责补宿主、补槽位、增槽、设置槽位图标和标签等结构性写入。
    /// </summary>
    internal sealed partial class WriteService : IWriteService
    {
        /// <summary>
        /// 确保宿主物品具备可写的槽位宿主组件。
        /// 当当前物品尚未拥有槽位系统时，会尝试调用运行时的 CreateSlotsComponent。
        /// </summary>
        /// <param name="ownerItem">目标宿主物品。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryEnsureSlotHost(object ownerItem)
        {
            try
            {
                if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.owner");
                var slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (slots != null) return RichResult.Success();

                var create = ownerItem.GetType().GetMethod("CreateSlotsComponent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (create == null) return RichResult.Fail(ErrorCode.NotSupported, "slot.host.create_missing");

                create.Invoke(ownerItem, null);
                slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (slots == null) return RichResult.Fail(ErrorCode.OperationFailed, "slot.host.create_failed");

                NotifySlotAndChildChanged(ownerItem);
                TryRefreshOwnerInventory(ownerItem);
                IMKDuckov.PublishItemChanged(ownerItem);
                MarkDirtyFromWriteScope(ownerItem, DirtyKind.Slots);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryEnsureSlotHost failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        /// <summary>
        /// 确保宿主物品具备指定槽位集合。
        /// 缺失槽位宿主时会先尝试创建宿主，然后逐个补齐缺失槽位。
        /// </summary>
        /// <param name="ownerItem">目标宿主物品。</param>
        /// <param name="desiredSlots">期望存在的槽位定义集合。</param>
        /// <param name="reuseExistingIfPresent">遇到同键槽位时是否直接视为满足。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryEnsureSlots(object ownerItem, SlotCreateOptions[] desiredSlots, bool reuseExistingIfPresent = true)
        {
            try
            {
                if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.owner");
                if (desiredSlots == null || desiredSlots.Length == 0) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.ensure.no_desired_slots");

                var ensureHost = TryEnsureSlotHost(ownerItem);
                if (!ensureHost.Ok)
                {
                    return ensureHost;
                }

                var slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (slots == null) return RichResult.Fail(ErrorCode.NotSupported, "slot.owner.no_slots");

                foreach (var options in desiredSlots)
                {
                    if (options == null || string.IsNullOrEmpty(options.Key))
                    {
                        return RichResult.Fail(ErrorCode.InvalidArgument, "slot.ensure.invalid_definition");
                    }

                    if (ResolveSlot(slots, options.Key) != null)
                    {
                        if (reuseExistingIfPresent)
                        {
                            continue;
                        }

                        return RichResult.Fail(ErrorCode.Conflict, "slot.ensure.slot_already_exists");
                    }

                    var add = TryAddSlot(ownerItem, options);
                    if (!add.Ok)
                    {
                        return add;
                    }

                    slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                    if (slots == null)
                    {
                        return RichResult.Fail(ErrorCode.OperationFailed, "slot.ensure.slot_host_lost");
                    }
                }

                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryEnsureSlots failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        /// <summary>
        /// 在宿主物品上新增一个槽位。
        /// 新槽位键会自动做唯一化处理，避免和已有槽位冲突。
        /// </summary>
        /// <param name="ownerItem">目标宿主物品。</param>
        /// <param name="options">槽位创建选项。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TryAddSlot(object ownerItem, SlotCreateOptions options)
        {
            try
            {
                if (ownerItem == null || options == null) return RichResult.Fail(ErrorCode.InvalidArgument, "null args");
                var slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (slots == null) return RichResult.Fail(ErrorCode.NotSupported, "no Slots on owner");
                var slotType = ResolveRuntimeSlotType(slots);
                if (slotType == null) return RichResult.Fail(ErrorCode.NotSupported, "Slot type missing");
                var desired = string.IsNullOrEmpty(options.Key) ? "Socket" : options.Key;
                var finalKey = EnsureUniqueSlotKey(slots, desired);
                var slot = CreateSlotInstance(slotType, finalKey);
                if (slot == null) return RichResult.Fail(ErrorCode.OperationFailed, "create slot failed");
                if (options.SlotIcon != null) DuckovTypeUtils.SetProp(slot, "SlotIcon", options.SlotIcon);
                if (!TryAssignSlotTags(slot, options.RequireTags, options.ExcludeTags, out var tagError))
                {
                    return RichResult.Fail(ErrorCode.OperationFailed, "slot.tags.assign_failed:" + tagError);
                }

                if (options.ForbidItemsWithSameID.HasValue)
                {
                    var f = DuckovReflectionCache.GetField(slotType, "forbidItemsWithSameID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null) f.SetValue(slot, options.ForbidItemsWithSameID.Value);
                }

                var init = DuckovReflectionCache.GetMethod(slotType, "Initialize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                init?.Invoke(slot, new[] { slots });
                var add = DuckovReflectionCache.GetMethod(slots.GetType(), "Add", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (add == null) return RichResult.Fail(ErrorCode.NotSupported, "SlotCollection.Add not found");
                add.Invoke(slots, new[] { slot });
                NotifySlotAndChildChanged(ownerItem);
                MarkDirtyFromWriteScope(ownerItem, DirtyKind.Slots);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryAddSlot failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        /// <summary>
        /// 更新目标槽位的图标。
        /// </summary>
        /// <param name="ownerItem">槽位宿主物品。</param>
        /// <param name="slotKey">目标槽位键。</param>
        /// <param name="sprite">新的图标对象。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TrySetSlotIcon(object ownerItem, string slotKey, object sprite)
        {
            try
            {
                if (ownerItem == null || string.IsNullOrEmpty(slotKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.args");
                var slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (slots == null) return RichResult.Fail(ErrorCode.NotSupported, "slot.owner.no_slots");
                var slot = ResolveSlot(slots, slotKey);
                if (slot == null) return RichResult.Fail(ErrorCode.NotFound, "slot.notfound");
                DuckovTypeUtils.SetProp(slot, "SlotIcon", sprite);
                NotifySlotAndChildChanged(ownerItem);
                MarkDirtyFromWriteScope(ownerItem, DirtyKind.Slots);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TrySetSlotIcon failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        /// <summary>
        /// 更新空槽位的标签限制。
        /// 仅当目标槽位当前未被占用时允许修改标签集合。
        /// </summary>
        /// <param name="ownerItem">槽位宿主物品。</param>
        /// <param name="slotKey">目标槽位键。</param>
        /// <param name="requireTagKeys">新的必需标签键集合。</param>
        /// <param name="excludeTagKeys">新的排除标签键集合。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        public RichResult TrySetSlotTags(object ownerItem, string slotKey, string[] requireTagKeys, string[] excludeTagKeys)
        {
            try
            {
                if (ownerItem == null || string.IsNullOrEmpty(slotKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "slot.invalid.args");
                var slots = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(ownerItem);
                if (slots == null) return RichResult.Fail(ErrorCode.NotSupported, "slot.owner.no_slots");
                var slot = ResolveSlot(slots, slotKey);
                if (slot == null) return RichResult.Fail(ErrorCode.NotFound, "slot.notfound");
                if (TryGetSlotContent(slot, out _)) return RichResult.Fail(ErrorCode.Conflict, "slot.occupied");
                if (!TryAssignSlotTags(slot, requireTagKeys, excludeTagKeys, out var tagError))
                {
                    return RichResult.Fail(ErrorCode.OperationFailed, "slot.tags.assign_failed:" + tagError);
                }
                NotifySlotAndChildChanged(ownerItem);
                MarkDirtyFromWriteScope(ownerItem, DirtyKind.Slots);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TrySetSlotTags failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        /// <summary>
        /// 创建运行时槽位实例，并尽量用给定键完成初始化。
        /// 优先使用接受 string 键的构造函数，失败时回退到无参构造加字段赋值。
        /// </summary>
        /// <param name="slotType">运行时槽位类型。</param>
        /// <param name="key">期望写入的新槽位键。</param>
        /// <returns>成功返回槽位实例；失败时返回 null。</returns>
        private static object CreateSlotInstance(Type slotType, string key)
        {
            try
            {
                var ctor = slotType.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(string) }, null);
                if (ctor != null)
                {
                    return ctor.Invoke(new object[] { key });
                }
            }
            catch
            {
            }

            try
            {
                var slot = Activator.CreateInstance(slotType);
                if (slot != null)
                {
                    var keyField = DuckovReflectionCache.GetField(slotType, "key", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (keyField != null)
                    {
                        keyField.SetValue(slot, key);
                    }
                }

                return slot;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 将标签限制写入槽位对象。
        /// </summary>
        /// <param name="slot">目标槽位对象。</param>
        /// <param name="requireTagKeys">必需标签键集合。</param>
        /// <param name="excludeTagKeys">排除标签键集合。</param>
        /// <param name="errorMessage">写入失败时的错误描述。</param>
        /// <returns>全部字段写入成功时返回 true；否则返回 false。</returns>
        private static bool TryAssignSlotTags(object slot, string[] requireTagKeys, string[] excludeTagKeys, out string errorMessage)
        {
            errorMessage = null;
            if (slot == null)
            {
                errorMessage = "slot.null";
                return false;
            }

            if (!TrySetSlotTagField(slot, "requireTags", requireTagKeys, out errorMessage))
            {
                return false;
            }

            if (!TrySetSlotTagField(slot, "excludeTags", excludeTagKeys, out errorMessage))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 把指定标签键集合写入槽位对象上的某个标签字段。
        /// </summary>
        /// <param name="slot">目标槽位对象。</param>
        /// <param name="fieldName">字段名，如 requireTags 或 excludeTags。</param>
        /// <param name="tagKeys">要写入的标签键集合。</param>
        /// <param name="errorMessage">写入失败时的错误描述。</param>
        /// <returns>写入成功时返回 true；否则返回 false。</returns>
        private static bool TrySetSlotTagField(object slot, string fieldName, string[] tagKeys, out string errorMessage)
        {
            errorMessage = null;
            var field = DuckovReflectionCache.GetField(slot.GetType(), fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                errorMessage = fieldName + ".missing";
                return false;
            }

            try
            {
                field.SetValue(slot, ResolveSlotTags(tagKeys));
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 将字符串标签键解析为运行时 Tag 集合。
        /// 当无法从现有注册表解析时，会尝试创建一个同名 Tag 占位对象。
        /// </summary>
        /// <param name="tagKeys">标签键集合。</param>
        /// <returns>解析得到的 Tag 列表。</returns>
        private static List<Tag> ResolveSlotTags(string[] tagKeys)
        {
            var resolved = new List<Tag>();
            if (tagKeys == null || tagKeys.Length == 0)
            {
                return resolved;
            }

            foreach (var rawKey in tagKeys)
            {
                var key = rawKey?.Trim();
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                var tag = TryResolveRegisteredSlotTag(key);

                if (tag == null)
                {
                    tag = ScriptableObject.CreateInstance<Tag>();
                    tag.name = key;
                }

                if (!resolved.Exists(existing => existing != null && existing.Hash == tag.Hash))
                {
                    resolved.Add(tag);
                }
            }

            return resolved;
        }

        private static Tag TryResolveRegisteredSlotTag(string key)
        {
            try
            {
                var allTags = GameplayDataSettings.Tags?.AllTags;
                if (allTags == null)
                {
                    return null;
                }

                return allTags.FirstOrDefault(tag =>
                    tag != null &&
                    (string.Equals(tag.name, key, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Convert.ToString(DuckovTypeUtils.GetMaybe(tag, new[] { "Key" })), key, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Convert.ToString(DuckovTypeUtils.GetMaybe(tag, new[] { "Name", "name" })), key, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Convert.ToString(DuckovTypeUtils.GetMaybe(tag, new[] { "DisplayName" })), key, StringComparison.OrdinalIgnoreCase)));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 推断当前宿主上的运行时槽位类型。
        /// 优先从现有槽位实例取类型；如果当前没有实例，则回退到常见类型名探测。
        /// </summary>
        /// <param name="slots">宿主上的槽位集合对象。</param>
        /// <returns>解析得到的槽位类型；失败时返回 null。</returns>
        private static Type ResolveRuntimeSlotType(object slots)
        {
            try
            {
                var enumerable = slots as System.Collections.IEnumerable;
                if (enumerable != null)
                {
                    foreach (var slot in enumerable)
                    {
                        if (slot != null)
                        {
                            return slot.GetType();
                        }
                    }
                }
            }
            catch
            {
            }

            return DuckovTypeUtils.FindType("ItemStatsSystem.Items.Slot")
                ?? DuckovTypeUtils.FindType("ItemStatsSystem.Slot")
                ?? DuckovTypeUtils.FindType("Slot");
        }
    }
}