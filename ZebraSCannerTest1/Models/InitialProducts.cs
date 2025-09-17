using System.ComponentModel.DataAnnotations;

namespace ZebraSCannerTest1.Models
{
    public class InitialProduct
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; }

        public int Quantity { get; set; } // initial quantity
    }
}
