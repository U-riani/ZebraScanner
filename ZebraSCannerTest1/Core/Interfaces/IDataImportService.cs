using ZebraSCannerTest1.Core.Enums;

namespace ZebraSCannerTest1.Core.Interfaces;

public interface IDataImportService
{
    Task ImportExcelAsync(Stream stream, InventoryMode mode = InventoryMode.Standard, string? fileName = null);
    Task ImportDbAsync(Stream dbStream, InventoryMode mode = InventoryMode.Standard);
    Task<int> ImportJsonAsync(Stream jsonStream, InventoryMode mode = InventoryMode.Standard);
}

