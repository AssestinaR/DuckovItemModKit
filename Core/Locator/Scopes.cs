using System;

namespace ItemModKit.Core.Locator
{
    // Simple scopes; will be extended
    internal sealed class AnyScope : IItemScope
    {
        public static readonly AnyScope Instance = new AnyScope();
        public bool Includes(object rawItem, object inventory, object ownerItem) => true;
    }

    internal sealed class LootBoxScope : IItemScope
    {
        private readonly IInventoryClassifier _classifier;
        public LootBoxScope(IInventoryClassifier classifier) { _classifier = classifier; }
        public bool Includes(object rawItem, object inventory, object ownerItem)
        {
            if (inventory == null) return false;
            return _classifier.IsLootBox(inventory);
        }
    }
}
