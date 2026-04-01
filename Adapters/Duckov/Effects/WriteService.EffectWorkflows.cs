using System;
using System.Reflection;
using ItemModKit.Core;
using UnityEngine;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 写入服务（Effects Workflows）：effect graph 的结构型工作流实现。
    /// </summary>
    internal sealed partial class WriteService : IWriteService
    {
        public RichResult TryAddEffect(object item, string effectTypeFullName, EffectCreateOptions options = null)
        {
            DebugLog("TryAddEffect type=" + effectTypeFullName);
            try
            {
                if (item == null || string.IsNullOrEmpty(effectTypeFullName)) return RichResult.Fail(ErrorCode.InvalidArgument, "args");
                var effType = DuckovTypeUtils.FindType(effectTypeFullName); if (effType == null) return RichResult.Fail(ErrorCode.NotFound, "effect type not found");
                var go = DuckovTypeUtils.GetMaybe(item, new[] { "gameObject" }) as GameObject; if (go == null) return RichResult.Fail(ErrorCode.NotSupported, "item has no gameObject");
                var child = new GameObject(options?.Name ?? ("New " + effType.Name)); child.hideFlags = HideFlags.HideInInspector; child.transform.SetParent(go.transform, false);
                var effect = child.AddComponent(effType);
                if (options != null)
                {
                    if (options.Display.HasValue) { try { TryAssignMember(effect, "display", options.Display.Value); } catch { } }
                    if (!string.IsNullOrEmpty(options.Description)) { try { TryAssignMember(effect, "description", options.Description); } catch { } }
                    if (options.Enabled.HasValue) { try { ((Behaviour)effect).enabled = options.Enabled.Value; } catch { } }
                }

                if (!TryAddToEffectsList(item, effect)) return RichResult.Fail(ErrorCode.OperationFailed, "failed to add effect to list");
                TryBindEffectToItem(effect, effType, item);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryAddEffect failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        public RichResult TryAddEffect(object item, string effectTypeFullName) => TryAddEffect(item, effectTypeFullName, null);

        public RichResult TryRemoveEffect(object item, int effectIndex)
        {
            DebugLog("TryRemoveEffect index=" + effectIndex);
            try
            {
                var effects = TryGetEffectsList(item);
                if (effects == null) return RichResult.Fail(item == null ? ErrorCode.InvalidArgument : ErrorCode.NotSupported, item == null ? "item is null" : "no Effects list");
                if (effectIndex < 0 || effectIndex >= effects.Count) return RichResult.Fail(ErrorCode.OutOfRange, "index");
                var effect = effects[effectIndex] as Component;
                effects.RemoveAt(effectIndex);
                if (effect != null) { try { UnityEngine.Object.DestroyImmediate(effect.gameObject); } catch { try { UnityEngine.Object.Destroy(effect.gameObject); } catch { } } }
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryRemoveEffect failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        public RichResult TryEnableEffect(object item, int effectIndex, bool enabled)
        {
            DebugLog("TryEnableEffect index=" + effectIndex + " enabled=" + enabled);
            try
            {
                var effect = TryGetEffectAt(item, effectIndex, out var error) as Behaviour;
                if (!error.Ok) return error;
                if (effect == null) return RichResult.Fail(ErrorCode.NotSupported, "effect not Behaviour");
                effect.enabled = enabled;
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryEnableEffect failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        public RichResult TrySetEffectProperty(object item, int effectIndex, string propName, object value)
        {
            DebugLog("TrySetEffectProperty index=" + effectIndex + " prop=" + propName);
            try
            {
                if (item == null || string.IsNullOrEmpty(propName)) return RichResult.Fail(ErrorCode.InvalidArgument, "args");
                var effect = TryGetEffectAt(item, effectIndex, out var error);
                if (!error.Ok) return error;
                return TryAssignMember(effect, propName, value)
                    ? RichResult.Success()
                    : RichResult.Fail(ErrorCode.NotSupported, "setter not found");
            }
            catch (Exception ex) { Log.Error("TrySetEffectProperty failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        public RichResult TryAddEffectComponent(object item, int effectIndex, string componentTypeFullName, string kind)
        {
            DebugLog("TryAddEffectComponent index=" + effectIndex + " kind=" + kind + " type=" + componentTypeFullName);
            try
            {
                if (item == null || string.IsNullOrEmpty(componentTypeFullName) || string.IsNullOrEmpty(kind)) return RichResult.Fail(ErrorCode.InvalidArgument, "args");
                var effect = TryGetEffectAt(item, effectIndex, out var error) as Component;
                if (!error.Ok) return error;
                if (effect == null) return RichResult.Fail(ErrorCode.OperationFailed, "effect null");
                var type = DuckovTypeUtils.FindType(componentTypeFullName); if (type == null) return RichResult.Fail(ErrorCode.NotFound, "component type not found");
                var comp = effect.gameObject.AddComponent(type);
                var add = DuckovReflectionCache.GetMethod(effect.GetType(), "AddEffectComponent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { DuckovTypeUtils.FindType("ItemStatsSystem.EffectComponent") ?? typeof(Component) })
                          ?? DuckovReflectionCache.GetMethod(effect.GetType(), "AddEffectComponent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (add != null) add.Invoke(effect, new[] { comp });
                else
                {
                    var list = TryGetEffectComponents(effect, kind);
                    if (list == null) return RichResult.Fail(ErrorCode.NotSupported, "no component list");
                    list.Add(comp);
                }
                TryAssignComponentMaster(type, comp, effect);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryAddEffectComponent failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        public RichResult TryRemoveEffectComponent(object item, int effectIndex, string kind, int componentIndex)
        {
            DebugLog("TryRemoveEffectComponent effectIndex=" + effectIndex + " kind=" + kind + " compIndex=" + componentIndex);
            try
            {
                if (item == null || string.IsNullOrEmpty(kind)) return RichResult.Fail(ErrorCode.InvalidArgument, "args");
                var effect = TryGetEffectAt(item, effectIndex, out var error) as Component;
                if (!error.Ok) return error;
                if (effect == null) return RichResult.Fail(ErrorCode.OperationFailed, "effect null");
                var list = TryGetEffectComponents(effect, kind);
                if (list == null || componentIndex < 0 || componentIndex >= list.Count) return RichResult.Fail(ErrorCode.OutOfRange, "component index");
                var comp = list[componentIndex] as Component;
                list.RemoveAt(componentIndex);
                if (comp != null) { try { UnityEngine.Object.DestroyImmediate(comp); } catch { try { UnityEngine.Object.Destroy(comp); } catch { } } }
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryRemoveEffectComponent failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        public RichResult TrySetEffectComponentProperty(object item, int effectIndex, string kind, int componentIndex, string propName, object value)
        {
            DebugLog("TrySetEffectComponentProperty effectIndex=" + effectIndex + " kind=" + kind + " compIndex=" + componentIndex + " prop=" + propName);
            try
            {
                if (item == null || string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(propName)) return RichResult.Fail(ErrorCode.InvalidArgument, "args");
                var effect = TryGetEffectAt(item, effectIndex, out var error);
                if (!error.Ok) return error;
                var list = TryGetEffectComponents(effect, kind);
                if (list == null || componentIndex < 0 || componentIndex >= list.Count) return RichResult.Fail(ErrorCode.OutOfRange, "component index");
                var comp = list[componentIndex];
                return TryAssignMember(comp, propName, value)
                    ? RichResult.Success()
                    : RichResult.Fail(ErrorCode.NotSupported, "setter not found");
            }
            catch (Exception ex) { Log.Error("TrySetEffectComponentProperty failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        public RichResult TryRenameEffect(object item, int effectIndex, string newName)
        {
            try
            {
                if (item == null || string.IsNullOrEmpty(newName)) return RichResult.Fail(ErrorCode.InvalidArgument, "args");
                var effect = TryGetEffectAt(item, effectIndex, out var error) as Component;
                if (!error.Ok) return error;
                if (effect == null) return RichResult.Fail(ErrorCode.OperationFailed, "effect null");
                var go = effect.gameObject; if (go != null) go.name = newName;
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryRenameEffect failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        public RichResult TrySetEffectDisplay(object item, int effectIndex, bool display)
        {
            try
            {
                var effect = TryGetEffectAt(item, effectIndex, out var error);
                if (!error.Ok) return error;
                return TryAssignMember(effect, "display", display)
                    ? RichResult.Success()
                    : RichResult.Fail(ErrorCode.NotSupported, "display not found");
            }
            catch (Exception ex) { Log.Error("TrySetEffectDisplay failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        public RichResult TrySetEffectDescription(object item, int effectIndex, string description)
        {
            try
            {
                var effect = TryGetEffectAt(item, effectIndex, out var error);
                if (!error.Ok) return error;
                return TryAssignMember(effect, "description", description)
                    ? RichResult.Success()
                    : RichResult.Fail(ErrorCode.NotSupported, "description not found");
            }
            catch (Exception ex) { Log.Error("TrySetEffectDescription failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        public RichResult TryMoveEffect(object item, int fromIndex, int toIndex)
        {
            try
            {
                var effects = TryGetEffectsList(item);
                if (effects == null) return RichResult.Fail(item == null ? ErrorCode.InvalidArgument : ErrorCode.NotSupported, item == null ? "item null" : "no Effects list");
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
                var effect = TryGetEffectAt(item, effectIndex, out var error);
                if (!error.Ok) return error;
                var list = TryGetEffectComponents(effect, kind);
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
                var effects = TryGetEffectsList(item);
                if (effects == null) return RichResult.Fail(item == null ? ErrorCode.InvalidArgument : ErrorCode.NotSupported, item == null ? "item null" : "no Effects list");
                for (int i = effects.Count - 1; i >= 0; i--)
                {
                    var comp = effects[i] as Component;
                    if (comp == null || comp.transform == null || comp.transform.parent == null || comp.transform.parent.gameObject != (DuckovTypeUtils.GetMaybe(item, new[] { "gameObject" }) as GameObject))
                    {
                        effects.RemoveAt(i);
                        try { if (comp != null) UnityEngine.Object.DestroyImmediate(comp.gameObject); } catch { }
                    }
                }
                foreach (var e in effects)
                {
                    if (e == null) continue;
                    foreach (var kind in new[] { "Trigger", "Filter", "Action" })
                    {
                        try
                        {
                            var list = TryGetEffectComponents(e, kind);
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