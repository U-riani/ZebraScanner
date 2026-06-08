namespace ZebraSCannerTest1.Core.Models;

public class LootBoxSummary
{
    public string Box_Id { get; set; } = string.Empty;
    public int InitialQuantity { get; set; }
    public int ScannedQuantity { get; set; }

    public string TotalInfo => $"{ScannedQuantity} / {InitialQuantity}";
}
