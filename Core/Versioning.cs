using System;

namespace ItemModKit.Core
{
 [Flags]
 public enum IMKCapabilities
 {
 None =0,
 ItemAdapter =1 <<0,
 InventoryAdapter =1 <<1,
 SlotAdapter =1 <<2,
 Query =1 <<3,
 Persistence =1 <<4,
 Rebirth =1 <<5,
 UISelection =1 <<6,
 InventoryEvents =1 <<7,
 WorldDrops =1 <<8,
 Ownership =1 <<9,
 ExternalEventPublishing =1 <<10,
 Mutex =1 <<11,
 Logging =1 <<12,
 RichResults =1 <<13
 }

 public static class IMKVersion
 {
 public static readonly Version Version = new Version(0,1,0);
 public static readonly IMKCapabilities Capabilities =
 IMKCapabilities.ItemAdapter | IMKCapabilities.InventoryAdapter | IMKCapabilities.SlotAdapter |
 IMKCapabilities.Query | IMKCapabilities.Persistence | IMKCapabilities.Rebirth | IMKCapabilities.UISelection |
 IMKCapabilities.InventoryEvents | IMKCapabilities.WorldDrops | IMKCapabilities.Ownership |
 IMKCapabilities.ExternalEventPublishing | IMKCapabilities.Mutex | IMKCapabilities.Logging | IMKCapabilities.RichResults;

 public static bool Require(Version min, out string error)
 {
 if (Version >= min) { error = null; return true; }
 error = $"IMK version {Version} < required {min}"; return false;
 }
 }
}
