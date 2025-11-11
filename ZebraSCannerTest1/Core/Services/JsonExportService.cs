using Microsoft.Data.Sqlite;
using System.Text.Json;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.Helpers;

namespace ZebraSCannerTest1.Core.Services
{
    public class JsonExportService : IJsonExportService
    {
        private readonly SqliteConnection _conn;

        public JsonExportService(SqliteConnection conn)
        {
            _conn = conn;
        }

        /// <summary>
        /// Exports product data as JSON (Standard or Loots mode).
        /// </summary>
        public async Task<string> ExportProductsJsonAsync(
            string? filePath = null,
            IProgress<double>? progress = null,
            InventoryMode mode = InventoryMode.Standard)
        {
            string table = mode == InventoryMode.Loots ? "LootsProducts" : "Products";
            Console.WriteLine($"[DOTNET] Starting JSON export from table: {table}");

            var rows = new List<object>();
            int count = 0;
            int totalCount = 0;

            using var conn = DatabaseInitializer.GetConnection(mode);
            using (var countCmd = conn.CreateCommand())
            {
                countCmd.CommandText = $"SELECT COUNT(*) FROM {table}";
                totalCount = Convert.ToInt32(countCmd.ExecuteScalar() ?? 0);
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = mode == InventoryMode.Loots
                ? @"SELECT Barcode, Box_Id, InitialQuantity, ScannedQuantity, Name, Color, Size, Price, ArticCode, UpdatedAt
                    FROM LootsProducts ORDER BY UpdatedAt DESC;"
                : @"SELECT Barcode, InitialQuantity, ScannedQuantity, Name, Color, Size, Price, ArticCode, UpdatedAt
                    FROM Products ORDER BY UpdatedAt DESC;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (mode == InventoryMode.Loots)
                {
                    rows.Add(new
                    {
                        Barcode = reader.SafeGetString(0),
                        Box_Id = reader.SafeGetString(1),
                        InitialQuantity = reader.SafeGetInt(2),
                        ScannedQuantity = reader.SafeGetInt(3),
                        Name = reader.SafeGetString(4),
                        Color = reader.SafeGetString(5),
                        Size = reader.SafeGetString(6),
                        Price = reader.SafeGetString(7),
                        ArticCode = reader.SafeGetString(8),
                        UpdatedAt = reader.SafeGetDate(9).ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }
                else
                {
                    rows.Add(new
                    {
                        Barcode = reader.SafeGetString(0),
                        InitialQuantity = reader.SafeGetInt(1),
                        ScannedQuantity = reader.SafeGetInt(2),
                        Name = reader.SafeGetString(3),
                        Color = reader.SafeGetString(4),
                        Size = reader.SafeGetString(5),
                        Price = reader.SafeGetString(6),
                        ArticCode = reader.SafeGetString(7),
                        UpdatedAt = reader.SafeGetDate(8).ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }

                count++;
                if (totalCount > 0 && count % 200 == 0)
                    progress?.Report(Math.Min((double)count / totalCount, 1.0));
            }

            progress?.Report(1.0);

            string json = JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });

            if (!string.IsNullOrWhiteSpace(filePath))
            {
                await File.WriteAllTextAsync(filePath, json);
                Console.WriteLine($"[DOTNET] ✅ JSON exported to {filePath}");
            }

            return json;
        }
    }
}
