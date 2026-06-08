using ZebraSCannerTest1.Core.Enums;

namespace ZebraSCannerTest1.Core.Interfaces
{
    /// <summary>
    /// Handles exporting product data to Excel.
    /// </summary>
    public interface IExcelExportService
    {
        /// <summary>
        /// Exports product records from the database into an Excel file.
        /// </summary>
        /// <param name="filePath">Destination file path for the export.</param>
        /// <param name="progress">Optional progress reporter (0–1.0).</param>
        /// <param name="mode">Inventory mode — Standard or Loots (defaults to Standard).</param>
        Task ExportProductsAsync(string filePath, IProgress<double>? progress = null, InventoryMode mode = InventoryMode.Standard);
    }
}
