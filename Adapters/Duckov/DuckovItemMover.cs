using System;
using System.Linq;
using System.Reflection;
using ItemModKit.Core;
using UnityEngine;

namespace ItemModKit.Adapters.Duckov
{
 /// <summary>
 /// 物品移动服务：提供添加/移除/移动/跨背包转移、发送到玩家或仓库、丢到地面、拆分与合并堆叠等操作。
 /// 尽量使用引擎现有 API（反射获取），并在失败时返回明确的错误码。
 /// </summary>
 internal sealed class DuckovItemMover : IItemMover
 {
     private readonly IItemAdapter _item; private readonly IInventoryAdapter _inv;
     public DuckovItemMover(IItemAdapter item, IInventoryAdapter inv) { _item = item; _inv = inv; }

     /// <summary>
     /// 解析任意对象上的物品组件：支持 Component/GameObject 或包装器含 Item 字段/属性。
     /// </summary>
     private static object ResolveItemComponent(object obj)
     {
         if (obj is Component c)
         {
             // 优先返回 Item 组件
             var itemT = Type.GetType("ItemStatsSystem.Item") ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ItemStatsSystem.Item", false)).FirstOrDefault(x => x != null);
             if (itemT != null && itemT.IsInstanceOfType(c)) return c;
             try { if (itemT != null) { var got = c.GetComponent(itemT); if (got != null) return got; } } catch { }
             return c;
         }
         if (obj is GameObject go)
         {
             var itemT = Type.GetType("ItemStatsSystem.Item") ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ItemStatsSystem.Item", false)).FirstOrDefault(x => x != null);
             try { if (itemT != null) { var got = go.GetComponent(itemT); if (got != null) return got; } } catch { }
             return go;
         }
         // 支持包装类型
         try
         {
             var t = obj?.GetType(); if (t != null)
             {
                 var p = t.GetProperty("Item", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                 var v = p?.GetValue(obj, null);
                 if (v is Component) return v;
                 var f = t.GetField("item", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                 v = v ?? f?.GetValue(obj);
                 if (v is Component) return v;
             }
         }
         catch { }
         return obj;
     }

     /// <summary>添加到指定背包（可选索引/是否允许合并）。</summary>
     public RichResult TryAddToInventory(object item, object inventory, int? index = null, bool allowMerge = true)
     {
         try
         {
             if (item == null || inventory == null) return RichResult.Fail(ErrorCode.InvalidArgument, "null args");
             if (index.HasValue)
             {
                 bool ok = _inv.AddAt(inventory, item, Math.Max(0, index.Value));
                 return ok ? RichResult.Success() : RichResult.Fail(ErrorCode.OperationFailed, "AddAt failed");
             }
             else
             {
                 bool ok = allowMerge ? _inv.AddAndMerge(inventory, item) : _inv.AddAt(inventory, item, 0);
                 return ok ? RichResult.Success() : RichResult.Fail(ErrorCode.OperationFailed, "Add failed");
             }
         }
         catch (Exception ex) { Log.Error("TryAddToInventory failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
     }

     /// <summary>从所在背包移除。</summary>
     public RichResult TryRemoveFromInventory(object item)
     {
         try { if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item null"); _inv.Detach(item); return RichResult.Success(); }
         catch (Exception ex) { Log.Error("TryRemoveFromInventory failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
     }

     /// <summary>在同一背包中从一个索引移动到另一个索引。</summary>
     public RichResult TryMoveInInventory(object inventory, int fromIndex, int toIndex)
     {
         try
         {
             if (inventory == null) return RichResult.Fail(ErrorCode.InvalidArgument, "inv null");
             var from = _inv.GetItemAt(inventory, fromIndex); if (from == null) return RichResult.Fail(ErrorCode.NotFound, "from null");
             if (fromIndex == toIndex) return RichResult.Success();
             _inv.Detach(from);
             bool ok = _inv.AddAt(inventory, from, Math.Max(0, toIndex));
             return ok ? RichResult.Success() : RichResult.Fail(ErrorCode.OperationFailed, "AddAt failed");
         }
         catch (Exception ex) { Log.Error("TryMoveInInventory failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
     }

     /// <summary>跨背包转移（可选目标索引/允许合并）。</summary>
     public RichResult TryTransferBetweenInventories(object item, object fromInventory, object toInventory, int? toIndex = null, bool allowMerge = true)
     {
         try
         {
             if (item == null || toInventory == null) return RichResult.Fail(ErrorCode.InvalidArgument, "null args");
             if (fromInventory != null) { try { _inv.Detach(item); } catch { } }
             return TryAddToInventory(item, toInventory, toIndex, allowMerge);
         }
         catch (Exception ex) { Log.Error("TryTransferBetweenInventories failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
     }

     /// <summary>通过引擎的 ItemUtilities 发送到玩家（可选不合并/发往仓库）。</summary>
     public RichResult TrySendToPlayer(object item, bool dontMerge = false, bool sendToStorage = true)
     {
         try
         {
             var util = Type.GetType("TeamSoda.Duckov.Core.ItemUtilities") ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("TeamSoda.Duckov.Core.ItemUtilities", false) ?? a.GetType("ItemUtilities", false)).FirstOrDefault(x => x != null);
             var itemT = Type.GetType("ItemStatsSystem.Item") ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ItemStatsSystem.Item", false)).FirstOrDefault(x => x != null);
             var m = util?.GetMethod("SendToPlayer", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static, null, new[]{ itemT, typeof(bool), typeof(bool) }, null)
                  ?? util?.GetMethod("SendToPlayer", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static, null, new[]{ itemT }, null);
             if (m == null) return RichResult.Fail(ErrorCode.DependencyMissing, "ItemUtilities.SendToPlayer not found");
             var ps = m.GetParameters();
             if (ps.Length == 3) m.Invoke(null, new object[]{ item, dontMerge, sendToStorage }); else m.Invoke(null, new object[]{ item });
             return RichResult.Success();
         }
         catch (Exception ex) { Log.Error("TrySendToPlayer failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
     }

     /// <summary>仅发送到玩家背包（不含仓库）。</summary>
     public RichResult TrySendToPlayerInventory(object item, bool dontMerge = false)
     {
         try
         {
             var util = Type.GetType("TeamSoda.Duckov.Core.ItemUtilities") ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("TeamSoda.Duckov.Core.ItemUtilities", false) ?? a.GetType("ItemUtilities", false)).FirstOrDefault(x => x != null);
             var itemT = Type.GetType("ItemStatsSystem.Item") ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ItemStatsSystem.Item", false)).FirstOrDefault(x => x != null);
             var m = util?.GetMethod("SendToPlayerCharacterInventory", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static, null, new[]{ itemT, typeof(bool) }, null)
                  ?? util?.GetMethod("SendToPlayerCharacterInventory", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
             if (m == null) return RichResult.Fail(ErrorCode.DependencyMissing, "ItemUtilities.SendToPlayerCharacterInventory not found");
             var ps = m.GetParameters();
             if (ps.Length == 2) m.Invoke(null, new object[]{ item, dontMerge }); else m.Invoke(null, new object[]{ item, dontMerge });
             return RichResult.Success();
         }
         catch (Exception ex) { Log.Error("TrySendToPlayerInventory failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
     }

     /// <summary>发送到仓库（可选直接进入缓冲区）。</summary>
     public RichResult TrySendToWarehouse(object item, bool directToBuffer = false)
     {
         try
         {
             var util = Type.GetType("TeamSoda.Duckov.Core.ItemUtilities") ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("TeamSoda.Duckov.Core.ItemUtilities", false) ?? a.GetType("ItemUtilities", false)).FirstOrDefault(x => x != null);
             var itemT = Type.GetType("ItemStatsSystem.Item") ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ItemStatsSystem.Item", false)).FirstOrDefault(x => x != null);
             var m = util?.GetMethod("SendToPlayerStorage", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static, null, new[]{ itemT, typeof(bool) }, null)
                  ?? util?.GetMethod("SendToPlayerStorage", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
             if (m == null) return RichResult.Fail(ErrorCode.DependencyMissing, "ItemUtilities.SendToPlayerStorage not found");
             var ps = m.GetParameters();
             if (ps.Length == 2) m.Invoke(null, new object[]{ item, directToBuffer }); else m.Invoke(null, new object[]{ item, directToBuffer });
             return RichResult.Success();
         }
         catch (Exception ex) { Log.Error("TrySendToWarehouse failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
     }

     /// <summary>从仓库缓冲区取出指定索引的物品。</summary>
     public RichResult TryTakeFromWarehouseBuffer(int index)
     {
         try
         {
             var t = Type.GetType("PlayerStorage") ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PlayerStorage", false)).FirstOrDefault(x => x != null);
             if (t == null) return RichResult.Fail(ErrorCode.DependencyMissing, "PlayerStorage not found");
             var m = t.GetMethod("TakeBufferItem", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static, null, new[]{ typeof(int) }, null)
                  ?? t.GetMethod("TakeBufferItem", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
             if (m == null) return RichResult.Fail(ErrorCode.NotSupported, "TakeBufferItem not found");
             m.Invoke(null, new object[]{ index });
             return RichResult.Success();
         }
         catch (Exception ex) { Log.Error("TryTakeFromWarehouseBuffer failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
     }

     /// <summary>在玩家附近丢到地面（智能匹配多种 Drop 重载）。</summary>
     public RichResult TryDropToWorldNearPlayer(object item, float radius = 1f)
     {
         try
         {
             if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item null");
             var target = ResolveItemComponent(item);
             if (target is UnityEngine.Object uo && uo == null) return RichResult.Fail(ErrorCode.NotFound, "item destroyed");

             var tItem = target.GetType();
             var cmcT = Type.GetType("TeamSoda.Duckov.Core.CharacterMainControl")
                        ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("TeamSoda.Duckov.Core.CharacterMainControl", false) ?? a.GetType("CharacterMainControl", false)).FirstOrDefault(x => x != null);
             object main = null; try { main = cmcT?.GetProperty("Main", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)?.GetValue(null, null); } catch { }
             // 计算玩家附近的位置
             Vector3 pos = Vector3.zero;
             try { var tr = (main as Component)?.transform ?? main?.GetType().GetProperty("transform")?.GetValue(main, null) as Transform; if (tr != null) pos = tr.position; } catch { }
             if (pos == Vector3.zero)
             {
                 try { var go = (target as Component)?.gameObject; if (go != null) pos = go.transform.position; } catch { }
             }
             var offset = UnityEngine.Random.insideUnitSphere * Math.Max(0.1f, radius); offset.y = 0f;
             var methods = tItem.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Where(m => m.Name == "Drop").ToArray();
             foreach (var m in methods)
             {
                 var ps = m.GetParameters();
                 try
                 {
                     if (ps.Length == 2)
                     {
                         if (cmcT != null && ps[0].ParameterType.IsAssignableFrom(cmcT)) { m.Invoke(target, new object[]{ main, true }); return RichResult.Success(); }
                         if (ps[0].ParameterType == typeof(Vector3) && ps[1].ParameterType == typeof(bool)) { m.Invoke(target, new object[]{ pos + offset, true }); return RichResult.Success(); }
                     }
                     else if (ps.Length == 4)
                     {
                         if (ps[0].ParameterType == typeof(Vector3) && ps[1].ParameterType == typeof(bool) && ps[2].ParameterType == typeof(Vector3) && ps[3].ParameterType == typeof(float))
                         { m.Invoke(target, new object[]{ pos + offset, true, Vector3.up, 0f }); return RichResult.Success(); }
                     }
                     else if (ps.Length == 1)
                     {
                         if (ps[0].ParameterType == typeof(Vector3)) { m.Invoke(target, new object[]{ pos + offset }); return RichResult.Success(); }
                     }
                     else if (ps.Length == 3)
                     {
                         if (ps[0].ParameterType == typeof(Vector3) && ps[1].ParameterType == typeof(bool) && ps[2].ParameterType == typeof(Vector3))
                         { m.Invoke(target, new object[]{ pos + offset, true, Vector3.up }); return RichResult.Success(); }
                     }
                 }
                 catch { }
             }
             return RichResult.Fail(ErrorCode.DependencyMissing, "Drop overloads not found");
         }
         catch (Exception ex) { Log.Error("TryDropToWorldNearPlayer failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
     }

     /// <summary>按坐标丢到地面（Drop(Vector3,bool,Vector3,float)）。</summary>
     public RichResult TryDropToWorld(object item, float x, float y, float z, bool usePhysics = true, float fx = 0f, float fy = 0f, float fz = 0f)
     {
         try
         {
             var method = item?.GetType().GetMethod("Drop", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, null, new[]{ typeof(Vector3), typeof(bool), typeof(Vector3), typeof(float) }, null);
             if (method == null) return RichResult.Fail(ErrorCode.NotSupported, "Drop(Vector3,bool,Vector3,float) not found");
             method.Invoke(item, new object[]{ new Vector3(x,y,z), usePhysics, new Vector3(fx,fy,fz), 0f });
             return RichResult.Success();
         }
         catch (Exception ex) { Log.Error("TryDropToWorld failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
     }

     /// <summary>拆分堆叠（返回新物品）。</summary>
     public RichResult<object> TrySplitStack(object item, int count)
     {
         try
         {
             if (item == null) return RichResult<object>.Fail(ErrorCode.InvalidArgument, "item null");
             var m = item.GetType().GetMethod("Split", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, null, new[]{ typeof(int) }, null);
             if (m == null) return RichResult<object>.Fail(ErrorCode.NotSupported, "Split not found");
             var utask = m.Invoke(item, new object[]{ count });
             // 若为 UniTask<T>，尝试读取同步结果
             var resProp = utask?.GetType().GetProperty("Result");
             var result = resProp != null ? resProp.GetValue(utask, null) : null;
             return result != null ? RichResult<object>.Success(result) : RichResult<object>.Fail(ErrorCode.OperationFailed, "Split returned null");
         }
         catch (Exception ex) { Log.Error("TrySplitStack failed", ex); return RichResult<object>.Fail(ErrorCode.OperationFailed, ex.Message); }
     }

     /// <summary>合并两件堆叠。</summary>
     public RichResult TryMergeStacks(object a, object b)
     {
         try
         {
             if (a == null || b == null) return RichResult.Fail(ErrorCode.InvalidArgument, "null args");
             var m = a.GetType().GetMethod("Combine", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, null, new[]{ b.GetType() }, null)
                   ?? a.GetType().GetMethod("Combine", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
             if (m == null) return RichResult.Fail(ErrorCode.NotSupported, "Combine not found");
             m.Invoke(a, new[]{ b });
             return RichResult.Success();
         }
         catch (Exception ex) { Log.Error("TryMergeStacks failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
     }

     /// <summary>对同类堆叠进行重新打包（尽量合并满堆）。</summary>
     public RichResult TryRepackStacks(object[] items)
     {
         try
         {
             if (items == null || items.Length == 0) return RichResult.Success();
             for (int i=0;i<items.Length;i++)
             {
                 for (int j=i+1;j<items.Length;j++)
                 {
                     var ai = items[i]; var bj = items[j]; if (ai == null || bj == null) continue;
                     if (_item.GetTypeId(ai) != _item.GetTypeId(bj)) continue;
                     try { TryMergeStacks(ai, bj); } catch { }
                 }
             }
             return RichResult.Success();
         }
         catch (Exception ex) { Log.Error("TryRepackStacks failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
     }
 }
}
