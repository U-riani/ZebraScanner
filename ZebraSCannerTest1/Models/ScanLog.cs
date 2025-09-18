using System.ComponentModel.DataAnnotations;

namespace ZebraSCannerTest1.Models
{
    public class ScanLog
    {
        [Key]
        public int Id { get; set; }

        public string Barcode { get; set; }
        public int Quantity { get; set; }

        // Add InitialQuantity
        public int InitialQuantity { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Optional: link back to ScannedProduct
        public int ScannedProductId { get; set; }
        public ScannedProduct ScannedProduct { get; set; }

        // Optional: computed properties for convenience
        public bool IsBelowInitial => Quantity < InitialQuantity;
        public bool IsEqualInitial => Quantity == InitialQuantity;
        public bool IsAboveInitial => Quantity > InitialQuantity;
    }
}
