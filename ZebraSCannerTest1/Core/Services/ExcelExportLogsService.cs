using Microsoft.Data.Sqlite;
using MiniExcelLibs;
using System.Diagnostics;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.Data;

namespace ZebraSCannerTest1.Core.Services
{
    public class ExcelExportLogsService : IExcelExportLogsService
    {
        private readonly SqliteConnection _conn;

        public ExcelExportLogsService(SqliteConnection conn)
        {
            _conn = conn;
        }

        public async Task ExportLogsAsync(
            string filePath,
            IProgress<double>? progress = null,
            InventoryMode mode = InventoryMode.Standard)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));

            string table = mode == InventoryMode.Loots ? "LootsScanLogs" : "ScanLogs";
            Console.WriteLine($"[DOTNET] Exporting logs from table: {table} → {filePath}");

            var logs = new List<ScanLog>();

            using (var conn = DatabaseInitializer.GetConnection(mode))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = mode == InventoryMode.Loots
                    ? @"SELECT Barcode, Box_Id, Was, IncrementBy, IsValue, UpdatedAt, IsManual 
                        FROM LootsScanLogs
                        ORDER BY UpdatedAt DESC;"
                    : @"SELECT Barcode, Was, IncrementBy, IsValue, UpdatedAt, IsManual, Section
                        FROM ScanLogs
                        ORDER BY UpdatedAt DESC;";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var log = new ScanLog
                    {
                        Barcode = reader.GetString(0),
                        Was = reader.GetInt32(mode == InventoryMode.Loots ? 2 : 1),
                        IncrementBy = reader.GetInt32(mode == InventoryMode.Loots ? 3 : 2),
                        IsValue = reader.GetInt32(mode == InventoryMode.Loots ? 4 : 3),
                        UpdatedAt = DateTime.Parse(reader.GetString(mode == InventoryMode.Loots ? 5 : 4)),
                        IsManual = reader.IsDBNull(mode == InventoryMode.Loots ? 6 : 5)
                            ? null
                            : reader.GetInt32(mode == InventoryMode.Loots ? 6 : 5),
                        Section = mode == InventoryMode.Loots
                            ? (reader.IsDBNull(1) ? null : reader.GetString(1)) // Box_Id
                            : (reader.IsDBNull(6) ? null : reader.GetString(6))
                    };
                    logs.Add(log);
                }
            }

            if (logs.Count == 0)
                throw new InvalidOperationException("No logs found to export.");

            var exportList = logs.Select(l => new
            {
                l.Barcode,
                l.Was,
                l.IncrementBy,
                l.IsValue,
                UpdatedAt = l.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                IsManual = l.IsManual?.ToString() ?? "",
                Box_Id = mode == InventoryMode.Loots ? l.Section : null,
                Section = mode == InventoryMode.Standard ? l.Section : null
            }).ToList();

            await Task.Run(() => MiniExcel.SaveAs(filePath, exportList));
            progress?.Report(1.0);

            Console.WriteLine($"[DOTNET] ✅ Exported {logs.Count} rows from {table}");
        }
    }
}
