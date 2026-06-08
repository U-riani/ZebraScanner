using ZebraSCannerTest1.Core.Enums;

namespace ZebraSCannerTest1.Core.Interfaces
{
    public interface IJsonExportService
    {
        Task<string> ExportProductsJsonAsync(
            string? filePath = null,
            IProgress<double>? progress = null,
            InventoryMode mode = InventoryMode.Standard);
    }
}
