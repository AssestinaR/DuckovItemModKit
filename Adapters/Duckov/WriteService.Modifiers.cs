using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
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
        private static bool s_modifierModeProbed;
        private static bool s_descriptionsOnly; // when true, skip raw AddModifier and route to descriptions
        private static bool Verbose => Environment.GetEnvironmentVariable("IMK_DEBUG_MODIFIERS") == "1"; // simple gate

        private void EnsureModifierModeProbed(object item)
        {
            if (s_modifierModeProbed || item == null) return;
            s_modifierModeProbed = true;
            try
            {
                var sig = ResolveAddModifierSignatureCached(item.GetType());
                bool ok = false;
                string probeKey = "__imk_mod_probe__";
                // try numeric path
                if (sig.addNum != null)
                {
                    try
                    {
                        var args = sig.addNum.GetParameters().Length == 3 ? new object[] { probeKey, 0f, false } : new object[] { probeKey, 0f };
                        var r = sig.addNum.Invoke(item, args);
                        ok = !(r is bool b) || b;
                    }
                    catch { ok = false; }
                }
                // try object path if numeric failed
                if (!ok && sig.addObj != null && sig.modifierType != null)
                {
                    try
                    {
                        object enumVal = null;
                        if (sig.enumType != null)
                        {
                            var names = DuckovReflectionCache.GetEnumNames(sig.enumType);
                            enumVal = names!=null && names.Length>0? Enum.Parse(sig.enumType, names[0]) : Enum.GetValues(sig.enumType).GetValue(0);
                        }
                        var mod = CreateModifierInstance(sig.modifierType, sig.enumType, 0f, enumVal, null);
                        var r = sig.addObj.Invoke(item, new object[]{ probeKey, mod });
                        ok = !(r is bool b2) || b2;
                    }
                    catch { ok = false; }
                }
                // mark descriptions-only if both failed
                if (!ok) s_descriptionsOnly = true;
                else
                {
                    // remove probe descriptor if created (just in case)
                    try { TryRemoveModifierDescription(item, probeKey); } catch { }
                }
                if (Verbose) DebugLog("Probe modifier mode ok="+ok+" descriptionsOnly="+s_descriptionsOnly);
            }
            catch { s_descriptionsOnly = true; }
        }

        private static string MapModifierEnumName(string input, string[] names)
        {
            if (names == null || names.Length == 0) return input ?? "Add";
            string raw = (input ?? "Add").Trim();
            // normalize tokens
            bool isMul = raw.IndexOf("Multiply", StringComparison.OrdinalIgnoreCase)>=0 || raw.Equals("PercentageMultiply", StringComparison.OrdinalIgnoreCase);
            bool isPercAdd = raw.IndexOf("PercentageAdd", StringComparison.OrdinalIgnoreCase)>=0 || raw.IndexOf("Percent", StringComparison.OrdinalIgnoreCase)>=0 && raw.IndexOf("Add", StringComparison.OrdinalIgnoreCase)>=0;
            if (isMul)
                return names.FirstOrDefault(n=> n.IndexOf("Multiply", StringComparison.OrdinalIgnoreCase)>=0) ?? names.Last();
            if (isPercAdd)
                return names.FirstOrDefault(n=> n.IndexOf("Add", StringComparison.OrdinalIgnoreCase)>=0 && n.IndexOf("Percent", StringComparison.OrdinalIgnoreCase)>=0)
                       ?? names.FirstOrDefault(n=> n.IndexOf("PercentageAdd", StringComparison.OrdinalIgnoreCase)>=0)
                       ?? names[0];
            return names.FirstOrDefault(n=> string.Equals(n, "Add", StringComparison.OrdinalIgnoreCase))
                   ?? names.FirstOrDefault(n=> n.IndexOf("Add", StringComparison.OrdinalIgnoreCase)>=0)
                   ?? names[0];
        }

        public RichResult TryAddModifier(object item, string statKey, float value, bool isPercent = false, string type = null, object source = null)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null"); if (string.IsNullOrEmpty(statKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "statKey is null");
                EnsureModifierModeProbed(item);
                if (s_descriptionsOnly)
                {
                    // route directly to description layer (Add) ignoring percent flag (type retained for UI semantics)
                    return TryAddModifierDescription(item, statKey, type ?? "Add", value, true, 0, null);
                }
                if (Verbose) DebugLog("TryAddModifier begin key="+statKey+" value="+value+" type="+type+" percent="+isPercent);
                TryEnsureStat(item, statKey, value);
                var sig = ResolveAddModifierSignatureCached(item.GetType()); bool applied = false;
                if (sig.addNum != null)
                {
                    var args = sig.addNum.GetParameters().Length == 3 ? new object[] { statKey, value, isPercent } : new object[] { statKey, value };
                    try { var okObj = sig.addNum.Invoke(item, args); applied = !(okObj is bool) || (okObj is bool b && b); } catch { applied = false; }
                }
                if (!applied && sig.addObj != null && sig.modifierType != null)
                {
                    object enumVal = null; if (sig.enumType != null)
                    {
                        try { var names = DuckovReflectionCache.GetEnumNames(sig.enumType); string mapped = MapModifierEnumName(type, names); enumVal = Enum.Parse(sig.enumType, mapped); } catch { enumVal = sig.enumType.IsEnum ? Enum.GetValues(sig.enumType).GetValue(0) : null; }
                    }
                    var mod = CreateModifierInstance(sig.modifierType, sig.enumType, value, enumVal, source); if (mod != null)
                    {
                        try { var okObj = sig.addObj.Invoke(item, new object[] { statKey, mod }); applied = !(okObj is bool) || (okObj is bool b && b); } catch { applied = false; }
                        if (!applied)
                        {
                            // final fallback disabled when not verbose (avoid noisy reflection churn)
                            if (Verbose)
                            {
                                try
                                {
                                    var statsObj = DuckovReflectionCache.GetGetter(item.GetType(), "Stats", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item); object stat = null;
                                    var indexer = DuckovReflectionCache.GetMethod(statsObj?.GetType(), "get_Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(string) }); if (indexer != null) stat = indexer.Invoke(statsObj, new object[] { statKey });
                                    if (stat == null)
                                    {
                                        var getStat = DuckovReflectionCache.GetMethod(statsObj?.GetType(), "GetStat", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(string) }); if (getStat != null) stat = getStat.Invoke(statsObj, new object[] { statKey });
                                    }
                                    if (stat != null)
                                    {
                                        var statAdd = DuckovReflectionCache.GetMethod(stat.GetType(), "AddModifier", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { sig.modifierType })
                                                     ?? DuckovReflectionCache.GetMethod(stat.GetType(), "AddModifier", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                        if (statAdd != null) { statAdd.Invoke(stat, new object[] { mod }); applied = true; }
                                    }
                                }
                                catch { applied = false; }
                            }
                        }
                    }
                }
                if (!applied) return RichResult.Fail(ErrorCode.OperationFailed, "AddModifier failed");
                TryReapplyModifiers(item); return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryAddModifier failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

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

        public RichResult TryReapplyModifiers(object item)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null"); bool statsIsNull = false;
                try { var p = item.GetType().GetProperty("Stats", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); var stats = p?.GetValue(item, null); statsIsNull = stats == null; } catch { }
                if (statsIsNull) return RichResult.Success(); _item.ReapplyModifiers(item); return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryReapplyModifiers failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        // Modifier description APIs (single implementation)
        public RichResult TryAddModifierDescription(object item, string key, string type, float value, bool? display = null, int? order = null, string target = null)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null"); if (string.IsNullOrEmpty(key)) return RichResult.Fail(ErrorCode.InvalidArgument, "key is empty");
                DebugLog("TryAddModifierDescription begin key="+key+" value="+value+" type="+type);
                var modsCol = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item);
                if (modsCol == null) return RichResult.Fail(ErrorCode.NotSupported, "Modifiers collection not found");
                var existing = (modsCol as System.Collections.IEnumerable)?.Cast<object>().FirstOrDefault(d => string.Equals(Convert.ToString(DuckovTypeUtils.GetMaybe(d,new[]{"Key","key"})), key, StringComparison.Ordinal));
                if (existing != null)
                {
                    // Upsert: update value/type/display/order
                    InternalSetDescField(item, key, "Value", value);
                    if (!string.IsNullOrEmpty(type)) TrySetModifierDescriptionType(item, key, type);
                    if (display.HasValue) InternalSetDescField(item, key, "Display", display.Value);
                    if (order.HasValue) InternalSetDescField(item, key, "Order", order.Value);
                    return RichResult.Success();
                }
                TryEnsureStat(item, key, value);
                var descType = modsCol.GetType().GetGenericArguments().FirstOrDefault() ?? DuckovTypeUtils.FindType("ItemStatsSystem.ModifierDescription");
                if (descType == null) return RichResult.Fail(ErrorCode.NotSupported, "ModifierDescription type missing");
                var inst = Activator.CreateInstance(descType);
                var keyOk = TrySetByNames(descType, inst, new[]{"Key","key","Name"}, key);
                var valOk = TrySetByNames(descType, inst, new[]{"Value","value","Amount"}, value);
                var dispOk = display.HasValue ? TrySetByNames(descType, inst, new[]{"Display","display"}, display.Value) : TrySetByNames(descType, inst, new[]{"Display","display"}, true);
                if (order.HasValue) TrySetByNames(descType, inst, new[]{"Order","order","Index"}, order.Value);
                if (!string.IsNullOrEmpty(target)) TrySetByNames(descType, inst, new[]{"Target","target"}, target);
                if (!string.IsNullOrEmpty(type)) TrySetEnumByAny(descType, inst, type);
                if (!keyOk || !valOk) DebugDumpMembers(descType);
                var addDesc = DuckovReflectionCache.GetMethod(modsCol.GetType(), "Add", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { descType })
                              ?? DuckovReflectionCache.GetMethod(modsCol.GetType(), "Add", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                addDesc?.Invoke(modsCol, new[] { inst });
                var reapply = DuckovReflectionCache.GetMethod(modsCol.GetType(), "ReapplyModifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                reapply?.Invoke(modsCol, null);
                TryReapplyModifiers(item);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryAddModifierDescription failed", ex); DebugLog("TryAddModifierDescription exception: "+ex.Message); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        public RichResult TryRemoveModifierDescription(object item, string key)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var modsCol = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item);
                if (modsCol == null) return RichResult.Fail(ErrorCode.NotSupported, "Modifiers collection not found");
                object targetDesc = null;
                foreach (var d in (modsCol as System.Collections.IEnumerable) ?? Array.Empty<object>())
                {
                    if (d == null) continue; var k = Convert.ToString(DuckovTypeUtils.GetMaybe(d, new[] { "Key", "key" })); if (string.Equals(k, key, StringComparison.Ordinal)) { targetDesc = d; break; }
                }
                if (targetDesc == null) return RichResult.Fail(ErrorCode.NotFound, "description not found");
                var remove = DuckovReflectionCache.GetMethod(modsCol.GetType(), "Remove", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { targetDesc.GetType() })
                              ?? DuckovReflectionCache.GetMethod(modsCol.GetType(), "Remove", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                remove?.Invoke(modsCol, new[] { targetDesc });
                var reapply = DuckovReflectionCache.GetMethod(modsCol.GetType(), "ReapplyModifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); reapply?.Invoke(modsCol, null);
                TryReapplyModifiers(item); return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryRemoveModifierDescription failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        public RichResult TrySetModifierDescriptionValue(object item, string key, float value)
        { DebugLog("SetDescValue key="+key+" value="+value); return InternalSetDescField(item, key, "Value", value); }
        public RichResult TrySetModifierDescriptionType(object item, string key, string type)
        { DebugLog("SetDescType key="+key+" type="+type); return TrySetModifierDescriptionTypeInternal(item, key, type); }
        public RichResult TrySetModifierDescriptionOrder(object item, string key, int order)
        { DebugLog("SetDescOrder key="+key+" order="+order); return InternalSetDescField(item, key, "Order", order); }
        public RichResult TrySetModifierDescriptionDisplay(object item, string key, bool display)
        { DebugLog("SetDescDisplay key="+key+" display="+display); return InternalSetDescField(item, key, "Display", display); }
        public RichResult TryClearModifierDescriptions(object item)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var modsCol = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item);
                if (modsCol == null) return RichResult.Fail(ErrorCode.NotSupported, "Modifiers collection not found");
                var clear = DuckovReflectionCache.GetMethod(modsCol.GetType(), "Clear", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                            ?? DuckovReflectionCache.GetMethod(modsCol.GetType(), "ClearModifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                clear?.Invoke(modsCol, null); TryReapplyModifiers(item); return RichResult.Success();
            }
            catch(Exception ex){ Log.Error("TryClearModifierDescriptions failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        public RichResult TrySanitizeModifierDescriptions(object item)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var modsColObj = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item);
                var modsCol = modsColObj as System.Collections.IEnumerable; if (modsCol == null) return RichResult.Fail(ErrorCode.NotSupported, "Modifiers collection not found");
                var remove = DuckovReflectionCache.GetMethod(modsColObj.GetType(), "Remove", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                             ?? DuckovReflectionCache.GetMethod(modsColObj.GetType(), "RemoveModifier", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var groups = modsCol.Cast<object>().Where(d=>d!=null).GroupBy(d => Convert.ToString(DuckovTypeUtils.GetMaybe(d,new[]{"Key","key"})) ?? "");
                foreach (var g in groups)
                {
                    var list = g.ToList(); if (string.IsNullOrEmpty(g.Key)) { foreach (var d in list) { try { remove?.Invoke(modsColObj, new[] { d }); } catch { } } continue; }
                    for (int i=0;i<list.Count-1;i++) { try { remove?.Invoke(modsColObj, new[] { list[i] }); } catch { } } // keep last
                }
                TryReapplyModifiers(item); return RichResult.Success();
            }
            catch(Exception ex){ Log.Error("TrySanitizeModifierDescriptions failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        private RichResult InternalSetDescField(object item, string key, string field, object value)
        {
            try
            {
                var modsCol = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item) as System.Collections.IEnumerable;
                if (modsCol == null) return RichResult.Fail(ErrorCode.NotSupported, "Modifiers collection not found");
                foreach (var d in modsCol)
                {
                    if (d == null) continue; var k = Convert.ToString(DuckovTypeUtils.GetMaybe(d,new[]{"Key","key"})); if (!string.Equals(k, key, StringComparison.Ordinal)) continue;
                    var dt = d.GetType(); bool ok=false;
                    if (field=="Value") ok = TrySetByNames(dt, d, new[]{"Value","value"}, value);
                    else if (field=="Order") ok = TrySetByNames(dt, d, new[]{"Order","order","Index"}, value);
                    else if (field=="Display") ok = TrySetByNames(dt, d, new[]{"Display","display"}, value);
                    else ok = TrySetByNames(dt, d, new[]{field, field.ToLowerInvariant()}, value);
                    TryReapplyModifiers(item); return ok? RichResult.Success(): RichResult.Fail(ErrorCode.OperationFailed, "set failed");
                }
                return RichResult.Fail(ErrorCode.NotFound, "description not found");
            }
            catch(Exception ex){ return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        private RichResult TrySetModifierDescriptionTypeInternal(object item, string key, string type)
        {
            try
            {
                var modsColObj = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item);
                var modsCol = modsColObj as System.Collections.IEnumerable;
                if (modsCol == null) return RichResult.Fail(ErrorCode.NotSupported, "Modifiers collection not found");
                foreach (var d in modsCol)
                {
                    if (d == null) continue; var k = Convert.ToString(DuckovTypeUtils.GetMaybe(d,new[]{"Key","key","StatKey","statKey","Name","name"})); if (!string.Equals(k, key, StringComparison.Ordinal)) continue;
                    var dt = d.GetType();
                    // Attempt in place write: enum -> string -> backing field
                    bool ok = TrySetEnumByAny(dt, d, type);
                    if (!ok) ok = TrySetByNames(dt, d, new[]{"Type","type"}, type);
                    if (!ok) ok = TrySetAutoPropBackingField(dt, d, "Type", type);
                    if (!ok)
                    {
                        DebugDumpMembers(dt);
                        return RichResult.Fail(ErrorCode.OperationFailed, "set type failed");
                    }
                    // reapply + verify
                    TryReapplyModifiers(item);
                    string curType = Convert.ToString(DuckovTypeUtils.GetMaybe(d, new[]{"Type","type"})) ?? string.Empty;
                    if (!string.Equals(curType, type, StringComparison.OrdinalIgnoreCase))
                    {
                        // try one more time with direct assign to both property and backing field
                        TrySetByNames(dt, d, new[]{"Type","type"}, type);
                        TrySetAutoPropBackingField(dt, d, "Type", type);
                        TryReapplyModifiers(item);
                        curType = Convert.ToString(DuckovTypeUtils.GetMaybe(d, new[]{"Type","type"})) ?? string.Empty;
                    }
                    var success = string.Equals(curType, type, StringComparison.OrdinalIgnoreCase);
                    DebugDumpModifiers(item, "after-set-type");
                    return success ? RichResult.Success() : RichResult.Fail(ErrorCode.OperationFailed, "type verify failed");
                }
                return RichResult.Fail(ErrorCode.NotFound, "description not found");
            }
            catch(Exception ex){ return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }

        private static bool TrySetByNames(Type t, object inst, IEnumerable<string> names, object val)
        {
            foreach (var n in names)
            {
                try
                {
                    var p = t.GetProperty(n, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                    if (p != null)
                    {
                        try
                        {
                            object assign = val;
                            var pt = p.PropertyType;
                            if (pt.IsEnum && val is string s)
                            {
                                try { var namesArr = DuckovReflectionCache.GetEnumNames(pt); var mapped = MapModifierEnumName(s, namesArr); assign = Enum.Parse(pt, mapped, true); } catch { assign = Enum.GetValues(pt).GetValue(0); }
                            }
                            p.SetValue(inst, assign);
                            return true;
                        }
                        catch { /* fallthrough */ }
                    }
                    var f = t.GetField(n, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance) ?? t.GetField(n.ToLowerInvariant(), BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                    if (f != null)
                    {
                        try
                        {
                            object assign = val;
                            var ft = f.FieldType;
                            if (ft.IsEnum && val is string s)
                            {
                                try { var namesArr = DuckovReflectionCache.GetEnumNames(ft); var mapped = MapModifierEnumName(s, namesArr); assign = Enum.Parse(ft, mapped, true); } catch { assign = Enum.GetValues(ft).GetValue(0); }
                            }
                            f.SetValue(inst, assign);
                            return true;
                        }
                        catch { /* fallthrough */ }
                    }
                    // backing field of auto-property
                    if (TrySetAutoPropBackingField(t, inst, n, val)) return true;
                }
                catch { }
            }
            return false;
        }
        private static bool TrySetEnumByAny(Type t, object inst, string raw)
        {
            try
            {
                Func<string,bool> nameHit = n => n.IndexOf("Type", StringComparison.OrdinalIgnoreCase)>=0 || n.IndexOf("Kind", StringComparison.OrdinalIgnoreCase)>=0 || n.IndexOf("Op", StringComparison.OrdinalIgnoreCase)>=0 || n.IndexOf("Mode", StringComparison.OrdinalIgnoreCase)>=0 || n.IndexOf("Operation", StringComparison.OrdinalIgnoreCase)>=0 || n.IndexOf("Modifier", StringComparison.OrdinalIgnoreCase)>=0;
                var props = t.GetProperties(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                var prop = props.FirstOrDefault(p=> p.PropertyType.IsEnum && nameHit(p.Name)) ?? props.FirstOrDefault(p=> p.PropertyType.IsEnum);
                var fldsAll = t.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                var fld = fldsAll.FirstOrDefault(f=> f.FieldType.IsEnum && nameHit(f.Name)) ?? fldsAll.FirstOrDefault(f=> f.FieldType.IsEnum);
                var et = prop?.PropertyType ?? fld?.FieldType; if (et == null) return false;
                object val; try { var names = DuckovReflectionCache.GetEnumNames(et); var mapped = MapModifierEnumName(raw, names); val = Enum.Parse(et, mapped, true); } catch { try { val = Enum.Parse(et, raw, true); } catch { val = Enum.GetValues(et).GetValue(0); } }
                // try property via delegate first
                try
                {
                    var setter = DuckovReflectionCache.GetSetter(t, prop?.Name ?? "", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                    if (prop != null && setter != null) { setter(inst, val); return true; }
                }
                catch { }
                // fallback: direct property SetValue
                try { if (prop != null) { prop.SetValue(inst, val); return true; } } catch { }
                // fallback: enum field
                try { if (fld != null) { fld.SetValue(inst, val); return true; } } catch { }
                return false;
            }
            catch { return false; }
        }

        private void DebugLog(string msg){ if (Verbose) { try { UnityEngine.Debug.Log("[IMK.ModWrite] "+msg); } catch { } } }
        private void DebugDumpModifiers(object item, string tag)
        {
            if (!Verbose) return;
            try
            {
                var col = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(item) as System.Collections.IEnumerable;
                if (col == null){ DebugLog(tag+" modifiers=null"); return; }
                int i=0; foreach(var m in col){ if(m==null) continue; string k=Convert.ToString(DuckovTypeUtils.GetMaybe(m,new[]{"Key","key"})); float v=DuckovTypeUtils.ConvertToFloat(DuckovTypeUtils.GetMaybe(m,new[]{"Value","value"})); string t=Convert.ToString(DuckovTypeUtils.GetMaybe(m,new[]{"Type","type"})); DebugLog(tag+"["+i+"] key="+k+" val="+v+" type="+t); i++; }
                DebugLog(tag+" total="+i);
            }
            catch(Exception ex){ DebugLog(tag+" dump exception: "+ex.Message); }
        }
        private static void DebugDumpMembers(Type t)
        {
            try
            {
                if (!Verbose) return;
                var props = t.GetProperties(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Select(p=>p.Name+":"+p.PropertyType.Name);
                var flds = t.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Select(f=>f.Name+":"+f.FieldType.Name);
                try { UnityEngine.Debug.Log("[IMK.ModWrite] DescType="+t.FullName+" props=["+string.Join(",",props)+"] fields=["+string.Join(",",flds)+"]"); } catch { }
            }
            catch { }
        }
        private static bool TrySetAutoPropBackingField(Type t, object inst, string logicalName, object val)
        {
            try
            {
                var fields = t.GetFields(BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance);
                foreach (var f in fields)
                {
                    var n = f.Name;
                    if (n.IndexOf("k__BackingField", StringComparison.Ordinal) >= 0 && n.IndexOf(logicalName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        try { f.SetValue(inst, val); return true; } catch { }
                    }
                }
            }
            catch { }
            return false;
        }
    }
}
