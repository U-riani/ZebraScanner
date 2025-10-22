using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.Messages
{
    public class ProductUpdatedMessage
    {
        public Product Product { get; }
        public ProductUpdatedMessage(Product p) => Product = p;
    }
}
