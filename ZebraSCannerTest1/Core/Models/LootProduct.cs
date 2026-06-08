namespace ZebraSCannerTest1.Core.Models;

public class LootProduct
{
    public int Id { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string? Box_Id { get; set; }
    public int InitialQuantity { get; set; }
    public int ScannedQuantity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string? Name { get; set; }
    public string? Color { get; set; }
    public string? Size { get; set; }
    public string? Price { get; set; }
    public string? ArticCode { get; set; }
}
