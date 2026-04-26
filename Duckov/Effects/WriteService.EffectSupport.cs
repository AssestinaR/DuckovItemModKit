using System;
using System.Collections;
using System.Reflection;
using ItemModKit.Core;
using UnityEngine;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 写入服务（Effects Support）：封装 effect graph 公共反射访问与属性写入辅助。
    /// </summary>
    internal sealed partial class WriteService : IWriteService
    {
        private static IList TryGetEffectsList(object item)
        {
            return DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item) as IList;
        }

        private static object TryGetEffectAt(object item, int effectIndex, out RichResult error)
        {
            error = RichResult.Success();
            if (item == null)
            {
                error = RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                return null;
            }

            var effects = TryGetEffectsList(item);
            if (effects == null)
            {
                error = RichResult.Fail(ErrorCode.NotSupported, "no Effects list");
                return null;
            }

            if (effectIndex < 0 || effectIndex >= effects.Count)
            {
                error = RichResult.Fail(ErrorCode.OutOfRange, "index");
                return null;
            }

            return effects[effectIndex];
        }

        private static IList TryGetEffectComponents(object effect, string kind)
        {
            var fieldName = kind.Equals("Trigger", StringComparison.OrdinalIgnoreCase)
                ? "triggers"
                : kind.Equals("Filter", StringComparison.OrdinalIgnoreCase)
                    ? "filters"
                    : "actions";
            return DuckovReflectionCache.GetField(effect.GetType(), fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(effect) as IList;
        }

        private static bool TryAddToEffectsList(object item, object effect)
        {
            var effectsList = TryGetEffectsList(item);
            if (effectsList == null) return false;

            try
            {
                effectsList.Add(effect);
                return true;
            }
            catch
            {
                try
                {
                    var add = DuckovReflectionCache.GetMethod(effectsList.GetType(), "Add", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    add?.Invoke(effectsList, new[] { effect });
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        private static void TryBindEffectToItem(object effect, Type effectType, object item)
        {
            var setItem = DuckovReflectionCache.GetMethod(effectType, "SetItem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { item.GetType() })
                          ?? DuckovReflectionCache.GetMethod(effectType, "SetItem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            setItem?.Invoke(effect, new[] { item });
        }

        private static void TryAssignComponentMaster(Type componentType, object component, object effect)
        {
            try
            {
                DuckovReflectionCache.GetSetter(componentType, "Master", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(component, effect);
            }
            catch
            {
            }
        }

        private static string NormalizeEffectComponentKind(string kind)
        {
            if (kind.Equals("Trigger", StringComparison.OrdinalIgnoreCase)) return "Trigger";
            if (kind.Equals("Filter", StringComparison.OrdinalIgnoreCase)) return "Filter";
            return "Action";
        }

        private static bool TryAssignMember(object target, string memberName, object value)
        {
            return DuckovEffectSchemaSupport.TryAssignMember(target, memberName, value);
        }
    }
}