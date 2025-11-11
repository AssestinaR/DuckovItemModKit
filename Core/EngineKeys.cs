namespace ItemModKit.Core
{
 // Centralized engine keys to avoid typos and scattered literals
 public static class EngineKeys
 {
 public static class Variable
 {
 public const string Count = "Count";
 public const string Durability = "Durability";
 public const string DurabilityLoss = "DurabilityLoss";
 public const string Inspected = "Inspected";
 }

 public static class Constant
 {
 public const string MaxDurability = "MaxDurability";
 }

 public static class Property
 {
 public const string MaxStackCount = "MaxStackCount";
 public const string Inspecting = "Inspecting";
 public const string NeedInspection = "NeedInspection";
 public const string Inspected = "Inspected";
 public const string Main = "Main";
 public const string CharacterItem = "CharacterItem";
 public const string Inventory = "Inventory";
 }

 public static class Method
 {
 public const string Refresh = "Refresh";
 public const string SendToPlayer = "SendToPlayer";
 }
 }
}
