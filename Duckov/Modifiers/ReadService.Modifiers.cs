using System;
using System.Collections.Generic;
using System.Reflection;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 读取服务（Modifiers）：提供修饰器与修饰器描述的只读查询。
    /// </summary>
    internal sealed partial class ReadService
    {
        /// <summary>读取修饰器列表。</summary>
        public RichResult<ModifierEntry[]> TryReadModifiers(object item)
        {
            try
            {
                if (item == null) return RichResult<ModifierEntry[]>.Fail(ErrorCode.InvalidArgument, "item is null");
                return RichResult<ModifierEntry[]>.Success(_item.GetModifiers(item));
            }
            catch (Exception ex)
            {
                Log.Error("TryReadModifiers failed", ex);
                return RichResult<ModifierEntry[]>.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>读取修饰器描述信息（Key/Type/Value/Order/Display/Target）。</summary>
        public RichResult<ModifierDescriptionInfo[]> TryReadModifierDescriptions(object item)
        {
            try
            {
                if (item == null) return RichResult<ModifierDescriptionInfo[]>.Fail(ErrorCode.InvalidArgument, "item is null");
                var modsCol = DuckovReflectionCache.GetGetter(item.GetType(), "Modifiers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(item) as System.Collections.IEnumerable;
                if (modsCol == null) return RichResult<ModifierDescriptionInfo[]>.Success(Array.Empty<ModifierDescriptionInfo>());

                var list = new List<ModifierDescriptionInfo>();
                foreach (var m in modsCol)
                {
                    if (m == null) continue;
                    var dto = new ModifierDescriptionInfo();
                    try { dto.Key = Convert.ToString(DuckovTypeUtils.GetMaybe(m, new[] { "Key", "key" })); } catch { }
                    try { var t = DuckovTypeUtils.GetMaybe(m, new[] { "Type", "type" }); dto.Type = t != null ? Convert.ToString(t) : null; } catch { }
                    try { var v = DuckovTypeUtils.GetMaybe(m, new[] { "Value", "value" }); dto.Value = DuckovTypeUtils.ConvertToFloat(v); } catch { }
                    try { var o = DuckovTypeUtils.GetMaybe(m, new[] { "Order", "order" }); if (o != null) dto.Order = Convert.ToInt32(o); } catch { }
                    try { var d = DuckovTypeUtils.GetMaybe(m, new[] { "Display", "display" }); if (d != null) dto.Display = Convert.ToBoolean(d); } catch { }
                    try { var tg = DuckovTypeUtils.GetMaybe(m, new[] { "Target", "target" }); dto.Target = tg != null ? Convert.ToString(tg) : null; } catch { }
                    try { var enabled = DuckovTypeUtils.GetMaybe(m, new[] { "EnableInInventory", "enableInInventory" }); if (enabled != null) dto.EnableInInventory = Convert.ToBoolean(enabled); } catch { }
                    list.Add(dto);
                }

                return RichResult<ModifierDescriptionInfo[]>.Success(list.ToArray());
            }
            catch (Exception ex)
            {
                Log.Error("TryReadModifierDescriptions failed", ex);
                return RichResult<ModifierDescriptionInfo[]>.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }
    }
}