using System;
using System.Collections.Generic;

namespace ItemModKit.Core.Locator
{
    // Lightweight, engine-agnostic item handle (weakly references runtime object; survives replace via logicalId rebinding)
    public interface IItemHandle
    {
        bool IsAlive { get; }
        object TryGetRaw();
        int? InstanceId { get; }
        string LogicalId { get; }
        // Internal logical id rebinding (used by logical id map)
        void RebindLogical(string newId);
        // Cached metadata (populated once at handle creation)
        int TypeId { get; }
        string DisplayName { get; }
        string[] Tags { get; }
        void RefreshMetadata();
    }

    // Central locating entrypoint
    public interface IItemLocator
    {
        IItemHandle FromInstance(object raw);
        IItemHandle FromInstanceId(int instanceId);
        IItemHandle FromLogicalId(string id);
        IItemHandle FromUISelection();
        IItemHandle LastCreated();
        // Predicate/object kept generic at skeleton stage; will converge to Query API
        IItemHandle[] Query(object predicate = null, IItemScope scope = null);
    }

    // Index for fast lookups and event-driven updates
    public interface IItemIndex
    {
        void OnCreated(object raw);
        void OnDestroyed(object raw);
        void OnMoved(object raw, object newContainer = null);
        IItemHandle FindByInstanceId(int instanceId);
        IItemHandle[] FindAllByTypeId(int typeId);
    }

    // Inventory classifier (player/storage/lootbox/world/etc.)
    public interface IInventoryClassifier
    {
        InventoryKind ClassifyInventory(object inv);
        bool IsLootBox(object inv);
        bool IsPlayerInventory(object inv);
        bool IsStorage(object inv);
    }

    // Scope hint; pluggable so different adapters can decide inclusion rules
    public interface IItemScope
    {
        bool Includes(object rawItem, object inventory, object ownerItem);
    }

    public enum InventoryKind
    {
        Unknown = 0,
        Player = 1,
        Storage = 2,
        LootBox = 3,
        World = 4,
        Other = 5,
    }

    public interface IInventoryHandle
    {
        int InstanceId { get; }
        int Capacity { get; }
        InventoryKind Kind { get; }
        IItemHandle OwnerItem { get; }
        object Raw { get; }
    }
    public interface ISlotHandle
    {
        string Key { get; }
        IItemHandle Owner { get; }
        bool Occupied { get; }
        IItemHandle Content { get; }
        object Raw { get; }
    }
    public interface IOwnershipService
    {
        IItemHandle GetOwner(IItemHandle item);
        IItemHandle GetCharacterRoot(IItemHandle item);
        IInventoryHandle GetInventory(IItemHandle item);
        ISlotHandle GetSlot(IItemHandle item);
    }
    public interface IItemQuery
    {
        IItemQuery ByTypeId(int typeId);
        IItemQuery InInventory(IInventoryHandle inventory);
        IItemQuery InScope(IItemScope scope);
        IItemQuery ByTags(params string[] tags); // AND semantics
        IItemQuery ByTagAny(params string[] tags); // OR semantics
        IItemQuery NameContains(string part);
        IItemQuery OwnedBy(IItemHandle ownerRoot); // owner chain includes specified root
        IItemQuery Equipped(bool equipped = true); // match IsEquipped flag
        IItemQuery Depth(int min, int max); // ownership chain depth in [min,max]
        IItemHandle First();
        IItemHandle[] Take(int count);
        IItemHandle[] All();
        IItemQuery ResetPredicates();
    }
    public interface IUISelectionV2
    {
        bool TryGetCurrent(out IItemHandle handle);
        bool TryGetCurrentInventory(out IInventoryHandle inventory);
    }
    public interface ILogicalIdMap
    {
        void Bind(IItemHandle oldItem, IItemHandle newItem);
        IItemHandle Resolve(string logicalId);
        bool TryGetLogicalId(IItemHandle item, out string logicalId);
        void Unbind(IItemHandle item);
    }
}
