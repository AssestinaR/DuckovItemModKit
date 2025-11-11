using System;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
 /// <summary>
 /// 轻量迁移器：保证物品元数据完整（Owner/名称/类型/品质/价值等），必要时写回并提升版本号。
 /// </summary>
 internal static class DuckovMigration
 {
 // Lightweight migrator: ensure meta exists and essential fields are filled; upgrade MetaVersion
 public static bool EnsureMigrated(IItemAdapter itemApi, IItemPersistence persist, object item)
 {
 if (itemApi == null || persist == null || item == null) return false;
 try
 {
 // Try read existing meta
 if (!persist.TryExtractMeta(item, out var m) || m == null)
 {
 var snap = ItemSnapshot.Capture(itemApi, item);
 var meta = Persistence.BuildMetaFromSnapshot(snap);
 meta.OwnerId = DuckovOwnership.CurrentOrInfer();
 meta.MetaVersion =1;
 persist.RecordMeta(item, meta, writeVariables: true);
 return true;
 }
 // Fill missing fields and bump version if needed
 bool changed = false;
 if (m.MetaVersion <=0) { m.MetaVersion =1; changed = true; }
 if (string.IsNullOrEmpty(m.NameKey)) { m.NameKey = itemApi.GetDisplayNameRaw(item) ?? itemApi.GetName(item); changed = true; }
 if (m.TypeId <=0) { m.TypeId = itemApi.GetTypeId(item); changed = true; }
 if (m.Quality <=0) { m.Quality = itemApi.GetQuality(item); changed = true; }
 if (m.DisplayQuality <=0) { m.DisplayQuality = itemApi.GetDisplayQuality(item); changed = true; }
 if (m.Value <=0) { m.Value = itemApi.GetValue(item); changed = true; }
 if (string.IsNullOrEmpty(m.OwnerId)) { m.OwnerId = DuckovOwnership.CurrentOrInfer(); changed = true; }
 if (changed)
 {
 persist.RecordMeta(item, m, writeVariables: true);
 return true;
 }
 return false;
 }
 catch (Exception ex) { Log.Warn($"[IMK/Migration] EnsureMigrated failed: {ex.Message}"); return false; }
 }
 }
}
