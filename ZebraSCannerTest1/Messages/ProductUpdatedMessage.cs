using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZebraSCannerTest1.Models;

namespace ZebraSCannerTest1.Messages;

public class ProductUpdatedMessage
{
    public TestModel Product { get; }

    public ProductUpdatedMessage(TestModel product)
    {
        Product = product;
    }
}
