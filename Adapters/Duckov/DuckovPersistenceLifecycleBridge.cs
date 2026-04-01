using System;
using ItemStatsSystem;
using ItemStatsSystem.Data;
using UnityEngine;

namespace ItemModKit.Adapters.Duckov
{
    internal static class DuckovPersistenceLifecycleBridge
    {
        private static bool _initialized;
        private static bool _liveItemsBootstrapped;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            try
            {
                ItemTreeData.OnItemLoaded += OnItemLoaded;
            }
            catch (Exception ex)
            {
                Core.Log.Warn("DuckovPersistenceLifecycleBridge.Initialize subscribe failed: " + ex.Message);
            }

            if (!_liveItemsBootstrapped)
            {
                TryApplyToLiveItems();
                _liveItemsBootstrapped = true;
            }
        }

        public static void Dispose()
        {
            if (!_initialized)
            {
                return;
            }

            _initialized = false;
            try
            {
                ItemTreeData.OnItemLoaded -= OnItemLoaded;
            }
            catch
            {
            }
        }

        internal static void SyncBeforeSerialize(Item item)
        {
            if (!ShouldProcessItem(item))
            {
                return;
            }

            if (!DuckovSlotProvisioningDraft.HasPersistedDefinitions(item))
            {
                return;
            }

            try
            {
                DuckovSlotProvisioningDraft.TrySyncPersistedDefinitionsFromRuntime(item);
            }
            catch (Exception ex)
            {
                Core.Log.Warn("DuckovPersistenceLifecycleBridge.SyncBeforeSerialize slot sync failed: " + ex.Message);
            }
        }

        private static void OnItemLoaded(Item item)
        {
            if (!ShouldProcessItem(item))
            {
                return;
            }

            try
            {
                IMKDuckov.Persistence.EnsureApplied(item);
            }
            catch (Exception ex)
            {
                Core.Log.Warn("DuckovPersistenceLifecycleBridge.OnItemLoaded failed: " + ex.Message);
            }
        }

        private static void TryApplyToLiveItems()
        {
            try
            {
                var items = UnityEngine.Object.FindObjectsOfType<Item>(true);
                if (items == null)
                {
                    return;
                }

                for (var index = 0; index < items.Length; index++)
                {
                    var item = items[index];
                    if (!ShouldProcessItem(item))
                    {
                        continue;
                    }

                    OnItemLoaded(item);
                }
            }
            catch
            {
            }
        }

        private static bool ShouldProcessItem(Item item)
        {
            if (item == null)
            {
                return false;
            }

            try
            {
                if (DuckovSlotProvisioningDraft.HasPersistedDefinitions(item))
                {
                    return true;
                }

                if (DuckovResourceProvisioningDraft.HasPersistedDefinition(item))
                {
                    return true;
                }

                return IMKDuckov.Persistence.ShouldConsider(item);
            }
            catch
            {
                return false;
            }
        }
    }
}