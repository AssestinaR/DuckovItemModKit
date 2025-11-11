using System;

namespace ItemModKit.Core
{
 public interface IItemAdapter
 {
 // Basic info
 string GetName(object item);
 void SetName(object item, string name);
 string GetDisplayNameRaw(object item);
 void SetDisplayNameRaw(object item, string raw);
 int GetTypeId(object item);
 void SetTypeId(object item, int typeId);
 int GetQuality(object item);
 void SetQuality(object item, int quality);
 int GetDisplayQuality(object item);
 void SetDisplayQuality(object item, int dq);
 int GetValue(object item);
 void SetValue(object item, int value);

 // Variables
 VariableEntry[] GetVariables(object item);
 void SetVariable(object item, string key, object value, bool constant);
 object GetVariable(object item, string key);
 bool RemoveVariable(object item, string key);

 // Constants (engine read-only-ish metadata exposed via separate collection)
 VariableEntry[] GetConstants(object item);
 void SetConstant(object item, string key, object value, bool createIfNotExist);
 object GetConstant(object item, string key);
 bool RemoveConstant(object item, string key);

 // Modifiers / Slots / Tags
 ModifierEntry[] GetModifiers(object item);
 void ReapplyModifiers(object item);
 SlotEntry[] GetSlots(object item);
 string[] GetTags(object item);
 void SetTags(object item, string[] tags);
 }

 public interface IInventoryAdapter
 {
 bool IsInInventory(object item);
 object GetInventory(object item);
 int GetCapacity(object inventory);
 object GetItemAt(object inventory, int index);
 int IndexOf(object inventory, object item);
 bool AddAt(object inventory, object item, int index);
 bool AddAndMerge(object inventory, object item);
 void Detach(object item);
 }

 public interface ISlotAdapter
 {
 bool TryPlugToCharacter(object newItem, int preferredFirstIndex =0);
 }

 public interface IItemPersistence
 {
 void RecordMeta(object item, ItemMeta meta, bool writeVariables);
 bool TryExtractMeta(object item, out ItemMeta meta);
 bool EnsureApplied(object item);
 void ClearTemplateEffects(object item);
 bool ShouldConsider(object item);
 }

 // Query & Locate (by inventory, storage, slots, etc.)
 public interface IItemQuery
 {
 // Backpack / Storage by1-based index (user friendly). Implementations may accept0-based too.
 bool TryGetFromBackpack(int index1Based, out object item);
 bool TryGetFromStorage(int index1Based, out object item);
 bool TryGetFromAnyInventory(int index1Based, out object item);
 bool TryGetWeaponSlot(int slotIndex1Based, out object item);
 // Enumerations
 System.Collections.Generic.IEnumerable<object> EnumerateBackpack();
 System.Collections.Generic.IEnumerable<object> EnumerateStorage();
 System.Collections.Generic.IEnumerable<object> EnumerateAllInventories();
 }

 // UI selection helpers (current details/operation menu target)
 public interface IUISelection
 {
 bool TryGetDetailsItem(out object item);
 bool TryGetOperationMenuItem(out object item);
 bool TryGetCurrentItem(out object item); // prefer operation menu, then details
 }

 // Event/Subscription sources
 public interface IItemEventSource
 {
 event System.Action<object> OnItemAdded;
 event System.Action<object> OnItemRemoved;
 event System.Action<object> OnItemChanged;
 }

 public interface IWorldDropEventSource
 {
 event System.Action<object> OnEnemyDrop;
 event System.Action<object> OnEnvironmentDrop;
 }

 // Rebirth/Replace service (UI refresh via item replacement)
 public interface IRebirthService
 {
 // Replace old item with a new one (applies meta), optionally keeping location when possible.
 RichResult<object> ReplaceRebirth(object oldItem, ItemMeta meta, bool keepLocation = true);
 }

 // Data contracts
 public struct VariableEntry { public string Key; public object Value; public bool Constant; }
 public struct ModifierEntry { public string Key; public float Value; public string Modifier; public bool IsPercent; }
 public struct SlotEntry { public string Key; public bool Occupied; public string PlugType; }

