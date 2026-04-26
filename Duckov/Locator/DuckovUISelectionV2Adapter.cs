using ItemModKit.Core.Locator;

namespace ItemModKit.Adapters.Duckov.Locator
{
    internal sealed class DuckovUISelectionV2Adapter : IUISelectionV2
    {
        public bool TryGetCurrent(out IItemHandle handle)
        {
            handle = null;
            try
            {
                object raw;
                if (DuckovUISelectionResolver.TryGetCurrentItem(out raw) && raw != null)
                {
                    handle = DuckovHandleFactory.CreateItemHandle(raw);
                    return true;
                }
            }
            catch { }
            return false;
        }
        public bool TryGetCurrentInventory(out IInventoryHandle inventory)
        {
            inventory = null;
            try
            {
                IItemHandle handle;
                if (TryGetCurrent(out handle) && handle != null)
                {
                    inventory = IMKDuckov.Ownership.GetInventory(handle);
                    return inventory != null;
                }
            }
            catch { }
            return false;
        }
    }
}
