using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    internal sealed class DuckovCompatUISelectionFacade : IUISelection
    {
        public bool TryGetDetailsItem(out object item)
        {
            return DuckovUISelectionResolver.TryGetDetailsItem(out item);
        }

        public bool TryGetOperationMenuItem(out object item)
        {
            return DuckovUISelectionResolver.TryGetOperationMenuItem(out item);
        }

        public bool TryGetCurrentItem(out object item)
        {
            return DuckovUISelectionResolver.TryGetCurrentItem(out item);
        }
    }
}