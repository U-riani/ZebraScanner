using Microsoft.Data.Sqlite;
using MiniExcelLibs;
using ZebraSCannerTest1.Models;

namespace ZebraSCannerTest1.Services
{
    public class ExcelExportService
    {
        private readonly SqliteConnection _conn;

        public ExcelExportService(SqliteConnection conn) => _conn = conn;

        public async Task ExportProductsAsync(string filePath)
        {
            Console.WriteLine($"[DOTNET] Starting export to Excel: {filePath}");

            var rows = new List<object>();
            int count = 0;

            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT Barcode, InitialQuantity, ScannedQuantity, UpdatedAt FROM Products ORDER BY UpdatedAt DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                rows.Add(new
                {
                    Barcode = r.GetString(0),
                    InitialQuantity = r.GetInt32(1),
                    ScannedQuantity = r.GetInt32(2),
                    UpdatedAt = DateTime.Parse(r.GetString(3))
                });

                count++;
                if (count % 1000 == 0)
                    Console.WriteLine($"[DOTNET] Exported {count} rows so far...");
            }

            await MiniExcel.SaveAsAsync(filePath, rows);

            Console.WriteLine($"[DOTNET] ✅ Export finished. Total rows = {count}");
        }
    }
}
