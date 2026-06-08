namespace ZebraSCannerTest1.Core.Models;

public class CacheSnapshot
{
    public List<ScannedProduct> Scanned { get; set; } = new();
    public List<ScanLog> Logs { get; set; } = new();
}
