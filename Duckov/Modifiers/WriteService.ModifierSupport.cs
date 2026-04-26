using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 写入服务（修饰器/反射支持）：
    /// 负责 AddModifier 签名探测、modifier 对象构造、description 字段兼容写入和调试辅助。
    /// </summary>
    internal sealed partial class WriteService : IWriteService
    {
        /// <summary>
        /// AddModifier 反射签名缓存。
        /// 按物品运行时类型缓存数值签名、对象签名以及对应的 modifier/enum 类型，避免重复探测。
        /// </summary>
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, (MethodInfo addNum, MethodInfo addObj, Type modifierType, Type enumType)> s_addSig
            = new System.Collections.Concurrent.ConcurrentDictionary<Type, (MethodInfo, MethodInfo, Type, Type)>();

        /// <summary>
        /// 是否已经对当前进程内的 modifier 写入模式做过一次总探测。
        /// </summary>
        private static bool s_modifierModeProbed;

        /// <summary>
        /// 是否退化为 description-only 写入模式。
        /// 当原生 AddModifier 路径不可用时，后续统一走 modifier description 闭环。
        /// </summary>
        private static bool s_descriptionsOnly;

        /// <summary>
        /// 调试开关：当环境变量 IMK_DEBUG_MODIFIERS 为 1 时启用额外日志。
        /// </summary>
        private static bool Verbose => Environment.GetEnvironmentVariable("IMK_DEBUG_MODIFIERS") == "1";

        /// <summary>
        /// 读取或计算目标物品类型的 AddModifier 反射签名缓存。
        /// </summary>
        /// <param name="itemType">目标物品运行时类型。</param>
        /// <returns>返回数值型或对象型 AddModifier 的可用签名信息。</returns>
        private static (MethodInfo addNum, MethodInfo addObj, Type modifierType, Type enumType) ResolveAddModifierSignatureCached(Type itemType)
        {
            if (itemType == null) return default;
            if (s_addSig.TryGetValue(itemType, out var cached)) return cached;
            if (DuckovBindings.TryGetAddSignature(itemType, out var bound))
            {
                s_addSig[itemType] = bound;
                return bound;
            }

            var sig = DiscoverAddSignature(itemType);
            s_addSig[itemType] = sig;
            DuckovBindings.RecordAddSignature(itemType, sig.addNum, sig.addObj, sig.modifierType, sig.enumType);
            return sig;
        }

        /// <summary>
        /// 在目标物品类型上探测可用的 AddModifier 签名。
        /// 优先寻找 string + float(+bool) 形式，其次寻找 string + modifierObject 形式。
        /// </summary>
        /// <param name="itemType">目标物品运行时类型。</param>
        /// <returns>返回可用签名及其配套类型信息。</returns>
        private static (MethodInfo addNum, MethodInfo addObj, Type modifierType, Type enumType) DiscoverAddSignature(Type itemType)
        {
            MethodInfo addNum = null, addObj = null;
            Type modifierType = null, enumType = null;
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            try
            {
                foreach (var m in itemType.GetMethods(flags))
                {
                    if (!string.Equals(m.Name, "AddModifier", StringComparison.Ordinal)) continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 3 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(float) && ps[2].ParameterType == typeof(bool))
                    {
                        addNum = m;
                        break;
                    }

                    if (ps.Length == 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(float))
                    {
                        addNum = m;
                    }
                }

                if (addNum == null)
                {
                    foreach (var m in itemType.GetMethods(flags))
                    {
                        if (!string.Equals(m.Name, "AddModifier", StringComparison.Ordinal)) continue;
                        var ps = m.GetParameters();
                        if (ps.Length == 2 && ps[0].ParameterType == typeof(string))
                        {
                            var p1 = ps[1].ParameterType;
                            if (p1 != typeof(object))
                            {
                                addObj = m;
                                modifierType = p1;
                                break;
                            }
                        }
                    }

                    if (modifierType != null)
                    {
                        try { enumType = modifierType.GetProperty("Type", flags)?.PropertyType; } catch { }
                        if (enumType == null)
                        {
                            try { enumType = modifierType.GetField("type", flags)?.FieldType; } catch { }
                        }
                    }
                }
            }
            catch
            {
            }

            return (addNum, addObj, modifierType, enumType);
        }

        /// <summary>
        /// 构造运行时 modifier 实例，并尽量补齐 Type/Value/Source 字段。
        /// </summary>
        /// <param name="modifierType">modifier 运行时类型。</param>
        /// <param name="enumType">modifier 类型枚举。</param>
        /// <param name="value">modifier 数值。</param>
        /// <param name="enumVal">期望的枚举值。</param>
        /// <param name="source">来源对象。</param>
        /// <returns>成功返回新实例；失败返回 null。</returns>
        private static object CreateModifierInstance(Type modifierType, Type enumType, float value, object enumVal, object source)
        {
            if (modifierType == null) return null;
            object mod = null;
            try
            {
                if (enumType != null)
                {
                    var ctor3 = modifierType.GetConstructor(new[] { enumType, typeof(float), typeof(object) });
                    if (ctor3 != null) return ctor3.Invoke(new object[] { enumVal, value, source });

                    var ctor5 = modifierType.GetConstructor(new[] { enumType, typeof(float), typeof(bool), typeof(int), typeof(object) });
                    if (ctor5 != null) return ctor5.Invoke(new object[] { enumVal, value, false, 0, source });

                    var ctor2 = modifierType.GetConstructor(new[] { enumType, typeof(float) });
                    if (ctor2 != null) return ctor2.Invoke(new object[] { enumVal, value });
                }

                var dctor = modifierType.GetConstructor(Type.EmptyTypes);
                if (dctor != null) mod = dctor.Invoke(null);
                if (mod == null) mod = Activator.CreateInstance(modifierType);
                if (mod != null)
                {
                    try { DuckovReflectionCache.GetSetter(modifierType, "Type", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(mod, enumVal); } catch { try { modifierType.GetField("type", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(mod, enumVal); } catch { } }
                    try { DuckovReflectionCache.GetSetter(modifierType, "Value", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(mod, value); } catch { try { modifierType.GetField("value", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(mod, value); } catch { } }
                    try { DuckovReflectionCache.GetSetter(modifierType, "Source", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(mod, source); } catch { try { modifierType.GetField("source", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(mod, source); } catch { } }
                }
            }
            catch
            {
            }

            return mod;
        }

        /// <summary>
        /// 首次接触物品时探测其 modifier 写入模式。
        /// 若原生 AddModifier 无法工作，则后续统一降级到 description-only 模式。
        /// </summary>
        /// <param name="item">目标物品。</param>
        private void EnsureModifierModeProbed(object item)
        {
            if (s_modifierModeProbed || item == null) return;
            s_modifierModeProbed = true;
            try
            {
                var sig = ResolveAddModifierSignatureCached(item.GetType());
                var ok = false;
                const string probeKey = "__imk_mod_probe__";

                if (sig.addNum != null)
                {
                    try
                    {
                        var args = sig.addNum.GetParameters().Length == 3
                            ? new object[] { probeKey, 0f, false }
                            : new object[] { probeKey, 0f };
                        var r = sig.addNum.Invoke(item, args);
                        ok = !(r is bool b) || b;
                    }
                    catch
                    {
                        ok = false;
                    }
                }

                if (!ok && sig.addObj != null && sig.modifierType != null)
                {
                    try
                    {
                        object enumVal = null;
                        if (sig.enumType != null)
                        {
                            var names = DuckovReflectionCache.GetEnumNames(sig.enumType);
                            enumVal = names != null && names.Length > 0
                                ? Enum.Parse(sig.enumType, names[0])
                                : Enum.GetValues(sig.enumType).GetValue(0);
                        }

                        var mod = CreateModifierInstance(sig.modifierType, sig.enumType, 0f, enumVal, null);
                        var r = sig.addObj.Invoke(item, new object[] { probeKey, mod });
                        ok = !(r is bool b2) || b2;
                    }
                    catch
                    {
                        ok = false;
                    }
                }

                if (!ok) s_descriptionsOnly = true;
                else
                {
                    try { TryRemoveModifierDescription(item, probeKey); } catch { }
                }

                if (Verbose) DebugLog("Probe modifier mode ok=" + ok + " descriptionsOnly=" + s_descriptionsOnly);
            }
            catch
            {
                s_descriptionsOnly = true;
            }
        }

        /// <summary>
        /// 将外部的 modifier 类型名映射到运行时枚举名。
        /// </summary>
        /// <param name="input">外部输入的类型名。</param>
        /// <param name="names">运行时枚举全部名称。</param>
        /// <returns>尽量贴近原意的枚举项名称。</returns>
        private static string MapModifierEnumName(string input, string[] names)
        {
            if (names == null || names.Length == 0) return input ?? "Add";
            var raw = (input ?? "Add").Trim();
            var isMul = raw.IndexOf("Multiply", StringComparison.OrdinalIgnoreCase) >= 0 || raw.Equals("PercentageMultiply", StringComparison.OrdinalIgnoreCase);
            var isPercAdd = raw.IndexOf("PercentageAdd", StringComparison.OrdinalIgnoreCase) >= 0
                            || raw.IndexOf("Percent", StringComparison.OrdinalIgnoreCase) >= 0 && raw.IndexOf("Add", StringComparison.OrdinalIgnoreCase) >= 0;
            if (isMul)
            {
                return names.FirstOrDefault(n => n.IndexOf("Multiply", StringComparison.OrdinalIgnoreCase) >= 0) ?? names.Last();
            }

            if (isPercAdd)
            {
                return names.FirstOrDefault(n => n.IndexOf("Add", StringComparison.OrdinalIgnoreCase) >= 0 && n.IndexOf("Percent", StringComparison.OrdinalIgnoreCase) >= 0)
                       ?? names.FirstOrDefault(n => n.IndexOf("PercentageAdd", StringComparison.OrdinalIgnoreCase) >= 0)
                       ?? names[0];
            }

            return names.FirstOrDefault(n => string.Equals(n, "Add", StringComparison.OrdinalIgnoreCase))
                   ?? names.FirstOrDefault(n => n.IndexOf("Add", StringComparison.OrdinalIgnoreCase) >= 0)
                   ?? names[0];
        }

        /// <summary>
        /// 更新指定 description 的某个兼容字段，并在成功后重算 modifier。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <param name="key">目标描述键。</param>
        /// <param name="field">逻辑字段名。</param>
        /// <param name="value">待写入的值。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        private RichResult InternalSetDescField(object item, string key, string field, object value)
        {
            try
            {
                var modsCol = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item) as System.Collections.IEnumerable;
                if (modsCol == null) return RichResult.Fail(ErrorCode.NotSupported, "Modifiers collection not found");

                foreach (var d in modsCol)
                {
                    if (d == null) continue;
                    var k = Convert.ToString(DuckovTypeUtils.GetMaybe(d, new[] { "Key", "key" }));
                    if (!string.Equals(k, key, StringComparison.Ordinal)) continue;

                    var dt = d.GetType();
                    var ok = false;
                    if (field == "Value") ok = TrySetByNames(dt, d, new[] { "Value", "value" }, value);
                    else if (field == "Order") ok = TrySetByNames(dt, d, new[] { "Order", "order", "Index" }, value);
                    else if (field == "Display") ok = TrySetByNames(dt, d, new[] { "Display", "display" }, value);
                    else if (field == "Target") ok = TrySetByNames(dt, d, new[] { "Target", "target" }, value);
                    else if (field == "EnableInInventory") ok = TrySetByNames(dt, d, new[] { "EnableInInventory", "enableInInventory" }, value);
                    else ok = TrySetByNames(dt, d, new[] { field, field.ToLowerInvariant() }, value);

                    TryReapplyModifiers(item);
                    return ok ? RichResult.Success() : RichResult.Fail(ErrorCode.OperationFailed, "set failed");
                }

                return RichResult.Fail(ErrorCode.NotFound, "description not found");
            }
            catch (Exception ex)
            {
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>
        /// 兼容设置 description 的 Type 字段，并验证回写结果是否生效。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <param name="key">目标描述键。</param>
        /// <param name="type">期望的新类型名。</param>
        /// <returns>成功返回成功结果；失败时返回对应错误码与错误信息。</returns>
        private RichResult TrySetModifierDescriptionTypeInternal(object item, string key, string type)
        {
            try
            {
                var modsColObj = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item);
                var modsCol = modsColObj as System.Collections.IEnumerable;
                if (modsCol == null) return RichResult.Fail(ErrorCode.NotSupported, "Modifiers collection not found");

                foreach (var d in modsCol)
                {
                    if (d == null) continue;
                    var k = Convert.ToString(DuckovTypeUtils.GetMaybe(d, new[] { "Key", "key", "StatKey", "statKey", "Name", "name" }));
                    if (!string.Equals(k, key, StringComparison.Ordinal)) continue;

                    var dt = d.GetType();
                    var ok = TrySetEnumByAny(dt, d, type);
                    if (!ok) ok = TrySetByNames(dt, d, new[] { "Type", "type" }, type);
                    if (!ok) ok = TrySetAutoPropBackingField(dt, d, "Type", type);
                    if (!ok)
                    {
                        DebugDumpMembers(dt);
                        return RichResult.Fail(ErrorCode.OperationFailed, "set type failed");
                    }

                    TryReapplyModifiers(item);
                    var curType = Convert.ToString(DuckovTypeUtils.GetMaybe(d, new[] { "Type", "type" })) ?? string.Empty;
                    if (!string.Equals(curType, type, StringComparison.OrdinalIgnoreCase))
                    {
                        TrySetByNames(dt, d, new[] { "Type", "type" }, type);
                        TrySetAutoPropBackingField(dt, d, "Type", type);
                        TryReapplyModifiers(item);
                        curType = Convert.ToString(DuckovTypeUtils.GetMaybe(d, new[] { "Type", "type" })) ?? string.Empty;
                    }

                    var success = string.Equals(curType, type, StringComparison.OrdinalIgnoreCase);
                    DebugDumpModifiers(item, "after-set-type");
                    return success ? RichResult.Success() : RichResult.Fail(ErrorCode.OperationFailed, "type verify failed");
                }

                return RichResult.Fail(ErrorCode.NotFound, "description not found");
            }
            catch (Exception ex)
            {
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>
        /// 按多组兼容名称尝试写入属性、字段或自动属性 backing field。
        /// </summary>
        /// <param name="t">目标运行时类型。</param>
        /// <param name="inst">目标实例。</param>
        /// <param name="names">候选成员名称集合。</param>
        /// <param name="val">待写入的值。</param>
        /// <returns>任一路径成功即返回 true；否则返回 false。</returns>
        private static bool TrySetByNames(Type t, object inst, IEnumerable<string> names, object val)
        {
            foreach (var n in names)
            {
                try
                {
                    var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (p != null)
                    {
                        try
                        {
                            object assign = val;
                            var pt = p.PropertyType;
                            if (pt.IsEnum && val is string s)
                            {
                                try { assign = Enum.Parse(pt, s, true); }
                                catch
                                {
                                    try
                                    {
                                        var namesArr = DuckovReflectionCache.GetEnumNames(pt);
                                        var mapped = MapModifierEnumName(s, namesArr);
                                        assign = Enum.Parse(pt, mapped, true);
                                    }
                                    catch
                                    {
                                        assign = Enum.GetValues(pt).GetValue(0);
                                    }
                                }
                            }

                            p.SetValue(inst, assign);
                            return true;
                        }
                        catch
                        {
                        }
                    }

                    var f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                            ?? t.GetField(n.ToLowerInvariant(), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null)
                    {
                        try
                        {
                            object assign = val;
                            var ft = f.FieldType;
                            if (ft.IsEnum && val is string s)
                            {
                                try { assign = Enum.Parse(ft, s, true); }
                                catch
                                {
                                    try
                                    {
                                        var namesArr = DuckovReflectionCache.GetEnumNames(ft);
                                        var mapped = MapModifierEnumName(s, namesArr);
                                        assign = Enum.Parse(ft, mapped, true);
                                    }
                                    catch
                                    {
                                        assign = Enum.GetValues(ft).GetValue(0);
                                    }
                                }
                            }

                            f.SetValue(inst, assign);
                            return true;
                        }
                        catch
                        {
                        }
                    }

                    if (TrySetAutoPropBackingField(t, inst, n, val)) return true;
                }
                catch
                {
                }
            }

            return false;
        }

        /// <summary>
        /// 在目标类型中寻找最像“Type/Kind/Mode”的枚举成员并尝试赋值。
        /// </summary>
        /// <param name="t">目标运行时类型。</param>
        /// <param name="inst">目标实例。</param>
        /// <param name="raw">外部类型名。</param>
        /// <returns>成功写入枚举时返回 true；否则返回 false。</returns>
        private static bool TrySetEnumByAny(Type t, object inst, string raw)
        {
            try
            {
                Func<string, bool> nameHit = n => n.IndexOf("Type", StringComparison.OrdinalIgnoreCase) >= 0
                                                 || n.IndexOf("Kind", StringComparison.OrdinalIgnoreCase) >= 0
                                                 || n.IndexOf("Op", StringComparison.OrdinalIgnoreCase) >= 0
                                                 || n.IndexOf("Mode", StringComparison.OrdinalIgnoreCase) >= 0
                                                 || n.IndexOf("Operation", StringComparison.OrdinalIgnoreCase) >= 0
                                                 || n.IndexOf("Modifier", StringComparison.OrdinalIgnoreCase) >= 0;
                var props = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var prop = props.FirstOrDefault(p => p.PropertyType.IsEnum && nameHit(p.Name)) ?? props.FirstOrDefault(p => p.PropertyType.IsEnum);
                var fldsAll = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var fld = fldsAll.FirstOrDefault(f => f.FieldType.IsEnum && nameHit(f.Name)) ?? fldsAll.FirstOrDefault(f => f.FieldType.IsEnum);
                var et = prop?.PropertyType ?? fld?.FieldType;
                if (et == null) return false;

                object val;
                try
                {
                    var names = DuckovReflectionCache.GetEnumNames(et);
                    var mapped = MapModifierEnumName(raw, names);
                    val = Enum.Parse(et, mapped, true);
                }
                catch
                {
                    try { val = Enum.Parse(et, raw, true); }
                    catch { val = Enum.GetValues(et).GetValue(0); }
                }

                try
                {
                    var setter = DuckovReflectionCache.GetSetter(t, prop?.Name ?? string.Empty, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null && setter != null)
                    {
                        setter(inst, val);
                        return true;
                    }
                }
                catch
                {
                }

                try { if (prop != null) { prop.SetValue(inst, val); return true; } } catch { }
                try { if (fld != null) { fld.SetValue(inst, val); return true; } } catch { }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 仅在调试开关打开时输出修饰器写入日志。
        /// </summary>
        /// <param name="msg">日志文本。</param>
        private void DebugLog(string msg)
        {
            if (Verbose)
            {
                try { UnityEngine.Debug.Log("[IMK.ModWrite] " + msg); } catch { }
            }
        }

        /// <summary>
        /// 输出当前物品上 modifier descriptions 的调试快照。
        /// </summary>
        /// <param name="item">目标物品。</param>
        /// <param name="tag">日志标签。</param>
        private void DebugDumpModifiers(object item, string tag)
        {
            if (!Verbose) return;
            try
            {
                var col = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item) as System.Collections.IEnumerable;
                if (col == null)
                {
                    DebugLog(tag + " modifiers=null");
                    return;
                }

                var i = 0;
                foreach (var m in col)
                {
                    if (m == null) continue;
                    var k = Convert.ToString(DuckovTypeUtils.GetMaybe(m, new[] { "Key", "key" }));
                    var v = DuckovTypeUtils.ConvertToFloat(DuckovTypeUtils.GetMaybe(m, new[] { "Value", "value" }));
                    var t = Convert.ToString(DuckovTypeUtils.GetMaybe(m, new[] { "Type", "type" }));
                    DebugLog(tag + "[" + i + "] key=" + k + " val=" + v + " type=" + t);
                    i++;
                }

                DebugLog(tag + " total=" + i);
            }
            catch (Exception ex)
            {
                DebugLog(tag + " dump exception: " + ex.Message);
            }
        }

        /// <summary>
        /// 输出目标类型的属性与字段清单，用于定位反射写入失败原因。
        /// </summary>
        /// <param name="t">目标运行时类型。</param>
        private static void DebugDumpMembers(Type t)
        {
            try
            {
                if (!Verbose) return;
                var props = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Select(p => p.Name + ":" + p.PropertyType.Name);
                var flds = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Select(f => f.Name + ":" + f.FieldType.Name);
                try { UnityEngine.Debug.Log("[IMK.ModWrite] DescType=" + t.FullName + " props=[" + string.Join(",", props) + "] fields=[" + string.Join(",", flds) + "]"); } catch { }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 按自动属性 backing field 约定尝试写入逻辑字段。
        /// </summary>
        /// <param name="t">目标运行时类型。</param>
        /// <param name="inst">目标实例。</param>
        /// <param name="logicalName">逻辑属性名。</param>
        /// <param name="val">待写入的值。</param>
        /// <returns>写入成功返回 true；否则返回 false。</returns>
        private static bool TrySetAutoPropBackingField(Type t, object inst, string logicalName, object val)
        {
            try
            {
                var fields = t.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                foreach (var f in fields)
                {
                    var n = f.Name;
                    if (n.IndexOf("k__BackingField", StringComparison.Ordinal) >= 0 && n.IndexOf(logicalName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        try
                        {
                            f.SetValue(inst, val);
                            return true;
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }

            return false;
        }
    }
}