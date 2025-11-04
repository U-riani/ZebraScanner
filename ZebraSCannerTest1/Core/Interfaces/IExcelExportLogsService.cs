using ZebraSCannerTest1.Core.Enums;

namespace ZebraSCannerTest1.Core.Interfaces;

/// <summary>
/// Handles exporting scan logs to Excel.
/// </summary>
public interface IExcelExportLogsService
{
    /// <summary>
    /// Exports scan logs into an Excel file.
    /// </summary>
    /// <param name="filePath">Destination file path for the export.</param>
    /// <param name="progress">Optional progress reporter (0–1.0).</param>
    Task ExportLogsAsync(string filePath, IProgress<double>? progress = null, InventoryMode mode = InventoryMode.Standard);
}
