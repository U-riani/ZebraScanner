using Microsoft.Data.Sqlite;
using MiniExcelLibs;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Data;

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
            using var conn = DatabaseInitializer.GetConnection(mode);
            using (var countCmd = conn.CreateCommand())
            {
                countCmd.CommandText = $"SELECT COUNT(*) FROM {table}";
                var scalar = countCmd.ExecuteScalar();
                long totalCountLong = scalar == DBNull.Value || scalar == null ? 0 : Convert.ToInt64(scalar);
                totalCount = totalCountLong > int.MaxValue ? int.MaxValue : (int)totalCountLong;
            }

            using var cmd = conn.CreateCommand();


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
            try
            {
                using var reader = cmd.ExecuteReader();

                Console.WriteLine($"------------[DOTNET] Exporting {table}. Expecting columns: {reader.FieldCount}");

                while (reader.Read())
                {
                    if (mode == InventoryMode.Loots)
                    {
                        rows.Add(new
                        {
                            Barcode = reader.IsDBNull(0) ? "" : reader.GetString(0),
                            Box_Id = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            InitialQuantity = reader.IsDBNull(2) ? 0 : SafeToInt(reader.GetValue(2)),
                            ScannedQuantity = reader.IsDBNull(3) ? 0 : SafeToInt(reader.GetValue(3)),
                            Name = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            Color = reader.IsDBNull(5) ? "" : reader.GetString(5),
                            Size = reader.IsDBNull(6) ? "" : reader.GetString(6),
                            Price = reader.IsDBNull(7) ? "" : reader.GetString(7),
                            ArticCode = reader.IsDBNull(8) ? "" : reader.GetString(8),
                            UpdatedAt = SafeToDate(reader.GetValue(9))
                        });
                    }
                    else
                    {
                        rows.Add(new
                        {
                            Barcode = reader.IsDBNull(0) ? "" : reader.GetString(0),
                            InitialQuantity = reader.IsDBNull(1) ? 0 : SafeToInt(reader.GetValue(1)),
                            ScannedQuantity = reader.IsDBNull(2) ? 0 : SafeToInt(reader.GetValue(2)),
                            Name = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            Color = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            Size = reader.IsDBNull(5) ? "" : reader.GetString(5),
                            Price = reader.IsDBNull(6) ? "" : reader.GetString(6),
                            ArticCode = reader.IsDBNull(7) ? "" : reader.GetString(7),
                            UpdatedAt = SafeToDate(reader.GetValue(8))
                        });
                    }

                    count++;
                    if (count % 200 == 0)
                    {
                        Console.WriteLine($"[EXPORT DEBUG] Count={count}, Total={totalCount}");
                        if (totalCount > 0)
                            progress?.Report(Math.Min((double)count / totalCount, 1.0));
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DOTNET] Export error → {ex.Message}");
                throw;
            }

            await Task.Run(() => MiniExcel.SaveAs(filePath, rows));
            progress?.Report(1.0);



            Console.WriteLine($"[DOTNET] ✅ Excel export finished. Rows={count}, Mode={mode}");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    // make sure UI progress spinner actually closes
                    var popup = ZebraSCannerTest1.MauiProgram.ServiceProvider.GetService<ZebraSCannerTest1.UI.Services.PopupService>();
                    popup?.UpdateMessage("Finishing up...");
                    popup?.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DOTNET] Popup close error → {ex.Message}");
                }
            });

        }

        private static int SafeToInt(object value)
        {
            try
            {
                if (value == null || value == DBNull.Value)
                    return 0;

                long val = Convert.ToInt64(value);
                if (val > int.MaxValue) return int.MaxValue;
                if (val < int.MinValue) return int.MinValue;
                return (int)val;
            }
            catch
            {
                return 0;
            }
        }

        private static DateTime SafeToDate(object value)
        {
            if (value == null || value == DBNull.Value)
                return DateTime.MinValue;

            var s = value.ToString()?.Trim();
            if (string.IsNullOrEmpty(s))
                return DateTime.MinValue;

            if (DateTime.TryParse(s, out var dt))
                return dt;

            string[] formats = { "o", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss" };
            foreach (var f in formats)
                if (DateTime.TryParseExact(s, f, null, System.Globalization.DateTimeStyles.None, out dt))
                    return dt;

            return DateTime.MinValue;
        }

    }
}
