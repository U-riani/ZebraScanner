using System.ComponentModel.DataAnnotations;


namespace ZebraSCannerTest1.Core.Models;

public class ScanLog
{
    public int Id { get; set; }
    public string Barcode { get; set; }
    public int Was { get; set; }
    public int IncrementBy { get; set; }
    public int IsValue { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? IsManual { get; set; } // null = scanned, 1 = manual
    public string? Section { get; set; } // used for Standard mode
    public string? Box_Id { get; set; }  // used for Loots mode
}




