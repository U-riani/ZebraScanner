using Microsoft.Data.Sqlite;
using MiniExcelLibs;
using System.Diagnostics;
using ZebraSCannerTest1.Models;

namespace ZebraSCannerTest1.Services
{
    public class ExcelExportLogsService
    {
        private readonly SqliteConnection _conn;

        public ExcelExportLogsService(SqliteConnection conn)
        {
            _conn = conn;
        }

        public async Task ExportLogsAsync(string filePath, IProgress<double>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var logs = new List<ScanLog>();
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT 
                        Barcode, Was, IncrementBy, IsValue, UpdatedAt, IsManual, Section
                    FROM ScanLogs
                    ORDER BY UpdatedAt DESC;";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    logs.Add(new ScanLog
                    {
                        Barcode = reader.GetString(0),
                        Was = reader.GetInt32(1),
                        IncrementBy = reader.GetInt32(2),
                        IsValue = reader.GetInt32(3),
                        UpdatedAt = DateTime.Parse(reader.GetString(4)),
                        IsManual = !reader.IsDBNull(5) ? reader.GetInt32(5) : (int?)null,
                        Section = !reader.IsDBNull(6) ? reader.GetString(6) : null
                    });
                }
            }

            if (logs.Count == 0)
                throw new InvalidOperationException("No logs found to export.");

            // Build export DTOs (MiniExcel works best with plain anonymous or DTO objects)
            var exportList = logs.Select(l => new
            {
                l.Barcode,
                l.Was,
                l.IncrementBy,
                l.IsValue,
                UpdatedAt = l.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                IsManual = l.IsManual.HasValue ? l.IsManual.ToString() : "",
                l.Section
            }).ToList();

            Debug.WriteLine($"[DOTNET] Exporting {exportList.Count} logs to {filePath}");

            await Task.Run(() =>
            {
                MiniExcel.SaveAs(filePath, exportList);
            });

            progress?.Report(1.0);
        }
    }
}
