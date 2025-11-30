using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Core.Dtos
{
    public class ExcelSalesDto
    {
        public string Barcode { get; set; }
        public string? Name { get; set; }
        public string? Color { get; set; }
        public string? Size { get; set; }
        public string? SaleType { get; set; }
        public string? OldPrice { get; set; }
        public string? NewPrice { get; set; }
        public string? ArticCode { get; set; }
    }
}
