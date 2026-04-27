using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 写入服务（槽位/运行时支持）：
    /// 负责解析槽位对象、读取内容物、解析插入方法，以及槽位集合层面的底层操作。
    /// </summary>
    internal sealed partial class WriteService : IWriteService
    {
        private static readonly ConcurrentDictionary<Type, SlotHostAccessPlan> s_slotHostPlans = new ConcurrentDictionary<Type, SlotHostAccessPlan>();
        private static readonly ConcurrentDictionary<Type, SlotCollectionAccessPlan> s_slotCollectionPlans = new ConcurrentDictionary<Type, SlotCollectionAccessPlan>();
        private static readonly ConcurrentDictionary<Type, SlotInstanceAccessPlan> s_slotInstancePlans = new ConcurrentDictionary<Type, SlotInstanceAccessPlan>();

        private sealed class SlotHostAccessPlan
        {
            public Func<object, object> SlotsGetter;
            public MethodInfo CreateSlotsComponent;
        }

        private sealed class SlotCollectionAccessPlan
        {
            public MethodInfo GetSlotByKey;
            public FieldInfo BackingListField;
            public FieldInfo CachedDictionaryField;
        }

        private sealed class SlotInstanceAccessPlan
        {
            public PropertyInfo ContentProperty;
            public MethodInfo Unplug;
            public MethodInfo Changed;
        }

        private static SlotHostAccessPlan GetSlotHostPlan(Type ownerType)
        {
            return s_slotHostPlans.GetOrAdd(ownerType, static type => new SlotHostAccessPlan
            {
                SlotsGetter = DuckovReflectionCache.GetGetter(type, "Slots", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                CreateSlotsComponent = DuckovReflectionCache.GetMethod(type, "CreateSlotsComponent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)
                    ?? DuckovReflectionCache.GetMethod(type, "CreateSlotsComponent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
            });
        }

        private static SlotCollectionAccessPlan GetSlotCollectionPlan(Type slotsType)
        {
            return s_slotCollectionPlans.GetOrAdd(slotsType, static type => new SlotCollectionAccessPlan
            {
                GetSlotByKey = DuckovReflectionCache.GetMethod(type, "GetSlot", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(string) }),
                BackingListField = DuckovReflectionCache.GetField(type, "list", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                CachedDictionaryField = DuckovReflectionCache.GetField(type, "_cachedSlotsDictionary", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
            });
        }

        private static SlotInstanceAccessPlan GetSlotInstancePlan(Type slotType)
        {
            return s_slotInstancePlans.GetOrAdd(slotType, static type => new SlotInstanceAccessPlan
            {
                ContentProperty = DuckovReflectionCache.GetProp(type, "Content", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                Unplug = DuckovReflectionCache.GetMethod(type, "Unplug", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                Changed = DuckovReflectionCache.GetMethod(type, "ForceInvokeSlotContentChangedEvent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
            });
        }

        private static object GetSlotHost(object ownerItem)
        {
            if (ownerItem == null) return null;
            return GetSlotHostPlan(ownerItem.GetType()).SlotsGetter?.Invoke(ownerItem);
        }

        /// <summary>
        /// 按槽位键解析运行时槽位对象。
        /// 优先调用宿主槽位集合的 GetSlot(string)，失败时回退到枚举匹配。
        /// </summary>
        /// <param name="slots">宿主上的槽位集合对象。</param>
        /// <param name="slotKey">目标槽位键。</param>
        /// <returns>找到则返回槽位对象；否则返回 null。</returns>
        private static object ResolveSlot(object slots, string slotKey)
        {
            if (slots == null || string.IsNullOrEmpty(slotKey)) return null;
            var plan = GetSlotCollectionPlan(slots.GetType());
            var getSlotStr = plan.GetSlotByKey;
            if (getSlotStr != null)
            {
                try { var v = getSlotStr.Invoke(slots, new object[] { slotKey }); if (v != null) return v; } catch { }
            }

            try
            {
                var enumerable = slots as System.Collections.IEnumerable;
                if (enumerable != null)
                {
                    foreach (var s in enumerable)
                    {
                        if (s == null) continue;
                        var keyP = DuckovReflectionCache.GetProp(s.GetType(), "Key", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        var keyV = keyP?.GetValue(s, null) as string;
                        if (!string.IsNullOrEmpty(keyV) && string.Equals(keyV, slotKey, StringComparison.OrdinalIgnoreCase)) return s;
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// 读取槽位当前内容物。
        /// </summary>
        /// <param name="slot">目标槽位对象。</param>
        /// <param name="content">输出的内容物对象。</param>
        /// <returns>存在内容物时返回 true；否则返回 false。</returns>
        private static bool TryGetSlotContent(object slot, out object content)
        {
            content = null;
            if (slot == null) return false;
            try
            {
                var cp = GetSlotInstancePlan(slot.GetType()).ContentProperty;
                content = cp?.GetValue(slot, null);
                return content != null;
            }
            catch { return false; }
        }

        /// <summary>
        /// 判断指定子物品是否允许插入目标槽位。
        /// 当运行时无法解析 CanPlug 方法时，默认按“允许”处理。
        /// </summary>
        /// <param name="slot">目标槽位对象。</param>
        /// <param name="childItem">待插入的子物品。</param>
        /// <returns>允许插入时返回 true；否则返回 false。</returns>
        private static bool CanPlug(object slot, object childItem)
        {
            try
            {
                var m = DuckovReflectionCache.GetMethod(slot.GetType(), "CanPlug", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { childItem?.GetType() });
                if (m != null)
                {
                    var r = m.Invoke(slot, new[] { childItem });
                    if (r is bool b) return b;
                }
            }
            catch { }

            return true;
        }

        /// <summary>
        /// 解析槽位对象上的 Plug 方法签名。
        /// 支持 `Plug(item)` 与 `Plug(item, out previous)` 两种常见形态。
        /// </summary>
        /// <param name="slot">目标槽位对象。</param>
        /// <returns>返回解析到的方法及其是否包含 out previous 参数。</returns>
        private static (MethodInfo plug, bool hasOutPrev) ResolvePlugMethod(object slot)
        {
            var st = slot.GetType();
            try
            {
                var methods = st.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var m in methods)
                {
                    if (m.Name != "Plug") continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 2 && ps[1].IsOut)
                    {
                        if (m.ReturnType == typeof(bool)) return (m, true);
                    }

                    if (ps.Length == 1 && m.ReturnType == typeof(bool)) return (m, false);
                }
            }
            catch { }

            return (null, false);
        }

        /// <summary>
        /// 从运行时槽位集合中移除指定槽位对象。
        /// 会优先尝试宿主集合自身的 Remove，再回退到内部 list 字段。
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
                var list = GetSlotCollectionPlan(slots.GetType()).BackingListField?.GetValue(slots);
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
        /// 使槽位集合内部的键缓存失效，确保后续按键查找可见最新结构。
        /// </summary>
        /// <param name="slots">宿主上的槽位集合对象。</param>
        private static void InvalidateSlotCollectionCache(object slots)
        {
            try
            {
                GetSlotCollectionPlan(slots.GetType()).CachedDictionaryField?.SetValue(slots, null);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 尝试主动触发槽位内容变化事件。
        /// 主要用于底层删槽或结构变更后的运行时同步。
        /// </summary>
        /// <param name="slot">目标槽位对象。</param>
        private static void TryInvokeSlotChanged(object slot)
        {
            try
            {
                GetSlotInstancePlan(slot.GetType()).Changed?.Invoke(slot, null);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 直接调用槽位的 Unplug 逻辑并返回拔出的内容物。
        /// </summary>
        /// <param name="slot">目标槽位对象。</param>
        /// <returns>成功时返回拔出的物品；失败时返回 null。</returns>
        private static object TryUnplugSlot(object slot)
        {
            try
            {
                return GetSlotInstancePlan(slot.GetType()).Unplug?.Invoke(slot, null);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 收集当前槽位集合中全部已存在的槽位键。
        /// 返回结果会自动去重并保持原有枚举顺序。
        /// </summary>
        /// <param name="slots">宿主上的槽位集合对象。</param>
        /// <returns>当前存在的槽位键列表。</returns>
        private static List<string> CollectExistingSlotKeys(object slots)
        {
            var keys = new List<string>();
            if (!(slots is System.Collections.IEnumerable enumerable))
            {
                return keys;
            }

            foreach (var slot in enumerable)
            {
                if (slot == null)
                {
                    continue;
                }

                var key = DuckovTypeUtils.GetMaybe(slot, new[] { "Key", "key" }) as string;
                if (!string.IsNullOrWhiteSpace(key) && !keys.Exists(existing => string.Equals(existing, key, StringComparison.OrdinalIgnoreCase)))
                {
                    keys.Add(key);
                }
            }

            return keys;
        }
    }
}