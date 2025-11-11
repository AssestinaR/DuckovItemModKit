using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    internal sealed partial class WriteService : IWriteService
    {
        private readonly IItemAdapter _item;
        public WriteService(IItemAdapter item) { _item = item; }
    }
}
