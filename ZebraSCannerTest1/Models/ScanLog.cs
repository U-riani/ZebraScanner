using System.ComponentModel.DataAnnotations;

namespace ZebraSCannerTest1.Models
{
    public class ScanLog
    {
        [Key]
        public int Id { get; set; }
        public string Barcode { get; set; }
        public int ScannedQuantity { get; set; }
        public int InitialQuantity { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
