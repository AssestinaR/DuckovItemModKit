using System;
using System.Collections.Generic;
using System.Reflection;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 读取服务（Effects）：提供效果树与效果组件的只读查询。
    /// </summary>
    internal sealed partial class ReadService
    {
        /// <summary>读取效果组件（名称 + 是否启用）。</summary>
        public RichResult<EffectEntry[]> TryReadEffects(object item)
        {
            try
            {
                if (item == null) return RichResult<EffectEntry[]>.Fail(ErrorCode.InvalidArgument, "item is null");
                var list = new List<EffectEntry>();
                try
                {
                    var effects = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item) as System.Collections.IEnumerable;
                    if (effects != null)
                    {
                        foreach (var e in effects)
                        {
                            if (e == null) continue;
                            string name = null; bool enabled = false;
                            try
                            {
                                var go = DuckovTypeUtils.GetMaybe(e, new[] { "gameObject" }) as UnityEngine.GameObject;
                                if (go != null)
                                {
                                    name = go.name;
                                }
                            }
                            catch { name = e.GetType().Name; }
                            try
                            {
                                if (e is UnityEngine.Behaviour behaviour) enabled = behaviour.enabled;
                            }
                            catch { }
                            list.Add(new EffectEntry { Name = name ?? e.GetType().Name, Enabled = enabled });
                        }
                    }
                }
                catch { }
                return RichResult<EffectEntry[]>.Success(list.ToArray());
            }
            catch (Exception ex)
            {
                Log.Error("TryReadEffects failed", ex);
                return RichResult<EffectEntry[]>.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>读取效果组件的详细信息（显示/描述/触发/过滤/动作 类型）。</summary>
        public RichResult<EffectInfo[]> TryReadEffectsDetailed(object item)
        {
            try
            {
                if (item == null) return RichResult<EffectInfo[]>.Fail(ErrorCode.InvalidArgument, "item is null");
                var list = new List<EffectInfo>();
                var effectsEnum = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item) as System.Collections.IEnumerable;
                if (effectsEnum != null)
                {
                    foreach (var e in effectsEnum)
                    {
                        if (e == null) continue;
                        var info = new EffectInfo();
                        try { var go = DuckovTypeUtils.GetMaybe(e, new[] { "gameObject" }) as UnityEngine.GameObject; info.Name = go != null ? go.name : e.GetType().Name; } catch { info.Name = e.GetType().Name; }
                        try { var en = DuckovTypeUtils.GetMaybe(e, new[] { "enabled" }); if (en != null) info.Enabled = Convert.ToBoolean(en); } catch { }
                        try { var d = DuckovTypeUtils.GetMaybe(e, new[] { "display", "Display" }); if (d != null) info.Display = Convert.ToBoolean(d); } catch { }
                        try { var desc = DuckovTypeUtils.GetMaybe(e, new[] { "description", "Description" }); if (desc != null) info.Description = Convert.ToString(desc); } catch { }
                        try
                        {
                            var ts = DuckovTypeUtils.GetMaybe(e, new[] { "triggers", "Triggers" }) as System.Collections.IEnumerable;
                            var types = new List<string>();
                            if (ts != null) foreach (var t in ts) if (t != null) types.Add(t.GetType().FullName);
                            info.TriggerTypes = types.ToArray();
                        }
                        catch { info.TriggerTypes = Array.Empty<string>(); }
                        try
                        {
                            var fs = DuckovTypeUtils.GetMaybe(e, new[] { "filters", "Filters" }) as System.Collections.IEnumerable;
                            var types = new List<string>();
                            if (fs != null) foreach (var f in fs) if (f != null) types.Add(f.GetType().FullName);
                            info.FilterTypes = types.ToArray();
                        }
                        catch { info.FilterTypes = Array.Empty<string>(); }
                        try
                        {
                            var ac = DuckovTypeUtils.GetMaybe(e, new[] { "actions", "Actions" }) as System.Collections.IEnumerable;
                            var types = new List<string>();
                            if (ac != null) foreach (var a in ac) if (a != null) types.Add(a.GetType().FullName);
                            info.ActionTypes = types.ToArray();
                        }
                        catch { info.ActionTypes = Array.Empty<string>(); }
                        list.Add(info);
                    }
                }
                return RichResult<EffectInfo[]>.Success(list.ToArray());
            }
            catch (Exception ex)
            {
                Log.Error("TryReadEffectsDetailed failed", ex);
                return RichResult<EffectInfo[]>.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>读取效果组件的深度信息，包括触发器/过滤器/动作的基本属性。</summary>
        public RichResult<EffectDetails[]> TryReadEffectsDeep(object item)
        {
            try
            {
                if (item == null) return RichResult<EffectDetails[]>.Fail(ErrorCode.InvalidArgument, "item is null");
                var list = new List<EffectDetails>();
                var effectsEnum = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item) as System.Collections.IEnumerable;
                if (effectsEnum != null)
                {
                    foreach (var e in effectsEnum)
                    {
                        if (e == null) continue;
                        var det = new EffectDetails();
                        try { var go = DuckovTypeUtils.GetMaybe(e, new[] { "gameObject" }) as UnityEngine.GameObject; det.Name = go != null ? go.name : e.GetType().Name; } catch { det.Name = e.GetType().Name; }
                        try { var en = DuckovTypeUtils.GetMaybe(e, new[] { "enabled" }); if (en != null) det.Enabled = Convert.ToBoolean(en); } catch { }
                        try { var d = DuckovTypeUtils.GetMaybe(e, new[] { "display", "Display" }); if (d != null) det.Display = Convert.ToBoolean(d); } catch { }
                        try { var desc = DuckovTypeUtils.GetMaybe(e, new[] { "description", "Description" }); if (desc != null) det.Description = Convert.ToString(desc); } catch { }
                        det.Triggers = ReadComponents(e, "triggers", "Trigger");
                        det.Filters = ReadComponents(e, "filters", "Filter");
                        det.Actions = ReadComponents(e, "actions", "Action");
                        list.Add(det);
                    }
                }
                return RichResult<EffectDetails[]>.Success(list.ToArray());
            }
            catch (Exception ex)
            {
                Log.Error("TryReadEffectsDeep failed", ex);
                return RichResult<EffectDetails[]>.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        private static EffectComponentDetails[] ReadComponents(object effect, string fieldName, string kind)
        {
            try
            {
                var et = effect.GetType();
                var list = DuckovReflectionCache.GetField(et, fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(effect) as System.Collections.IEnumerable
                           ?? DuckovTypeUtils.GetMaybe(effect, new[] { fieldName, char.ToUpperInvariant(fieldName[0]) + fieldName.Substring(1) }) as System.Collections.IEnumerable;
                if (list == null) return Array.Empty<EffectComponentDetails>();
                var res = new List<EffectComponentDetails>();
                foreach (var c in list)
                {
                    if (c == null) continue;
                    var dto = new EffectComponentDetails { Kind = kind, Type = c.GetType().FullName, Properties = new Dictionary<string, object>() };
                    try
                    {
                        var ct = c.GetType();
                        foreach (var p in ct.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                        {
                            if (!DuckovEffectSchemaSupport.ShouldCaptureProperty(p)) continue;
                            try { dto.Properties[p.Name] = p.GetValue(c, null); } catch { }
                        }
                        foreach (var f in ct.GetFields(BindingFlags.Public | BindingFlags.Instance))
                        {
                            if (!DuckovEffectSchemaSupport.ShouldCaptureField(f)) continue;
                            try { dto.Properties[f.Name] = f.GetValue(c); } catch { }
                        }
                        foreach (var f in ct.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            if (!DuckovEffectSchemaSupport.ShouldCaptureField(f)) continue;
                            try { dto.Properties[f.Name] = f.GetValue(c); } catch { }
                        }
                    }
                    catch { }
                    res.Add(dto);
                }
                return res.ToArray();
            }
            catch { return Array.Empty<EffectComponentDetails>(); }
        }
    }
}