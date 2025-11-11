using System;
using System.Text;
using ItemModKit.Core;
using static ItemModKit.Adapters.Duckov.DuckovTypeUtils;

namespace ItemModKit.Adapters.Duckov
{
 /// <summary>
 /// Duckov 物品的持久化适配器：记录/提取 IMK 元数据 ItemMeta。
 /// - 变量写入：Owner/Name/Desc/TypeId/Quality/DisplayQuality/Value
 /// - 嵌入 JSON：将 Meta 序列化后 Base64 -> VarMeta
 /// - EnsureApplied：应用基本字段、重放修饰器、按需应用扩展块（一次性）
 /// </summary>
 internal sealed class DuckovPersistenceAdapter : IItemPersistence
 {
 private const int CurrentMetaVersion =1;
 private const string Marker = "[IMK]";
 // 变量键名常量
 private const string VarMeta = "IMK_Meta";
 private const string VarName = "IMK_Name";
 private const string VarDesc = "IMK_Desc";
 private const string VarTypeId = "IMK_TypeID";
 private const string VarQuality = "IMK_Quality";
 private const string VarDisplayQuality = "IMK_DisplayQuality";
 private const string VarValue = "IMK_Value";
 private const string VarOwner = "IMK_Owner";
 // 已应用扩展块的实例缓存（避免重复重放）
 private static readonly System.Collections.Generic.HashSet<int> s_extensionsApplied = new System.Collections.Generic.HashSet<int>();
 private readonly IItemAdapter _item;
 public DuckovPersistenceAdapter(IItemAdapter item) { _item = item; }

 /// <summary>
 /// 记录元数据：设置版本、嵌入、写基本属性、可选写变量；必要时重新应用修饰器。
 /// </summary>
 public void RecordMeta(object item, ItemMeta meta, bool writeVariables)
 {
 if (item == null || meta == null) return;
 try
 {
 if (string.IsNullOrEmpty(meta.OwnerId)) meta.OwnerId = DuckovOwnership.CurrentOrInfer();
 meta.MetaVersion = CurrentMetaVersion;
 EmbedMeta(item, meta);
 ApplyBasic(item, meta);
 if (writeVariables) WriteVars(item, meta);
 if (PersistenceSettings.Current.ReapplyAfterWrite)
 {
 try { _item.ReapplyModifiers(item); } catch { }
 }
 }
 catch { }
 }

 /// <summary>
 /// 提取元数据：优先读取嵌入的 JSON；失败则回退读取变量键集合。
 /// </summary>
 public bool TryExtractMeta(object item, out ItemMeta meta)
 {
 meta = null; if (item == null) return false;
 try
 {
 if (TryReadEmbedded(item, out meta) && meta != null)
 {
 if (meta.MetaVersion <=0) meta.MetaVersion = CurrentMetaVersion;
 if (meta.FormatVersion <=0) meta.FormatVersion =1; // minimal migration
 return true;
 }
 var m = new ItemMeta(); bool any = false;
 try { var v = _item.GetVariable(item, VarOwner) as string; if (!string.IsNullOrEmpty(v)) { m.OwnerId = v; any = true; } } catch { }
 try { var v = _item.GetVariable(item, VarName) as string; if (!string.IsNullOrEmpty(v)) { m.NameKey = v; any = true; } } catch { }
 try { var v = _item.GetVariable(item, VarDesc) as string; if (!string.IsNullOrEmpty(v)) { m.RemarkKey = v; any = true; } } catch { }
 try { var v = _item.GetVariable(item, VarTypeId); if (v != null) { m.TypeId = ToInt(v); any = true; } } catch { }
 try { var v = _item.GetVariable(item, VarQuality); if (v != null) { m.Quality = ToInt(v); any = true; } } catch { }
 try { var v = _item.GetVariable(item, VarDisplayQuality); if (v != null) { m.DisplayQuality = ToInt(v); any = true; } } catch { }
 try { var v = _item.GetVariable(item, VarValue); if (v != null) { m.Value = ToInt(v); any = true; } } catch { }
 if (string.IsNullOrEmpty(m.OwnerId)) m.OwnerId = DuckovOwnership.CurrentOrInfer();
 if (any) { m.MetaVersion = CurrentMetaVersion; m.FormatVersion =1; meta = m; return true; }
 return false;
 }
 catch { meta = null; return false; }
 }

