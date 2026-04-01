using System;
using System.Collections;
using System.Reflection;
using ItemModKit.Core;
using UnityEngine;

namespace ItemModKit.Adapters.Duckov
{
    internal sealed partial class WriteService : IWriteService
    {
        // Effect-related methods extracted from Modifiers file for clarity
        public RichResult TryAddEffect(object item, string effectTypeFullName, EffectCreateOptions options = null)
        {
            DebugLog("TryAddEffect type="+effectTypeFullName);
            try
            {
                if (item == null || string.IsNullOrEmpty(effectTypeFullName)) return RichResult.Fail(ErrorCode.InvalidArgument, "args");
                var effType = DuckovTypeUtils.FindType(effectTypeFullName); if (effType == null) return RichResult.Fail(ErrorCode.NotFound, "effect type not found");
                var go = DuckovTypeUtils.GetMaybe(item, new[]{"gameObject"}) as GameObject; if (go == null) return RichResult.Fail(ErrorCode.NotSupported, "item has no gameObject");
                var child = new GameObject(options?.Name ?? ("New "+effType.Name)); child.hideFlags = HideFlags.HideInInspector; child.transform.SetParent(go.transform,false);
                var effect = child.AddComponent(effType);
                if (options != null)
                {
                    if (options.Display.HasValue){ try { DuckovReflectionCache.GetSetter(effType, "display", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(effect, options.Display.Value); } catch { } }
                    if (!string.IsNullOrEmpty(options.Description)){ try { DuckovReflectionCache.GetSetter(effType, "description", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(effect, options.Description); } catch { } }
                }
                var effectsList = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item);
                var add = DuckovReflectionCache.GetMethod(effectsList?.GetType(), "Add", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ effType }); add?.Invoke(effectsList, new[]{ effect });
                var setItem = DuckovReflectionCache.GetMethod(effType, "SetItem", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ item.GetType() }) ?? DuckovReflectionCache.GetMethod(effType, "SetItem", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                setItem?.Invoke(effect, new[]{ item });
                if (options?.Enabled == true){ try { (effect as Behaviour).enabled = true; } catch { } }
                return RichResult.Success();
            }
            catch(Exception ex){ Log.Error("TryAddEffect failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        public RichResult TryAddEffect(object item, string effectTypeFullName) => TryAddEffect(item, effectTypeFullName, null);
        public RichResult TryRemoveEffect(object item, int effectIndex)
        {
            DebugLog("TryRemoveEffect index="+effectIndex);
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var effects = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item) as IList;
                if (effects == null) return RichResult.Fail(ErrorCode.NotSupported, "no Effects list");
                if (effectIndex < 0 || effectIndex >= effects.Count) return RichResult.Fail(ErrorCode.OutOfRange, "index");
                var effect = effects[effectIndex] as Component; effects.RemoveAt(effectIndex);
                if (effect != null){ try { UnityEngine.Object.DestroyImmediate(effect.gameObject); } catch { try { UnityEngine.Object.Destroy(effect.gameObject); } catch { } } }
                return RichResult.Success();
            }
            catch(Exception ex){ Log.Error("TryRemoveEffect failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        public RichResult TryEnableEffect(object item, int effectIndex, bool enabled)
        {
            DebugLog("TryEnableEffect index="+effectIndex+" enabled="+enabled);
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var effects = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item) as IList;
                if (effects == null) return RichResult.Fail(ErrorCode.NotSupported, "no Effects list");
                if (effectIndex < 0 || effectIndex >= effects.Count) return RichResult.Fail(ErrorCode.OutOfRange, "index");
                var effect = effects[effectIndex] as Behaviour; if (effect == null) return RichResult.Fail(ErrorCode.NotSupported, "effect not Behaviour");
                effect.enabled = enabled; return RichResult.Success();
            }
            catch(Exception ex){ Log.Error("TryEnableEffect failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        public RichResult TrySetEffectProperty(object item, int effectIndex, string propName, object value)
        {
            DebugLog("TrySetEffectProperty index="+effectIndex+" prop="+propName);
            try
            {
                if (item == null || string.IsNullOrEmpty(propName)) return RichResult.Fail(ErrorCode.InvalidArgument, "args");
                var effects = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item) as IList;
                if (effects == null) return RichResult.Fail(ErrorCode.NotSupported, "no Effects list");
                if (effectIndex < 0 || effectIndex >= effects.Count) return RichResult.Fail(ErrorCode.OutOfRange, "index");
                var effect = effects[effectIndex]; var et = effect.GetType();
                var setter = DuckovReflectionCache.GetSetter(et, propName, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
                          ?? DuckovReflectionCache.GetSetter(et, propName.ToLowerInvariant(), BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (setter == null) return RichResult.Fail(ErrorCode.NotSupported, "setter not found");
                setter(effect, value); return RichResult.Success();
            }
            catch(Exception ex){ Log.Error("TrySetEffectProperty failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        public RichResult TryAddEffectComponent(object item, int effectIndex, string componentTypeFullName, string kind)
        {
            DebugLog("TryAddEffectComponent index="+effectIndex+" kind="+kind+" type="+componentTypeFullName);
            try
            {
                if (item == null || string.IsNullOrEmpty(componentTypeFullName) || string.IsNullOrEmpty(kind)) return RichResult.Fail(ErrorCode.InvalidArgument, "args");
                var effects = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item) as IList;
                if (effects == null) return RichResult.Fail(ErrorCode.NotSupported, "no Effects list");
                if (effectIndex < 0 || effectIndex >= effects.Count) return RichResult.Fail(ErrorCode.OutOfRange, "index");
                var effect = effects[effectIndex] as Component; if (effect == null) return RichResult.Fail(ErrorCode.OperationFailed, "effect null");
                var type = DuckovTypeUtils.FindType(componentTypeFullName); if (type == null) return RichResult.Fail(ErrorCode.NotFound, "component type not found");
                var comp = effect.gameObject.AddComponent(type);
                var add = DuckovReflectionCache.GetMethod(effect.GetType(), "AddEffectComponent", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[] { DuckovTypeUtils.FindType("ItemStatsSystem.EffectComponent") ?? typeof(Component) })
                          ?? DuckovReflectionCache.GetMethod(effect.GetType(), "AddEffectComponent", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (add != null) add.Invoke(effect, new[] { comp });
                else
                {
                    var fieldName = kind.Equals("Trigger", StringComparison.OrdinalIgnoreCase) ? "triggers" : kind.Equals("Filter", StringComparison.OrdinalIgnoreCase) ? "filters" : "actions";
                    var list = DuckovReflectionCache.GetField(effect.GetType(), fieldName, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(effect) as IList;
                    list?.Add(comp);
                }
                try { DuckovReflectionCache.GetSetter(type, "Master", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(comp, effect); } catch { }
                return RichResult.Success();
            }
            catch(Exception ex){ Log.Error("TryAddEffectComponent failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        public RichResult TryRemoveEffectComponent(object item, int effectIndex, string kind, int componentIndex)
        {
            DebugLog("TryRemoveEffectComponent effectIndex="+effectIndex+" kind="+kind+" compIndex="+componentIndex);
            try
            {
                if (item == null || string.IsNullOrEmpty(kind)) return RichResult.Fail(ErrorCode.InvalidArgument, "args");
                var effects = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item) as IList;
                if (effects == null) return RichResult.Fail(ErrorCode.NotSupported, "no Effects list");
                if (effectIndex < 0 || effectIndex >= effects.Count) return RichResult.Fail(ErrorCode.OutOfRange, "index");
                var effect = effects[effectIndex] as Component; if (effect == null) return RichResult.Fail(ErrorCode.OperationFailed, "effect null");
                var fieldName = kind.Equals("Trigger", StringComparison.OrdinalIgnoreCase) ? "triggers" : kind.Equals("Filter", StringComparison.OrdinalIgnoreCase) ? "filters" : "actions";
                var list = DuckovReflectionCache.GetField(effect.GetType(), fieldName, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(effect) as IList;
                if (list == null || componentIndex < 0 || componentIndex >= list.Count) return RichResult.Fail(ErrorCode.OutOfRange, "component index");
                var comp = list[componentIndex] as Component; list.RemoveAt(componentIndex);
                if (comp != null){ try { UnityEngine.Object.DestroyImmediate(comp); } catch { try { UnityEngine.Object.Destroy(comp); } catch { } } }
                return RichResult.Success();
            }
            catch(Exception ex){ Log.Error("TryRemoveEffectComponent failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        public RichResult TrySetEffectComponentProperty(object item, int effectIndex, string kind, int componentIndex, string propName, object value)
        {
            DebugLog("TrySetEffectComponentProperty effectIndex="+effectIndex+" kind="+kind+" compIndex="+componentIndex+" prop="+propName);
            try
            {
                if (item == null || string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(propName)) return RichResult.Fail(ErrorCode.InvalidArgument, "args");
                var effects = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item) as IList;
                if (effects == null) return RichResult.Fail(ErrorCode.NotSupported, "no Effects list");
                if (effectIndex < 0 || effectIndex >= effects.Count) return RichResult.Fail(ErrorCode.OutOfRange, "index");
                var effect = effects[effectIndex]; var fieldName = kind.Equals("Trigger", StringComparison.OrdinalIgnoreCase) ? "triggers" : kind.Equals("Filter", StringComparison.OrdinalIgnoreCase) ? "filters" : "actions";
                var list = DuckovReflectionCache.GetField(effect.GetType(), fieldName, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(effect) as IList;
                if (list == null || componentIndex < 0 || componentIndex >= list.Count) return RichResult.Fail(ErrorCode.OutOfRange, "component index");
                var comp = list[componentIndex]; var setter = DuckovReflectionCache.GetSetter(comp.GetType(), propName, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
                           ?? DuckovReflectionCache.GetSetter(comp.GetType(), propName.ToLowerInvariant(), BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (setter == null) return RichResult.Fail(ErrorCode.NotSupported, "setter not found");
                setter(comp, value); return RichResult.Success();
            }
            catch(Exception ex){ Log.Error("TrySetEffectComponentProperty failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        public RichResult TryRenameEffect(object item, int effectIndex, string newName)
        {
            try
            {
                if (item == null || string.IsNullOrEmpty(newName)) return RichResult.Fail(ErrorCode.InvalidArgument, "args");
                var effects = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item) as IList;
                if (effects == null) return RichResult.Fail(ErrorCode.NotSupported, "no Effects list");
                if (effectIndex < 0 || effectIndex >= effects.Count) return RichResult.Fail(ErrorCode.OutOfRange, "index");
                var effect = effects[effectIndex] as Component; if (effect == null) return RichResult.Fail(ErrorCode.OperationFailed, "effect null");
                var go = effect.gameObject; if (go != null) go.name = newName;
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryRenameEffect failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        public RichResult TrySetEffectDisplay(object item, int effectIndex, bool display)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item null");
                var effects = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item) as IList;
                if (effects == null) return RichResult.Fail(ErrorCode.NotSupported, "no Effects list");
                if (effectIndex < 0 || effectIndex >= effects.Count) return RichResult.Fail(ErrorCode.OutOfRange, "index");
                var effect = effects[effectIndex];
                DuckovReflectionCache.GetSetter(effect.GetType(), "display", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(effect, display);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TrySetEffectDisplay failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        public RichResult TrySetEffectDescription(object item, int effectIndex, string description)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item null");
                var effects = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item) as IList;
                if (effects == null) return RichResult.Fail(ErrorCode.NotSupported, "no Effects list");
                if (effectIndex < 0 || effectIndex >= effects.Count) return RichResult.Fail(ErrorCode.OutOfRange, "index");
                var effect = effects[effectIndex];
                DuckovReflectionCache.GetSetter(effect.GetType(), "description", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(effect, description);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TrySetEffectDescription failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        public RichResult TryMoveEffect(object item, int fromIndex, int toIndex)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item null");
                var effects = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item) as IList;
                if (effects == null) return RichResult.Fail(ErrorCode.NotSupported, "no Effects list");
                if (fromIndex < 0 || fromIndex >= effects.Count || toIndex < 0 || toIndex >= effects.Count) return RichResult.Fail(ErrorCode.OutOfRange, "index");
                if (fromIndex == toIndex) return RichResult.Success();
                var it = effects[fromIndex]; effects.RemoveAt(fromIndex); effects.Insert(toIndex, it);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryMoveEffect failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        public RichResult TryMoveEffectComponent(object item, int effectIndex, string kind, int fromIndex, int toIndex)
        {
            try
            {
                if (item == null || string.IsNullOrEmpty(kind)) return RichResult.Fail(ErrorCode.InvalidArgument, "args");
                var effects = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item) as IList;
                if (effects == null) return RichResult.Fail(ErrorCode.NotSupported, "no Effects list");
                if (effectIndex < 0 || effectIndex >= effects.Count) return RichResult.Fail(ErrorCode.OutOfRange, "index");
                var effect = effects[effectIndex];
                var fieldName = kind.Equals("Trigger", StringComparison.OrdinalIgnoreCase) ? "triggers" : kind.Equals("Filter", StringComparison.OrdinalIgnoreCase) ? "filters" : "actions";
                var list = DuckovReflectionCache.GetField(effect.GetType(), fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(effect) as IList;
                if (list == null) return RichResult.Fail(ErrorCode.NotSupported, "no component list");
                if (fromIndex < 0 || fromIndex >= list.Count || toIndex < 0 || toIndex >= list.Count) return RichResult.Fail(ErrorCode.OutOfRange, "component index");
                if (fromIndex == toIndex) return RichResult.Success();
                var comp = list[fromIndex]; list.RemoveAt(fromIndex); list.Insert(toIndex, comp);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryMoveEffectComponent failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        public RichResult TrySanitizeEffects(object item)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item null");
                var effects = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item) as IList;
                if (effects == null) return RichResult.Fail(ErrorCode.NotSupported, "no Effects list");
                // remove null effects and non-child effects
                for (int i = effects.Count - 1; i >= 0; i--)
                {
                    var comp = effects[i] as Component;
                    if (comp == null || comp.transform == null || comp.transform.parent == null || comp.transform.parent.gameObject != (DuckovTypeUtils.GetMaybe(item, new[]{"gameObject"}) as GameObject))
                    {
                        effects.RemoveAt(i);
                        try { if (comp != null) UnityEngine.Object.DestroyImmediate(comp.gameObject); } catch { }
                    }
                }
                // clean component lists nulls
                foreach (var e in effects)
                {
                    if (e == null) continue; var et = e.GetType();
                    foreach (var fname in new[] { "triggers", "filters", "actions" })
                    {
                        try
                        {
                            var list = DuckovReflectionCache.GetField(et, fname, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(e) as IList;
                            if (list == null) continue;
                            for (int j = list.Count - 1; j >= 0; j--) if (list[j] == null) list.RemoveAt(j);
                        }
                        catch { }
                    }
                }
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TrySanitizeEffects failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
    }
}
