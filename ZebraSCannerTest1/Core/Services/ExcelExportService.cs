using Microsoft.Data.Sqlite;
using MiniExcelLibs;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;

namespace ZebraSCannerTest1.Core.Services
{
    public class ExcelExportService : IExcelExportService
    {
        private readonly SqliteConnection _conn;

        public ExcelExportService(SqliteConnection conn) => _conn = conn;

        /// <summary>
        /// Exports product data (Standard or Loots mode) to an Excel file.
        /// </summary>
        public async Task ExportProductsAsync(
            string filePath,
            IProgress<double>? progress = null,
            InventoryMode mode = InventoryMode.Standard)
        {
            string table = mode == InventoryMode.Loots ? "LootsProducts" : "Products";
            Console.WriteLine($"[DOTNET] Starting Excel export from table: {table} → {filePath}");

            var rows = new List<object>();
            int count = 0;
            int totalCount = 0;

            // 🧮 Get total for progress
            using (var countCmd = _conn.CreateCommand())
            {
                countCmd.CommandText = $"SELECT COUNT(*) FROM {table}";
                totalCount = Convert.ToInt32(countCmd.ExecuteScalar());
            }

            using var cmd = _conn.CreateCommand();

            cmd.CommandText = mode == InventoryMode.Loots
                ? $@"
                    SELECT 
                        Barcode, 
                        Box_Id, 
                        InitialQuantity, 
                        ScannedQuantity, 
                        Name, 
                        Color, 
                        Size, 
                        Price, 
                        ArticCode, 
                        UpdatedAt 
                    FROM {table} 
                    ORDER BY UpdatedAt DESC;"
                : $@"
                    SELECT 
                        Barcode, 
                        InitialQuantity, 
                        ScannedQuantity, 
                        Name, 
                        Color, 
                        Size, 
                        Price, 
                        ArticCode, 
                        UpdatedAt 
                    FROM {table} 
                    ORDER BY UpdatedAt DESC;";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                if (mode == InventoryMode.Loots)
                {
                    rows.Add(new
                    {
                        Barcode = reader.GetString(0),
                        Box_Id = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        InitialQuantity = reader.GetInt32(2),
                        ScannedQuantity = reader.GetInt32(3),
                        Name = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        Color = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        Size = reader.IsDBNull(6) ? "" : reader.GetString(6),
                        Price = reader.IsDBNull(7) ? "" : reader.GetString(7),
                        ArticCode = reader.IsDBNull(8) ? "" : reader.GetString(8),
                        UpdatedAt = reader.IsDBNull(9) ? DateTime.MinValue : DateTime.Parse(reader.GetString(9))
                    });
                }
                else
                {
                    rows.Add(new
                    {
                        Barcode = reader.GetString(0),
                        InitialQuantity = reader.GetInt32(1),
                        ScannedQuantity = reader.GetInt32(2),
                        Name = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        Color = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        Size = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        Price = reader.IsDBNull(6) ? "" : reader.GetString(6),
                        ArticCode = reader.IsDBNull(7) ? "" : reader.GetString(7),
                        UpdatedAt = reader.IsDBNull(8) ? DateTime.MinValue : DateTime.Parse(reader.GetString(8))
                    });
                }

                count++;
                if (count % 200 == 0 && totalCount > 0)
                {
                    double progressValue = (double)count / totalCount;
                    progress?.Report(progressValue);
                }
            }

            await MiniExcel.SaveAsAsync(filePath, rows);

            progress?.Report(1.0); // ✅ Complete
            Console.WriteLine($"[DOTNET] ✅ Excel export finished. Rows={count}, Mode={mode}");
        }
    }
}
