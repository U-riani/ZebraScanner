namespace ZebraSCannerTest1.Core.Models;

public class LootBarcodeProgress
{
    public string Barcode { get; set; } = string.Empty;
    public string? BoxId { get; set; }

    public int CurrentBoxScannedQuantity { get; set; }
    public int CurrentBoxExpectedQuantity { get; set; }

    public int BarcodeTotalExpectedQuantity { get; set; }
    public int BarcodeTotalScannedQuantity { get; set; }

    public bool HasBarcodeTotalExpectedQuantity { get; set; }

    public bool UsesBarcodeTotalFallback =>
        CurrentBoxExpectedQuantity <= 0 && HasBarcodeTotalExpectedQuantity && BarcodeTotalExpectedQuantity > 0;

    public int EffectiveExpectedQuantity =>
        UsesBarcodeTotalFallback ? BarcodeTotalExpectedQuantity : CurrentBoxExpectedQuantity;

    public int EffectiveScannedQuantity =>
        UsesBarcodeTotalFallback ? BarcodeTotalScannedQuantity : CurrentBoxScannedQuantity;

    public int RemainingQuantity => EffectiveExpectedQuantity - EffectiveScannedQuantity;

    public string? Name { get; set; }
    public string? Color { get; set; }
    public string? Size { get; set; }
    public string? Price { get; set; }
    public string? ArticCode { get; set; }
}
