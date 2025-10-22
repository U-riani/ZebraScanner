using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Core.Dtos
{
    public class JsonDto
    {

            public string Barcode { get; set; }
            public int InitialQuantity { get; set; }
            public int ScannedQuantity { get; set; }
            public string CreatedAt { get; set; }
            public string UpdatedAt { get; set; }
            public string Name { get; set; }
            public string Color { get; set; }
            public string Size { get; set; }
            public string Price { get; set; }
            public string ArticCode { get; set; }
        }
    
}
