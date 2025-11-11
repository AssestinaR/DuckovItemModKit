using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 写入服务（修饰器与效果）：
    /// - 修饰器：适配 AddModifier 的多种签名，必要时构造 Modifier 实例或回退到描述集合
    /// - 效果：增删启用、子组件增删、属性设置
    /// </summary>
    internal sealed partial class WriteService : IWriteService
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, (MethodInfo addNum, MethodInfo addObj, Type modifierType, Type enumType)> s_addSig
            = new System.Collections.Concurrent.ConcurrentDictionary<Type, (MethodInfo, MethodInfo, Type, Type)>();

        private static (MethodInfo addNum, MethodInfo addObj, Type modifierType, Type enumType) ResolveAddModifierSignatureCached(Type itemType)
        {
            if (itemType == null) return default;
            if (s_addSig.TryGetValue(itemType, out var cached)) return cached;
            if (DuckovBindings.TryGetAddSignature(itemType, out var bound)) { s_addSig[itemType] = bound; return bound; }
            var sig = DiscoverAddSignature(itemType); s_addSig[itemType] = sig; DuckovBindings.RecordAddSignature(itemType, sig.addNum, sig.addObj, sig.modifierType, sig.enumType); return sig;
        }
        private static (MethodInfo addNum, MethodInfo addObj, Type modifierType, Type enumType) DiscoverAddSignature(Type itemType)
        {
            MethodInfo addNum = null, addObj = null; Type modifierType = null, enumType = null;
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            try
            {
                foreach (var m in itemType.GetMethods(flags))
                {
                    if (!string.Equals(m.Name, "AddModifier", StringComparison.Ordinal)) continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 3 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(float) && ps[2].ParameterType == typeof(bool)) { addNum = m; break; }
                    if (ps.Length == 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(float)) { addNum = m; }
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
                            if (p1 != typeof(object)) { addObj = m; modifierType = p1; break; }
                        }
                    }
                    if (modifierType != null)
                    {
                        try { enumType = modifierType.GetProperty("Type", flags)?.PropertyType; } catch { }
                        if (enumType == null) { try { enumType = modifierType.GetField("type", flags)?.FieldType; } catch { } }
                    }
                }
            }
            catch { }
            return (addNum, addObj, modifierType, enumType);
        }
        private static object CreateModifierInstance(Type modifierType, Type enumType, float value, object enumVal, object source)
        {
            if (modifierType == null) return null; object mod = null; try
            {
                if (enumType != null)
                {
                    var ctor3 = modifierType.GetConstructor(new[] { enumType, typeof(float), typeof(object) }); if (ctor3 != null) return ctor3.Invoke(new object[] { enumVal, value, source });
                    var ctor5 = modifierType.GetConstructor(new[] { enumType, typeof(float), typeof(bool), typeof(int), typeof(object) }); if (ctor5 != null) return ctor5.Invoke(new object[] { enumVal, value, false, 0, source });
                    var ctor2 = modifierType.GetConstructor(new[] { enumType, typeof(float) }); if (ctor2 != null) return ctor2.Invoke(new object[] { enumVal, value });
                }
                var dctor = modifierType.GetConstructor(Type.EmptyTypes); if (dctor != null) mod = dctor.Invoke(null); if (mod == null) mod = Activator.CreateInstance(modifierType);
                if (mod != null)
                {
                    try { DuckovReflectionCache.GetSetter(modifierType, "Type", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(mod, enumVal); } catch { try { modifierType.GetField("type", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(mod, enumVal); } catch { } }
                    try { DuckovReflectionCache.GetSetter(modifierType, "Value", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(mod, value); } catch { try { modifierType.GetField("value", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(mod, value); } catch { } }
                    try { DuckovReflectionCache.GetSetter(modifierType, "Source", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(mod, source); } catch { try { modifierType.GetField("source", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(mod, source); } catch { } }
                }
            }
            catch { }
            return mod;
        }
        private static string MapModifierEnumName(string input, string[] names)
        {
            if (names == null || names.Length == 0) return input ?? "Add"; string s = input ?? "Add"; string pick = null;
            if (s.IndexOf("PercentageMultiply", StringComparison.OrdinalIgnoreCase) >= 0 || s.Equals("Multiply", StringComparison.OrdinalIgnoreCase) || s.Equals("Mul", StringComparison.OrdinalIgnoreCase)) pick = names.FirstOrDefault(n => n.IndexOf("Multiply", StringComparison.OrdinalIgnoreCase) >= 0) ?? names[0];
            else if (s.IndexOf("PercentageAdd", StringComparison.OrdinalIgnoreCase) >= 0 || s.Equals("AddPercent", StringComparison.OrdinalIgnoreCase) || s.Equals("PAdd", StringComparison.OrdinalIgnoreCase)) pick = names.FirstOrDefault(n => n.IndexOf("Add", StringComparison.OrdinalIgnoreCase) >= 0 && n.IndexOf("Percent", StringComparison.OrdinalIgnoreCase) >= 0) ?? names.FirstOrDefault(n => n.IndexOf("PercentageAdd", StringComparison.OrdinalIgnoreCase) >= 0) ?? names[0];
            else pick = names.FirstOrDefault(n => string.Equals(n, "Add", StringComparison.OrdinalIgnoreCase)) ?? names.FirstOrDefault(n => n.IndexOf("Add", StringComparison.OrdinalIgnoreCase) >= 0) ?? names[0];
            return pick ?? names[0];
        }
        /// <summary>添加修饰器：自动适配不同签名；失败则回退到描述集合。</summary>
        public RichResult TryAddModifier(object item, string statKey, float value, bool isPercent = false, string type = null, object source = null)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null"); if (string.IsNullOrEmpty(statKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "statKey is null");
                TryEnsureStat(item, statKey, null); var sig = ResolveAddModifierSignatureCached(item.GetType()); bool applied = false;
                if (sig.addNum != null)
                {
                    var args = sig.addNum.GetParameters().Length == 3 ? new object[] { statKey, value, isPercent } : new object[] { statKey, value }; try { var okObj = sig.addNum.Invoke(item, args); applied = !(okObj is bool) || (okObj is bool b && b); } catch { applied = false; }
                }
                if (!applied && sig.addObj != null && sig.modifierType != null)
                {
                    object enumVal = null; if (sig.enumType != null) { try { var names = DuckovReflectionCache.GetEnumNames(sig.enumType); string mapped = MapModifierEnumName(type, names); enumVal = Enum.Parse(sig.enumType, mapped); } catch { enumVal = sig.enumType.IsEnum ? Enum.GetValues(sig.enumType).GetValue(0) : null; } }
                    var mod = CreateModifierInstance(sig.modifierType, sig.enumType, value, enumVal, source); if (mod != null)
                    {
                        try { DuckovReflectionCache.GetMethod(item.GetType(), "RemoveAllModifiersFrom", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item, new object[] { source }); } catch { }
                        try { var okObj = sig.addObj.Invoke(item, new object[] { statKey, mod }); applied = !(okObj is bool) || (okObj is bool b && b); } catch { applied = false; }
                        if (!applied)
                        {
                            try
                            {
                                var statsObj = DuckovReflectionCache.GetGetter(item.GetType(), "Stats", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item); object stat = null;
                                var indexer = DuckovReflectionCache.GetMethod(statsObj?.GetType(), "get_Item", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ typeof(string) }); if (indexer != null) stat = indexer.Invoke(statsObj, new object[]{ statKey });
                                if (stat == null)
                                {
                                    var getStat = DuckovReflectionCache.GetMethod(statsObj?.GetType(), "GetStat", BindingFlags.Public|BindingFlags.NonPublic | BindingFlags.Instance, new[]{ typeof(string) }); if (getStat != null) stat = getStat.Invoke(statsObj, new object[]{ statKey });
                                }
                                if (stat != null)
                                {
                                    var statAdd = DuckovReflectionCache.GetMethod(stat.GetType(), "AddModifier", BindingFlags.Public|BindingFlags.NonPublic | BindingFlags.Instance, new[]{ sig.modifierType })
                                                 ?? DuckovReflectionCache.GetMethod(stat.GetType(), "AddModifier", BindingFlags.Public|BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (statAdd != null) { statAdd.Invoke(stat, new object[] { mod }); applied = true; }
                                }
                            }
                            catch { applied = false; }
                        }
                    }
                }
                if (!applied)
                {
                    try
                    {
                        var modsCol = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item);
                        if (modsCol != null)
                        {
                            Type descType = modsCol.GetType().GetGenericArguments().FirstOrDefault() ?? DuckovTypeUtils.FindType("ItemStatsSystem.ModifierDescription");
                            if (descType != null)
                            {
                                var inst = Activator.CreateInstance(descType); DuckovReflectionCache.GetSetter(descType, "Key", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(inst, statKey);
                                if (sig.enumType != null)
                                {
                                    try { var names = DuckovReflectionCache.GetEnumNames(sig.enumType); string mapped = MapModifierEnumName(type, names); var e = Enum.Parse(sig.enumType, mapped); DuckovReflectionCache.GetSetter(descType, "Type", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(inst, e); } catch { }
                                }
                                DuckovReflectionCache.GetSetter(descType, "Value", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(inst, value);
                                var addDesc = DuckovReflectionCache.GetMethod(modsCol.GetType(), "Add", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ descType })
                                              ?? DuckovReflectionCache.GetMethod(modsCol.GetType(), "Add", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                                addDesc?.Invoke(modsCol, new[] { inst });
                                var reapplyCol = DuckovReflectionCache.GetMethod(modsCol.GetType(), "ReapplyModifiers", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance); reapplyCol?.Invoke(modsCol, null); applied = true;
                            }
                        }
                    }
                    catch { applied = false; }
                }
                if (applied) { TryReapplyModifiers(item); return RichResult.Success(); }
                return RichResult.Fail(ErrorCode.OperationFailed, "AddModifier(object) returned false");
            }
            catch (Exception ex) { Log.Error("TryAddModifier failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>移除来源为 source 的全部修饰器。</summary>
        public RichResult<int> TryRemoveAllModifiersFromSource(object item, object source)
        {
            try
            {
                if (item == null) return RichResult<int>.Fail(ErrorCode.InvalidArgument, "item is null"); var rem = DuckovReflectionCache.GetMethod(item.GetType(), "RemoveAllModifiersFrom", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (rem != null)
                {
                    try { var r = rem.Invoke(item, new object[] { source }); int count = 0; if (r is int i) count = i; TryReapplyModifiers(item); return RichResult<int>.Success(count); } catch (Exception ex) { return RichResult<int>.Fail(ErrorCode.OperationFailed, ex.Message); }
                }
                int removed = 0; var modsCol = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item); if (modsCol == null) return RichResult<int>.Fail(ErrorCode.NotSupported, "Modifiers collection not found");
                var list = modsCol as System.Collections.IEnumerable; var toRemove = new List<object>(); if (list != null)
                {
                    foreach (var m in list) { if (m == null) continue; object src = null; try { src = DuckovTypeUtils.GetMaybe(m, new[] { "Source", "source" }); } catch { } if (ReferenceEquals(src, source)) toRemove.Add(m); }
                }
                var remove = DuckovReflectionCache.GetMethod(modsCol.GetType(), "Remove", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                           ?? DuckovReflectionCache.GetMethod(modsCol.GetType(), "RemoveModifier", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var m in toRemove) { try { remove?.Invoke(modsCol, new[] { m }); removed++; } catch { } }
                TryReapplyModifiers(item); return RichResult<int>.Success(removed);
            }
            catch (Exception ex) { Log.Error("TryRemoveAllModifiersFromSource failed", ex); return RichResult<int>.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>重新应用修饰器（如果没有 Stats 则直接视为成功）。</summary>
        public RichResult TryReapplyModifiers(object item)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null"); bool statsIsNull = false;
                try { var p = item.GetType().GetProperty("Stats", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance); var stats = p?.GetValue(item, null); statsIsNull = stats == null; } catch { }
                if (statsIsNull) return RichResult.Success(); _item.ReapplyModifiers(item); return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryReapplyModifiers failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        // Modifier descriptions（描述集合相关 API）
        /// <summary>添加修饰器描述项。</summary>
        public RichResult TryAddModifierDescription(object item, string key, string type, float value, bool? display = null, int? order = null, string target = null)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var modsCol = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item);
                if (modsCol == null) return RichResult.Fail(ErrorCode.NotSupported, "Modifiers collection not found");
                var descType = modsCol.GetType().GetGenericArguments().FirstOrDefault() ?? DuckovTypeUtils.FindType("ItemStatsSystem.ModifierDescription");
                if (descType == null) return RichResult.Fail(ErrorCode.NotSupported, "ModifierDescription type missing");
                if (string.IsNullOrEmpty(key)) return RichResult.Fail(ErrorCode.InvalidArgument, "key is empty");
                var inst = Activator.CreateInstance(descType);
                try { DuckovReflectionCache.GetSetter(descType, "Key", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(inst, key); } catch { }
                if (!string.IsNullOrEmpty(type))
                {
                    try
                    {
                        var typeProp = descType.GetProperty("Type", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                        var enumType = typeProp?.PropertyType;
                        if (enumType != null)
                        {
                            var names = DuckovReflectionCache.GetEnumNames(enumType);
                            var mapped = MapModifierEnumName(type, names);
                            var e = Enum.Parse(enumType, mapped);
                            DuckovReflectionCache.GetSetter(descType, "Type", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(inst, e);
                        }
                    }
                    catch { }
                }
                try { DuckovReflectionCache.GetSetter(descType, "Value", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(inst, value); } catch { }
                if (order.HasValue) { try { DuckovReflectionCache.GetSetter(descType, "Order", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(inst, order.Value); } catch { } }
                if (display.HasValue) { try { DuckovReflectionCache.GetSetter(descType, "Display", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(inst, display.Value); } catch { } }
                if (!string.IsNullOrEmpty(target)) { try { DuckovReflectionCache.GetSetter(descType, "Target", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(inst, target); } catch { } }
                var add = DuckovReflectionCache.GetMethod(modsCol.GetType(), "Add", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ descType })
                          ?? DuckovReflectionCache.GetMethod(modsCol.GetType(), "Add", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                add?.Invoke(modsCol, new[]{ inst });
                var reapply = DuckovReflectionCache.GetMethod(modsCol.GetType(), "ReapplyModifiers", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                reapply?.Invoke(modsCol, null);
                TryReapplyModifiers(item);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryAddModifierDescription failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>移除修饰器描述项。</summary>
        public RichResult TryRemoveModifierDescription(object item, string key)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var modsCol = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item);
                if (modsCol == null) return RichResult.Fail(ErrorCode.NotSupported, "Modifiers collection not found");
                object targetDesc = null;
                var en = modsCol as System.Collections.IEnumerable;
                if (en != null)
                {
                    foreach (var d in en)
                    {
                        if (d == null) continue;
                        var k = Convert.ToString(DuckovTypeUtils.GetMaybe(d, new[]{"Key","key"}));
                        if (string.Equals(k, key, StringComparison.Ordinal)) { targetDesc = d; break; }
                    }
                }
                if (targetDesc == null) return RichResult.Fail(ErrorCode.NotFound, "description not found");
                var remove = DuckovReflectionCache.GetMethod(modsCol.GetType(), "Remove", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ targetDesc.GetType() })
                              ?? DuckovReflectionCache.GetMethod(modsCol.GetType(), "Remove", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                remove?.Invoke(modsCol, new[]{ targetDesc });
                var reapply = DuckovReflectionCache.GetMethod(modsCol.GetType(), "ReapplyModifiers", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                reapply?.Invoke(modsCol, null);
                TryReapplyModifiers(item);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryRemoveModifierDescription failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>设置描述项的数值。</summary>
        public RichResult TrySetModifierDescriptionValue(object item, string key, float value)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var modsCol = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item) as System.Collections.IEnumerable;
                if (modsCol == null) return RichResult.Fail(ErrorCode.NotSupported, "Modifiers collection not found");
                foreach (var d in modsCol)
                {
                    if (d == null) continue; var k = Convert.ToString(DuckovTypeUtils.GetMaybe(d, new[]{"Key","key"})); if (!string.Equals(k, key, StringComparison.Ordinal)) continue;
                    var setter = DuckovReflectionCache.GetSetter(d.GetType(), "Value", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                    setter?.Invoke(d, value);
                    var reapply = DuckovReflectionCache.GetMethod(d.GetType().DeclaringType ?? d.GetType(), "ReapplyModifiers", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                    // fallback: on collection
                    var modsColObj = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item);
                    if (reapply == null) reapply = DuckovReflectionCache.GetMethod(modsColObj?.GetType(), "ReapplyModifiers", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                    reapply?.Invoke(modsColObj, null);
                    TryReapplyModifiers(item);
                    return RichResult.Success();
                }
                return RichResult.Fail(ErrorCode.NotFound, "description not found");
            }
            catch (Exception ex) { Log.Error("TrySetModifierDescriptionValue failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>设置描述项的类型（枚举名支持模糊映射，如 AddPercent/Mul）。</summary>
        public RichResult TrySetModifierDescriptionType(object item, string key, string type)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var modsCol = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item) as System.Collections.IEnumerable;
                if (modsCol == null) return RichResult.Fail(ErrorCode.NotSupported, "Modifiers collection not found");
                foreach (var d in modsCol)
                {
                    if (d == null) continue;
                    var k = Convert.ToString(DuckovTypeUtils.GetMaybe(d, new[]{"Key","key"}));
                    if (!string.Equals(k, key, StringComparison.Ordinal)) continue;
                    Type enumType = null;
                    var tp = d.GetType().GetProperty("Type", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                    if (tp != null) enumType = tp.PropertyType;
                    if (enumType == null)
                    {
                        var tf = d.GetType().GetField("type", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                        if (tf != null) enumType = tf.FieldType;
                    }
                    if (enumType == null) return RichResult.Fail(ErrorCode.NotSupported, "Enum type not found on description");
                    object enumVal = null;
                    try
                    {
                        var names = DuckovReflectionCache.GetEnumNames(enumType);
                        var mapped = MapModifierEnumName(type, names);
                        enumVal = Enum.Parse(enumType, mapped);
                    }
                    catch { enumVal = enumType.IsEnum ? Enum.GetValues(enumType).GetValue(0) : null; }
                    bool setOk = false;
                    try
                    {
                        var setter = DuckovReflectionCache.GetSetter(d.GetType(), "Type", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                        if (setter != null) { setter.Invoke(d, enumVal); setOk = true; }
                        else
                        {
                            var fld = DuckovReflectionCache.GetField(d.GetType(), "type", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                            if (fld != null) { fld.SetValue(d, enumVal); setOk = true; }
                        }
                    }
                    catch { setOk = false; }
                    if (!setOk) return RichResult.Fail(ErrorCode.OperationFailed, "failed to set description type");
                    TryReapplyModifiers(item);
                    return RichResult.Success();
                }
                return RichResult.Fail(ErrorCode.NotFound, "description not found");
            }
            catch (Exception ex) { Log.Error("TrySetModifierDescriptionType failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>设置描述项的顺序。</summary>
        public RichResult TrySetModifierDescriptionOrder(object item, string key, int order)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var modsCol = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item) as System.Collections.IEnumerable;
                if (modsCol == null) return RichResult.Fail(ErrorCode.NotSupported, "Modifiers collection not found");
                foreach (var d in modsCol)
                {
                    if (d == null) continue; var k = Convert.ToString(DuckovTypeUtils.GetMaybe(d, new[]{"Key","key"})); if (!string.Equals(k, key, StringComparison.Ordinal)) continue;
                    DuckovReflectionCache.GetSetter(d.GetType(), "Order", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(d, order);
                    TryReapplyModifiers(item);
                    return RichResult.Success();
                }
                return RichResult.Fail(ErrorCode.NotFound, "description not found");
            }
            catch (Exception ex) { Log.Error("TrySetModifierDescriptionOrder failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>设置描述项是否显示。</summary>
        public RichResult TrySetModifierDescriptionDisplay(object item, string key, bool display)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var modsCol = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item) as System.Collections.IEnumerable;
                if (modsCol == null) return RichResult.Fail(ErrorCode.NotSupported, "Modifiers collection not found");
                foreach (var d in modsCol)
                {
                    if (d == null) continue; var k = Convert.ToString(DuckovTypeUtils.GetMaybe(d, new[]{"Key","key"})); if (!string.Equals(k, key, StringComparison.Ordinal)) continue;
                    DuckovReflectionCache.GetSetter(d.GetType(), "Display", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(d, display);
                    TryReapplyModifiers(item);
                    return RichResult.Success();
                }
                return RichResult.Fail(ErrorCode.NotFound, "description not found");
            }
            catch (Exception ex) { Log.Error("TrySetModifierDescriptionDisplay failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>清空描述集合。</summary>
        public RichResult TryClearModifierDescriptions(object item)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var modsCol = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item);
                if (modsCol == null) return RichResult.Fail(ErrorCode.NotSupported, "Modifiers collection not found");
                var clear = DuckovReflectionCache.GetMethod(modsCol.GetType(), "Clear", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
                            ?? DuckovReflectionCache.GetMethod(modsCol.GetType(), "ClearModifiers", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                clear?.Invoke(modsCol, null);
                TryReapplyModifiers(item);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryClearModifierDescriptions failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>清理无效描述项（如空 Key）。</summary>
        public RichResult TrySanitizeModifierDescriptions(object item)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var modsColObj = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item);
                var modsCol = modsColObj as System.Collections.IEnumerable;
                if (modsCol == null) return RichResult.Fail(ErrorCode.NotSupported, "Modifiers collection not found");
                var remove = DuckovReflectionCache.GetMethod(modsColObj.GetType(), "Remove", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
                             ?? DuckovReflectionCache.GetMethod(modsColObj.GetType(), "RemoveModifier", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                var toRemove = new List<object>();
                foreach (var d in modsCol)
                {
                    if (d == null) continue;
                    var k = Convert.ToString(DuckovTypeUtils.GetMaybe(d, new[]{"Key","key"}));
                    if (string.IsNullOrEmpty(k)) toRemove.Add(d);
                }
                foreach (var d in toRemove)
                {
                    try { remove?.Invoke(modsColObj, new[]{ d }); } catch { }
                }
                TryReapplyModifiers(item);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TrySanitizeModifierDescriptions failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        // Effects
        /// <summary>新增一个效果组件。</summary>
        public RichResult TryAddEffect(object item, string effectTypeFullName, EffectCreateOptions options = null)
        {
            try
            {
                if (item == null || string.IsNullOrEmpty(effectTypeFullName)) return RichResult.Fail(ErrorCode.InvalidArgument, "args");
                var effType = DuckovTypeUtils.FindType(effectTypeFullName);
                if (effType == null) return RichResult.Fail(ErrorCode.NotFound, "effect type not found");
                var go = DuckovTypeUtils.GetMaybe(item, new[]{"gameObject"}) as UnityEngine.GameObject; if (go == null) return RichResult.Fail(ErrorCode.NotSupported, "item has no gameObject");
                var child = new UnityEngine.GameObject(options?.Name ?? ("New " + effType.Name)); child.hideFlags = UnityEngine.HideFlags.HideInInspector; child.transform.SetParent(go.transform, false);
                var effect = child.AddComponent(effType);
                if (options != null)
                {
                    if (options.Display.HasValue) { try { DuckovReflectionCache.GetSetter(effType, "display", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(effect, options.Display.Value); } catch { } }
                    if (!string.IsNullOrEmpty(options.Description)) { try { DuckovReflectionCache.GetSetter(effType, "description", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(effect, options.Description); } catch { } }
                }
                var effectsList = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item);
                var add = DuckovReflectionCache.GetMethod(effectsList?.GetType(), "Add", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ effType }); add?.Invoke(effectsList, new[]{ effect });
                var setItem = DuckovReflectionCache.GetMethod(effType, "SetItem", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ item.GetType() })
                              ?? DuckovReflectionCache.GetMethod(effType, "SetItem", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                setItem?.Invoke(effect, new[]{ item });
                if (options?.Enabled == true) { try { (effect as UnityEngine.Behaviour).enabled = true; } catch { } }
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryAddEffect failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        public RichResult TryAddEffect(object item, string effectTypeFullName) => TryAddEffect(item, effectTypeFullName, null);
        /// <summary>移除指定索引的效果组件。</summary>
        public RichResult TryRemoveEffect(object item, int effectIndex)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var effects = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item) as System.Collections.IList;
                if (effects == null) return RichResult.Fail(ErrorCode.NotSupported, "no Effects list");
                if (effectIndex < 0 || effectIndex >= effects.Count) return RichResult.Fail(ErrorCode.OutOfRange, "index");
                var effect = effects[effectIndex] as UnityEngine.Component; effects.RemoveAt(effectIndex);
                if (effect != null) { try { UnityEngine.Object.DestroyImmediate(effect.gameObject); } catch { try { UnityEngine.Object.Destroy(effect.gameObject); } catch { } } }
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryRemoveEffect failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>启用/禁用效果组件。</summary>
        public RichResult TryEnableEffect(object item, int effectIndex, bool enabled)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var effects = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item) as System.Collections.IList;
                if (effects == null) return RichResult.Fail(ErrorCode.NotSupported, "no Effects list");
                if (effectIndex < 0 || effectIndex >= effects.Count) return RichResult.Fail(ErrorCode.OutOfRange, "index");
                var effect = effects[effectIndex] as UnityEngine.Behaviour; if (effect == null) return RichResult.Fail(ErrorCode.NotSupported, "effect not Behaviour");
                effect.enabled = enabled; return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryEnableEffect failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>设置效果组件上的任意属性。</summary>
        public RichResult TrySetEffectProperty(object item, int effectIndex, string propName, object value)
        {
            try
            {
                if (item == null || string.IsNullOrEmpty(propName)) return RichResult.Fail(ErrorCode.InvalidArgument, "args");
                var effects = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item) as System.Collections.IList;
                if (effects == null) return RichResult.Fail(ErrorCode.NotSupported, "no Effects list");
                if (effectIndex < 0 || effectIndex >= effects.Count) return RichResult.Fail(ErrorCode.OutOfRange, "index");
                var effect = effects[effectIndex]; var et = effect.GetType();
                var setter = DuckovReflectionCache.GetSetter(et, propName, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
                          ?? DuckovReflectionCache.GetSetter(et, propName.ToLowerInvariant(), BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (setter == null) return RichResult.Fail(ErrorCode.NotSupported, "setter not found");
                setter(effect, value); return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TrySetEffectProperty failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>为效果添加子组件（Trigger/Filter/Action）。</summary>
        public RichResult TryAddEffectComponent(object item, int effectIndex, string componentTypeFullName, string kind)
        {
            try
            {
                if (item == null || string.IsNullOrEmpty(componentTypeFullName) || string.IsNullOrEmpty(kind)) return RichResult.Fail(ErrorCode.InvalidArgument, "args");
                var effects = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item) as System.Collections.IList;
                if (effects == null) return RichResult.Fail(ErrorCode.NotSupported, "no Effects list");
                if (effectIndex < 0 || effectIndex >= effects.Count) return RichResult.Fail(ErrorCode.OutOfRange, "index");
                var effect = effects[effectIndex] as UnityEngine.Component; if (effect == null) return RichResult.Fail(ErrorCode.OperationFailed, "effect null");
                var type = DuckovTypeUtils.FindType(componentTypeFullName); if (type == null) return RichResult.Fail(ErrorCode.NotFound, "component type not found");
                var comp = effect.gameObject.AddComponent(type);
                var add = DuckovReflectionCache.GetMethod(effect.GetType(), "AddEffectComponent", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ DuckovTypeUtils.FindType("ItemStatsSystem.EffectComponent") ?? typeof(UnityEngine.Component) })
                          ?? DuckovReflectionCache.GetMethod(effect.GetType(), "AddEffectComponent", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (add != null) add.Invoke(effect, new[]{ comp }); else
                {
                    var fieldName = kind.Equals("Trigger", StringComparison.OrdinalIgnoreCase) ? "triggers" : kind.Equals("Filter", StringComparison.OrdinalIgnoreCase) ? "filters" : "actions";
                    var list = DuckovReflectionCache.GetField(effect.GetType(), fieldName, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(effect) as System.Collections.IList;
                    list?.Add(comp);
                }
                try { DuckovReflectionCache.GetSetter(type, "Master", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(comp, effect); } catch { }
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryAddEffectComponent failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>移除指定索引的效果子组件。</summary>
        public RichResult TryRemoveEffectComponent(object item, int effectIndex, string kind, int componentIndex)
        {
            try
            {
                if (item == null || string.IsNullOrEmpty(kind)) return RichResult.Fail(ErrorCode.InvalidArgument, "args");
                var effects = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item) as System.Collections.IList;
                if (effects == null) return RichResult.Fail(ErrorCode.NotSupported, "no Effects list");
                if (effectIndex < 0 || effectIndex >= effects.Count) return RichResult.Fail(ErrorCode.OutOfRange, "index");
                var effect = effects[effectIndex] as UnityEngine.Component; if (effect == null) return RichResult.Fail(ErrorCode.OperationFailed, "effect null");
                var fieldName = kind.Equals("Trigger", StringComparison.OrdinalIgnoreCase) ? "triggers" : kind.Equals("Filter", StringComparison.OrdinalIgnoreCase) ? "filters" : "actions";
                var list = DuckovReflectionCache.GetField(effect.GetType(), fieldName, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(effect) as System.Collections.IList;
                if (list == null || componentIndex < 0 || componentIndex >= list.Count) return RichResult.Fail(ErrorCode.OutOfRange, "component index");
                var comp = list[componentIndex] as UnityEngine.Component; list.RemoveAt(componentIndex);
                if (comp != null) { try { UnityEngine.Object.DestroyImmediate(comp); } catch { try { UnityEngine.Object.Destroy(comp); } catch { } } }
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryRemoveEffectComponent failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>设置效果子组件属性。</summary>
        public RichResult TrySetEffectComponentProperty(object item, int effectIndex, string kind, int componentIndex, string propName, object value)
        {
            try
            {
                if (item == null || string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(propName)) return RichResult.Fail(ErrorCode.InvalidArgument, "args");
                var effects = DuckovReflectionCache.GetGetter(item.GetType(), "Effects", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item) as System.Collections.IList;
                if (effects == null) return RichResult.Fail(ErrorCode.NotSupported, "no Effects list");
                if (effectIndex < 0 || effectIndex >= effects.Count) return RichResult.Fail(ErrorCode.OutOfRange, "index");
                var effect = effects[effectIndex]; var fieldName = kind.Equals("Trigger", StringComparison.OrdinalIgnoreCase) ? "triggers" : kind.Equals("Filter", StringComparison.OrdinalIgnoreCase) ? "filters" : "actions";
                var list = DuckovReflectionCache.GetField(effect.GetType(), fieldName, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(effect) as System.Collections.IList;
                if (list == null || componentIndex < 0 || componentIndex >= list.Count) return RichResult.Fail(ErrorCode.OutOfRange, "component index");
                var comp = list[componentIndex]; var setter = DuckovReflectionCache.GetSetter(comp.GetType(), propName, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
                           ?? DuckovReflectionCache.GetSetter(comp.GetType(), propName.ToLowerInvariant(), BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (setter == null) return RichResult.Fail(ErrorCode.NotSupported, "setter not found");
                setter(comp, value); return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TrySetEffectComponentProperty failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
    }
}
