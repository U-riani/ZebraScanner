using Microsoft.Data.Sqlite;
using System.Text.Json;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.Helpers;

namespace ZebraSCannerTest1.Core.Services
{
    public class JsonExportLogsService : IJsonExportLogsService
    {
        private readonly SqliteConnection _conn;

        public JsonExportLogsService(SqliteConnection conn)
        {
            _conn = conn;
        }

        public async Task<string> ExportLogsJsonAsync(
            string? filePath = null,
            IProgress<double>? progress = null,
            InventoryMode mode = InventoryMode.Standard)
        {
            string table = mode == InventoryMode.Loots ? "LootsScanLogs" : "ScanLogs";
            Console.WriteLine($"[DOTNET] Starting JSON log export from table: {table}");

            var logs = new List<object>();

            using var conn = DatabaseInitializer.GetConnection(mode);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = mode == InventoryMode.Loots
                ? @"SELECT Barcode, Box_Id, Was, IncrementBy, IsValue, UpdatedAt, IsManual 
                    FROM LootsScanLogs ORDER BY UpdatedAt DESC;"
                : @"SELECT Barcode, Was, IncrementBy, IsValue, UpdatedAt, IsManual, Section
                    FROM ScanLogs ORDER BY UpdatedAt DESC;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (mode == InventoryMode.Loots)
                {
                    logs.Add(new
                    {
                        Barcode = reader.SafeGetString(0),
                        Box_Id = reader.SafeGetString(1),
                        Was = reader.SafeGetInt(2),
                        IncrementBy = reader.SafeGetInt(3),
                        IsValue = reader.SafeGetInt(4),
                        UpdatedAt = reader.SafeGetDate(5).ToString("yyyy-MM-dd HH:mm:ss"),
                        IsManual = reader.SafeGetInt(6)
                    });
                }
                else
                {
                    logs.Add(new
                    {
                        Barcode = reader.SafeGetString(0),
                        Was = reader.SafeGetInt(1),
                        IncrementBy = reader.SafeGetInt(2),
                        IsValue = reader.SafeGetInt(3),
                        UpdatedAt = reader.SafeGetDate(4).ToString("yyyy-MM-dd HH:mm:ss"),
                        IsManual = reader.SafeGetInt(5),
                        Section = reader.SafeGetString(6)
                    });
                }
            }

            string json = JsonSerializer.Serialize(logs, new JsonSerializerOptions { WriteIndented = true });

            if (!string.IsNullOrWhiteSpace(filePath))
            {
                await File.WriteAllTextAsync(filePath, json);
                Console.WriteLine($"[DOTNET] ✅ JSON logs exported to {filePath}");
            }

            progress?.Report(1.0);
            return json;
        }
    }
}