 /// <summary>
 /// 确保应用元数据：若成功提取则写基本属性、重放修饰器，且扩展块只应用一次。
 /// </summary>
 public bool EnsureApplied(object item)
 {
 if (item == null) return false;
 try
 {
 if (!TryExtractMeta(item, out var m) || m == null) return false;
 ApplyBasic(item, m);
 _item.ReapplyModifiers(item);
 int id = DuckovTypeUtils.GetStableId(item);
 if (!s_extensionsApplied.Contains(id))
 {
 try { ItemStateExtensions.TryApply(item, m); } catch { }
 s_extensionsApplied.Add(id);
 }
 return true;
 }
 catch { return false; }
 }

 /// <summary>
 /// 清理模板上的效果组件（统一禁用）。
 /// </summary>
 public void ClearTemplateEffects(object item)
 {
 try
 {
 var effects = item?.GetType().GetMethod("GetComponentsInChildren", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance)?.Invoke(item, new object[]{ FindType("ItemStatsSystem.Effect"), true }) as Array;
 if (effects == null) return;
 foreach (var e in effects) { try { var enP = e.GetType().GetProperty("enabled"); enP?.SetValue(e, false, null); } catch { } }
 }
 catch { }
 }

 /// <summary>
 /// 判断是否“应当考虑”为 IMK 管理对象：存在嵌入块或任意 IMK 变量。
 /// </summary>
 public bool ShouldConsider(object item)
 {
 if (item == null) return false;
 try
 {
 if (HasEmbedded(item)) return true;
 foreach (var k in new[]{ VarMeta, VarOwner, VarName, VarDesc, VarTypeId, VarQuality, VarDisplayQuality, VarValue })
 {
 var v = _item.GetVariable(item, k); if (v != null) return true;
 }
 return false;
 }
 catch { return false; }
 }

 /// <summary>应用基本属性（名字/品质/价值等）。</summary>
 private void ApplyBasic(object item, ItemMeta meta)
 {
 try
 {
 if (!string.IsNullOrEmpty(meta.NameKey)) { _item.SetDisplayNameRaw(item, meta.NameKey); _item.SetName(item, meta.NameKey); }
 if (!string.IsNullOrEmpty(meta.RemarkKey)) { try { _item.SetVariable(item, VarDesc, meta.RemarkKey, true); } catch { } }
 if (meta.TypeId >0) _item.SetTypeId(item, meta.TypeId);
 if (meta.Quality >0) _item.SetQuality(item, meta.Quality);
 if (meta.DisplayQuality >0) _item.SetDisplayQuality(item, meta.DisplayQuality);
 if (meta.Value >0) _item.SetValue(item, meta.Value);
 }
 catch { }
 }

 /// <summary>写入变量形式的元数据（非嵌入 JSON）。</summary>
 private void WriteVars(object item, ItemMeta meta)
 {
 try { if (!string.IsNullOrEmpty(meta.OwnerId)) _item.SetVariable(item, VarOwner, meta.OwnerId, true); } catch { }
 try { if (!string.IsNullOrEmpty(meta.NameKey)) _item.SetVariable(item, VarName, meta.NameKey, true); } catch { }
 try { if (!string.IsNullOrEmpty(meta.RemarkKey)) _item.SetVariable(item, VarDesc, meta.RemarkKey, true); } catch { }
 try { if (meta.TypeId >0) _item.SetVariable(item, VarTypeId, meta.TypeId, true); } catch { }
 try { if (meta.Quality >0) _item.SetVariable(item, VarQuality, meta.Quality, true); } catch { }
 try { if (meta.DisplayQuality >0) _item.SetVariable(item, VarDisplayQuality, meta.DisplayQuality, true); } catch { }
 try { if (meta.Value >0) _item.SetVariable(item, VarValue, meta.Value, true); } catch { }
 }