 // Optional handles for locate results
 public readonly struct ItemHandle
 {
 public readonly object Item; public readonly object Inventory; public readonly int Index1Based; public readonly string SlotKey;
 public ItemHandle(object item, object inventory, int index1Based, string slotKey) { Item = item; Inventory = inventory; Index1Based = index1Based; SlotKey = slotKey; }
 public bool HasInventory => Inventory != null;
 public bool HasSlot => !string.IsNullOrEmpty(SlotKey);
 }

 public enum ErrorCode
 {
 None=0,
 NotFound,
 InvalidArgument,
 OutOfRange,
 DependencyMissing,
 OperationFailed,
 NotSupported,
 Unauthorized,
 Conflict
 }

 public readonly struct RichResult
 {
 public bool Ok { get; }
 public string Error { get; }
 public ErrorCode Code { get; }
 public static RichResult Success() => new RichResult(true, null, ErrorCode.None);
 public static RichResult Fail(ErrorCode code, string err) => new RichResult(false, err, code);
 private RichResult(bool ok, string error, ErrorCode code) { Ok = ok; Error = error; Code = code; }
 }

 public readonly struct RichResult<T>
 {
 public bool Ok { get; }
 public string Error { get; }
 public ErrorCode Code { get; }
 public T Value { get; }
 public static RichResult<T> Success(T v) => new RichResult<T>(true, null, ErrorCode.None, v);
 public static RichResult<T> Fail(ErrorCode code, string err) => new RichResult<T>(false, err, code, default(T));
 private RichResult(bool ok, string error, ErrorCode code, T v) { Ok = ok; Error = error; Code = code; Value = v; }
 }

 public interface IItemFactory
 {
     RichResult<object> TryInstantiateByTypeId(int typeId);
     RichResult<object> TryInstantiateFromPrefab(object prefab);
     RichResult<object> TryGenerateByTypeId(int typeId); // simple wrapper over Instantiate; adapters may add fallbacks
     RichResult TryRegisterDynamicEntry(object prefab);
     RichResult<object> TryCloneItem(object item);
     RichResult TryDeleteItem(object item);
 }
 
 public interface IItemMover
 {
     // Inventory
     RichResult TryAddToInventory(object item, object inventory, int? index = null, bool allowMerge = true);
     RichResult TryRemoveFromInventory(object item);
     RichResult TryMoveInInventory(object inventory, int fromIndex, int toIndex);
     RichResult TryTransferBetweenInventories(object item, object fromInventory, object toInventory, int? toIndex = null, bool allowMerge = true);
     // Send
     RichResult TrySendToPlayer(object item, bool dontMerge = false, bool sendToStorage = true);
     RichResult TrySendToPlayerInventory(object item, bool dontMerge = false);
     RichResult TrySendToWarehouse(object item, bool directToBuffer = false);
     RichResult TryTakeFromWarehouseBuffer(int index);
     // World
     RichResult TryDropToWorldNearPlayer(object item, float radius = 1f);
     RichResult TryDropToWorld(object item, float x, float y, float z, bool usePhysics = true, float fx = 0f, float fy = 0f, float fz = 0f);
     // Stacks
     RichResult<object> TrySplitStack(object item, int count);
     RichResult TryMergeStacks(object a, object b);
     RichResult TryRepackStacks(object[] items);
 }

 public interface IVariableMergeService
 {
     // Merge variables from source->target according to mode; acceptKey optional filter (null = allow all)
     void Merge(object source, object target, VariableMergeMode mode, Func<string,bool> acceptKey = null);
 }

 public interface IUIRefreshService
 {
     void RefreshInventory(object inventory, bool markNeedInspection = true);
 }

 public interface IInventoryResolver
 {
     object Resolve(string target); // target: character|storage|null(auto)
     object ResolveFallback();
 }

 public interface IInventoryPlacementService
 {
     // Attempts to add; may schedule deferred retry. Returns (added, index, deferredScheduled)
     (bool added, int index, bool deferredScheduled) TryPlace(object inventory, object item, bool allowMerge = true, bool enableDeferredRetry = true);
 }
}
