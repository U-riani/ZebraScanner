using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.Messages
{
    public class ProductUpdatedMessage
    {
        public Product Product { get; }
        public InventoryMode? Mode { get; }

        public ProductUpdatedMessage(Product p, InventoryMode? mode = null)
        {
            Product = p;
            Mode = mode;
        }
    }
}
