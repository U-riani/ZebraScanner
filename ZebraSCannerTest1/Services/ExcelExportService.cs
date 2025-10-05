using Microsoft.Data.Sqlite;
using MiniExcelLibs;

namespace ZebraSCannerTest1.Services
{
    public class ExcelExportService
    {
        private readonly SqliteConnection _conn;

        public ExcelExportService(SqliteConnection conn) => _conn = conn;

        public async Task ExportProductsAsync(string filePath, IProgress<double>? progress = null)
        {
            Console.WriteLine($"[DOTNET] Starting export to Excel: {filePath}");

            var rows = new List<object>();
            int count = 0;
            int totalCount = 0;

            // Get total for progress
            using (var countCmd = _conn.CreateCommand())
            {
                countCmd.CommandText = "SELECT COUNT(*) FROM Products";
                totalCount = Convert.ToInt32(countCmd.ExecuteScalar());
            }

            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT Barcode, InitialQuantity, ScannedQuantity, Name, Color, Size, Price, ArticCode, UpdatedAt FROM Products ORDER BY UpdatedAt DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                rows.Add(new
                {
                    Barcode = r.GetString(0),
                    InitialQuantity = r.GetInt32(1),
                    ScannedQuantity = r.GetInt32(2),
                    Name = r.GetString(3),
                    Color = r.GetString(4),
                    Size = r.GetString(5),
                    Price = r.GetString(6),
                    ArticCode= r.GetString(7),
                    UpdatedAt = DateTime.Parse(r.GetString(8))
                });

                count++;
                if (count % 200 == 0 && totalCount > 0)
                {
                    double progressValue = (double)count / totalCount;
                    progress?.Report(progressValue);
                }
            }

            await MiniExcel.SaveAsAsync(filePath, rows);

            progress?.Report(1.0); // 100%
            Console.WriteLine($"[DOTNET] ✅ Export finished. Total rows = {count}");
        }
    }
}