 /// <summary>
 /// 将 ItemMeta 序列化为 JSON + Base64 并嵌入到专用变量 VarMeta。
 /// </summary>
 private void EmbedMeta(object item, ItemMeta meta)
 {
 try
 {
 var json = Newtonsoft.Json.JsonConvert.SerializeObject(meta) ?? string.Empty;
 var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
 var blob = Marker + b64;
 try { _item.SetVariable(item, VarMeta, blob, true); } catch { }
 }
 catch { }
 }

 /// <summary>
 /// 尝试读取嵌入块，支持标记前缀或直接 JSON；并验证可选校验和。
 /// </summary>
 private bool TryReadEmbedded(object item, out ItemMeta meta)
 {
 meta = null; try
 {
 var blobO = _item.GetVariable(item, VarMeta); var blob = blobO as string;
 if (string.IsNullOrEmpty(blob)) return false;
 string json = null;
 if (blob.StartsWith(Marker, System.StringComparison.Ordinal))
 {
 var b64 = blob.Substring(Marker.Length);
 try { json = Encoding.UTF8.GetString(Convert.FromBase64String(b64)); } catch { json = null; }
 }
 else if (blob.TrimStart().StartsWith("{"))
 {
 json = blob;
 }
 if (string.IsNullOrEmpty(json)) return false;
 try { meta = Newtonsoft.Json.JsonConvert.DeserializeObject<ItemMeta>(json); } catch { meta = null; }
 if (meta != null && string.IsNullOrEmpty(meta.OwnerId)) meta.OwnerId = DuckovOwnership.CurrentOrInfer();
 // 校验嵌入扩展块
 if (meta != null && !string.IsNullOrEmpty(meta.EmbeddedJson) && !string.IsNullOrEmpty(meta.ExtraChecksum))
 {
 try
 {
 var raw = meta.EmbeddedJson;
 string decoded = raw;
 if (!raw.TrimStart().StartsWith("{") && !raw.TrimStart().StartsWith("["))
 {
 try { decoded = Encoding.UTF8.GetString(Convert.FromBase64String(raw)); } catch { decoded = raw; }
 }
 var recomputed = ComputeChecksum(decoded);
 if (!string.Equals(recomputed, meta.ExtraChecksum, System.StringComparison.OrdinalIgnoreCase))
 {
 meta.EmbeddedJson = null; meta.ExtraChecksum = null;
 }
 }
 catch { meta.EmbeddedJson = null; meta.ExtraChecksum = null; }
 }
 return meta != null;
 }
 catch { meta = null; return false; }
 }

 private static int ToInt(object v)
 { try { if (v == null) return 0; if (v is int i) return i; if (v is long l) return (int)l; if (v is float f) return (int)f; if (v is double d) return (int)d; int x; if (int.TryParse(v.ToString(), out x)) return x; } catch { } return 0; }
 private static bool HasEmbedded(object item)
 {
 try
 {
 var m = DuckovTypeUtils.GetProp<string>(item, VarMeta); if (!string.IsNullOrEmpty(m)) return true;
 }
 catch { }
 return false;
 }

 /// <summary>获取 OwnerId（变量优先，其次尝试提取 Meta）。</summary>
 public static string GetOwnerId(object item, IItemAdapter adapter)
 { try { var v = adapter.GetVariable(item, VarOwner) as string; if (!string.IsNullOrEmpty(v)) return v; if (new DuckovPersistenceAdapter(adapter).TryExtractMeta(item, out var m) && m != null) return m.OwnerId; } catch { } return null; }
 /// <summary>判断物品是否属于指定 OwnerId。</summary>
 public static bool IsOwnedBy(object item, string ownerId, IItemAdapter adapter)
 { try { var o = GetOwnerId(item, adapter); return !string.IsNullOrEmpty(o) && string.Equals(o, ownerId, System.StringComparison.Ordinal); } catch { return false; } }

 private static string ComputeChecksum(string json)
 {
 try
 {
 using (var crc32 = new ItemModKit.Core.Crc32())
 {
 var data = Encoding.UTF8.GetBytes(json ?? string.Empty);
 var hash = crc32.ComputeHash(data);
 return BitConverter.ToString(hash).Replace("-", string.Empty);
 }
 }
 catch { return null; }
 }
 }
}
