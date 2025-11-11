using System;
using System.Reflection;
using System.Globalization;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
 /// <summary>
 /// 写入服务（统计 Stat）：提供设置、确保存在、移除以及内部多重反射匹配逻辑。
 /// </summary>
 internal sealed partial class WriteService : IWriteService
 {
 // Stats write implementations
 /// <summary>设置指定 Stat 的数值，尝试多种路径（属性/方法/字段）。</summary>
 public RichResult TrySetStatValue(object ownerItem, string statKey, float value)
 {
 try
 {
 if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "owner null");
 if (string.IsNullOrEmpty(statKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "statKey null");
 var stats = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Stats", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(ownerItem);
 if (stats == null) return RichResult.Fail(ErrorCode.NotSupported, "no Stats on owner");
 object stat = null;
 var indexer = DuckovReflectionCache.GetMethod(stats.GetType(), "get_Item", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ typeof(string) });
 if (indexer != null) { stat = indexer.Invoke(stats, new object[]{ statKey }); }
 if (stat == null)
 {
 var getStat = DuckovReflectionCache.GetMethod(stats.GetType(), "GetStat", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ typeof(string) });
 if (getStat != null) stat = getStat.Invoke(stats, new object[]{ statKey });
 }
 if (stat == null) return RichResult.Fail(ErrorCode.NotFound, "stat not found");

 // 1) Value 属性
 var setVal = DuckovReflectionCache.GetSetter(stat.GetType(), "Value", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (setVal != null)
 {
 setVal(stat, value);
 _item.ReapplyModifiers(ownerItem);
 IMKDuckov.MarkDirty(ownerItem, DirtyKind.Stats);
 return RichResult.Success();
 }

 // 2) 方法 SetValue/Set/SetBaseValue
 foreach (var mname in new[]{ "SetValue", "Set", "SetBaseValue" })
 {
 var m = DuckovReflectionCache.GetMethod(stat.GetType(), mname, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ typeof(float) })
         ?? DuckovReflectionCache.GetMethod(stat.GetType(), mname, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ typeof(double) })
         ?? DuckovReflectionCache.GetMethod(stat.GetType(), mname, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (m == null) continue;
 var ps = m.GetParameters();
 if (ps.Length != 1) continue;
 object arg = value;
 try { arg = System.Convert.ChangeType(value, ps[0].ParameterType, CultureInfo.InvariantCulture); } catch { }
 try { m.Invoke(stat, new[]{ arg }); _item.ReapplyModifiers(ownerItem); IMKDuckov.MarkDirty(ownerItem, DirtyKind.Stats); return RichResult.Success(); } catch { }
 }

 // 3) 备用属性名
 foreach (var pname in new[]{ "BaseValue", "CurrentValue", "Amount" })
 {
 var setter = DuckovReflectionCache.GetSetter(stat.GetType(), pname, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (setter != null) { setter(stat, value); _item.ReapplyModifiers(ownerItem); IMKDuckov.MarkDirty(ownerItem, DirtyKind.Stats); return RichResult.Success(); }
 }

 // 4) 字段后备
 foreach (var fname in new[]{ "Value", "m_Value", "_value", "BaseValue", "m_BaseValue", "_baseValue" })
 {
 var f = DuckovReflectionCache.GetField(stat.GetType(), fname, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (f == null || f.IsInitOnly) continue;
 try
 {
 object arg = value;
 try { arg = System.Convert.ChangeType(value, f.FieldType, CultureInfo.InvariantCulture); } catch { }
 f.SetValue(stat, arg);
 _item.ReapplyModifiers(ownerItem);
 IMKDuckov.MarkDirty(ownerItem, DirtyKind.Stats);
 return RichResult.Success();
 }
 catch { }
 }

 return RichResult.Fail(ErrorCode.NotSupported, "stat.Value setter not found");
 }
 catch (System.Exception ex) { Log.Error("TrySetStatValue failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
 }

 /// <summary>确保 Stat 存在（若不存在则实例化并加入集合，可选初始值）。</summary>
 public RichResult TryEnsureStat(object ownerItem, string statKey, float? initialValue = null)
 {
 try
 {
 if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "owner null");
 if (string.IsNullOrEmpty(statKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "statKey null");
 var stats = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Stats", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(ownerItem);
 if (stats == null) return RichResult.Fail(ErrorCode.NotSupported, "no Stats on owner");
 object stat = null;
 var indexer = DuckovReflectionCache.GetMethod(stats.GetType(), "get_Item", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ typeof(string) });
 if (indexer != null) { stat = indexer.Invoke(stats, new object[]{ statKey }); }
 if (stat == null)
 {
 var getStat = DuckovReflectionCache.GetMethod(stats.GetType(), "GetStat", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ typeof(string) });
 if (getStat != null) stat = getStat.Invoke(stats, new object[]{ statKey });
 }
 if (stat != null)
 {
 if (initialValue.HasValue)
 {
 var rsv = TrySetStatValue(ownerItem, statKey, initialValue.Value);
 }
 IMKDuckov.MarkDirty(ownerItem, DirtyKind.Stats);
 return RichResult.Success();
 }
 // Add(T) 签名推断类型
 var add = DuckovReflectionCache.GetMethod(stats.GetType(), "Add", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (add == null || add.GetParameters().Length != 1)
 {
 var fallbackStatType = DuckovTypeUtils.FindType("ItemStatsSystem.Stats.Stat") ?? DuckovTypeUtils.FindType("Stat");
 if (fallbackStatType == null) return RichResult.Fail(ErrorCode.NotSupported, "Stat type missing");
 var ns = System.Activator.CreateInstance(fallbackStatType);
 DuckovTypeUtils.SetProp(ns, "Key", statKey);
 if (initialValue.HasValue) { TryAssignStatValue(ns, initialValue.Value); }
 var init0 = DuckovReflectionCache.GetMethod(fallbackStatType, "Initialize", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ stats.GetType() });
 init0?.Invoke(ns, new[]{ stats });
 var add0 = DuckovReflectionCache.GetMethod(stats.GetType(), "Add", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ fallbackStatType });
 if (add0 == null) return RichResult.Fail(ErrorCode.NotSupported, "StatCollection.Add not found");
 add0.Invoke(stats, new[]{ ns });
 _item.ReapplyModifiers(ownerItem);
 IMKDuckov.MarkDirty(ownerItem, DirtyKind.Stats);
 return RichResult.Success();
 }
 var statType = add.GetParameters()[0].ParameterType;
 var newStat = System.Activator.CreateInstance(statType);
 DuckovTypeUtils.SetProp(newStat, "Key", statKey);
 if (initialValue.HasValue) { TryAssignStatValue(newStat, initialValue.Value); }
 var init = DuckovReflectionCache.GetMethod(statType, "Initialize", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ stats.GetType() })
            ?? DuckovReflectionCache.GetMethod(statType, "Initialize", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, System.Type.EmptyTypes)
            ?? DuckovReflectionCache.GetMethod(statType, "Initialize", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 init?.Invoke(newStat, init != null && init.GetParameters().Length == 0 ? null : new[]{ stats });
 var addTyped = DuckovReflectionCache.GetMethod(stats.GetType(), "Add", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ statType }) ?? add;
 addTyped.Invoke(stats, new[]{ newStat });
 _item.ReapplyModifiers(ownerItem);
 IMKDuckov.MarkDirty(ownerItem, DirtyKind.Stats);
 return RichResult.Success();
 }
 catch (System.Exception ex) { Log.Error("TryEnsureStat failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
 }

 private static void TryAssignStatValue(object stat, float value)
 {
 try
 {
 var setVal = DuckovReflectionCache.GetSetter(stat.GetType(), "Value", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (setVal != null) { setVal(stat, value); return; }
 foreach (var mname in new[]{ "SetValue", "Set", "SetBaseValue" })
 {
 var m = DuckovReflectionCache.GetMethod(stat.GetType(), mname, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ typeof(float) })
         ?? DuckovReflectionCache.GetMethod(stat.GetType(), mname, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ typeof(double) })
         ?? DuckovReflectionCache.GetMethod(stat.GetType(), mname, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (m == null) continue;
 var ps = m.GetParameters(); if (ps.Length != 1) continue;
 object arg = value; try { arg = Convert.ChangeType(value, ps[0].ParameterType, CultureInfo.InvariantCulture); } catch { }
 try { m.Invoke(stat, new[]{ arg }); return; } catch { }
 }
 foreach (var pname in new[]{ "BaseValue", "CurrentValue", "Amount" })
 {
 var setter = DuckovReflectionCache.GetSetter(stat.GetType(), pname, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (setter != null) { setter(stat, value); return; }
 }
 foreach (var fname in new[]{ "Value", "m_Value", "_value", "BaseValue", "m_BaseValue", "_baseValue" })
 {
 var f = DuckovReflectionCache.GetField(stat.GetType(), fname, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
 if (f == null || f.IsInitOnly) continue;
 object arg = value; try { arg = Convert.ChangeType(value, f.FieldType, CultureInfo.InvariantCulture); } catch { }
 try { f.SetValue(stat, arg); return; } catch { }
 }
 }
 catch { }
 }

 /// <summary>移除指定 Stat。</summary>
 public RichResult TryRemoveStat(object ownerItem, string statKey)
 {
 try
 {
 if (ownerItem == null) return RichResult.Fail(ErrorCode.InvalidArgument, "owner null");
 if (string.IsNullOrEmpty(statKey)) return RichResult.Fail(ErrorCode.InvalidArgument, "statKey null");
 var stats = DuckovReflectionCache.GetGetter(ownerItem.GetType(), "Stats", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(ownerItem);
 if (stats == null) return RichResult.Fail(ErrorCode.NotSupported, "no Stats on owner");
 var getStat = DuckovReflectionCache.GetMethod(stats.GetType(), "GetStat", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ typeof(string) });
 if (getStat == null) return RichResult.Fail(ErrorCode.NotSupported, "GetStat not found");
 var stat = getStat.Invoke(stats, new object[]{ statKey });
 if (stat == null) return RichResult.Fail(ErrorCode.NotFound, "stat not found");
 var remove = DuckovReflectionCache.GetMethod(stats.GetType(), "Remove", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, new[]{ stat.GetType() });
 if (remove == null) return RichResult.Fail(ErrorCode.NotSupported, "StatCollection.Remove not found");
 remove.Invoke(stats, new[]{ stat });
 _item.ReapplyModifiers(ownerItem);
 IMKDuckov.MarkDirty(ownerItem, DirtyKind.Stats);
 return RichResult.Success();
 }
 catch (System.Exception ex) { Log.Error("TryRemoveStat failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
 }
 }
}
