using ZebraSCannerTest1.Core.Enums;

namespace ZebraSCannerTest1.Core.Interfaces
{
    public interface IJsonExportLogsService
    {
        Task<string> ExportLogsJsonAsync(
            string? filePath = null,
            IProgress<double>? progress = null,
            InventoryMode mode = InventoryMode.Standard);
    }
}
