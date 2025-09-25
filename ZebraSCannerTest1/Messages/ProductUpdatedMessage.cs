using ZebraSCannerTest1.Models;

namespace ZebraSCannerTest1.Messages
{
    public class ProductUpdatedMessage
    {
        public Product Product { get; }
        public ProductUpdatedMessage(Product p) => Product = p;
    }
}
