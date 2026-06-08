using System.ComponentModel.DataAnnotations;

namespace ZebraSCannerTest1.Core.Models
{
    public class InitialProduct
    {
        [Key]
        public int Id { get; set; }

        public string? Barcode { get; set; }
        public int Quantity { get; set; } // initial quantity

        // Static fields (metadata, not changed by scanning)
        public string? Name { get; set; }
        public string? Color { get; set; }
        public string? Size { get; set; }
        public string? Price { get; set; }
        public string? ArticCode { get; set; }
    }
}
