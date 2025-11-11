using System;

namespace ItemModKit.Core
{
 public static class Persistence
 {
 public static ItemMeta BuildMetaFromSnapshot(ItemSnapshot s)
 {
 return new ItemMeta
 {
 NameKey = s?.NameRaw ?? s?.Name,
 TypeId = s?.TypeId ??0,
 Quality = s?.Quality ??0,
 DisplayQuality = s?.DisplayQuality ??0,
 Value = s?.Value ??0,
 // Affixes left to adapters to fill (game-specific)
 };
 }
 }

 public sealed class ItemMeta
 {
 public int MetaVersion { get; set; } // New: version for migrations
 public int FormatVersion { get; set; } // New: schema version for EmbeddedJson
 public string NameKey { get; set; }
 public string RemarkKey { get; set; }
 public int TypeId { get; set; }
 public int Quality { get; set; }
 public int DisplayQuality { get; set; }
 public int Value { get; set; }
 public string OwnerId { get; set; } // which mod owns this meta
 // Adapter-specific payloads can attach here via extension members or JSON blob
 public string EmbeddedJson { get; set; }
 public string ExtraChecksum { get; set; } // checksum for EmbeddedJson
 }
}
