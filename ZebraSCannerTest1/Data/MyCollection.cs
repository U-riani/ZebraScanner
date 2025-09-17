using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZebraSCannerTest1.Models;

namespace ZebraSCannerTest1.Data
{
    public class MyCollection
    {
        public List<TestModel> Products { get; set; }
        
        public MyCollection(string prod)
        {
            Products.Add(new TestModel { Name = prod });
            
        }
    }
}
