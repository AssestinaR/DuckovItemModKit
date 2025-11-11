using System;

namespace ItemModKit.Core
{
 public static class KeyHelper
 {
 // Build a namespaced variable key for a specific owner/mod, e.g. <Owner>_MyVar
 public static string BuildOwnedKey(string ownerId, string key)
 {
 if (string.IsNullOrEmpty(key)) return key;
 var owner = string.IsNullOrEmpty(ownerId) ? "Unknown" : ownerId.Trim();
 return owner + "_" + key.Trim();
 }

 // Make a unique key by appending an increasing integer suffix if needed
 // e.g. desired "Socket" -> "Socket", "Socket1", "Socket2" ...
 public static string NextIncrementalKey(System.Collections.Generic.ISet<string> existingKeys, string desired)
 {
 if (string.IsNullOrEmpty(desired)) desired = "Key";
 if (existingKeys == null || !existingKeys.Contains(desired)) return desired;
 int n = 1;
 string baseName = desired;
 while (existingKeys.Contains(baseName + n)) n++;
 return baseName + n;
 }
 }
}
