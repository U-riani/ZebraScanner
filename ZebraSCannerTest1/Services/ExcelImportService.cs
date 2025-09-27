using Microsoft.Data.Sqlite;
using MiniExcelLibs;
using ZebraSCannerTest1.Dtos;

namespace ZebraSCannerTest1.Services
{
    public class ExcelImportService
    {
        private readonly SqliteConnection _conn;

        public ExcelImportService(SqliteConnection conn) => _conn = conn;

        public async Task ImportExcelAsync(string filePath)
        {
            Console.WriteLine($"[DOTNET] Importing from Excel: {filePath}");
            var rows = MiniExcel.Query<ExcelProductDto>(filePath).ToList();
            Console.WriteLine($"[DOTNET] Read {rows.Count} rows from Excel");

            using var tx = _conn.BeginTransaction();

            using var upsert = _conn.CreateCommand();
            upsert.Transaction = tx;
            upsert.CommandText = @"
INSERT INTO Products (Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt)
VALUES ($barcode, $initial, COALESCE((SELECT ScannedQuantity FROM Products WHERE Barcode=$barcode),0), $created, $updated)
ON CONFLICT(Barcode) DO UPDATE SET
    InitialQuantity = $initial,
    UpdatedAt       = $updated;";
            upsert.Parameters.Add("$barcode", SqliteType.Text);
            upsert.Parameters.Add("$initial", SqliteType.Integer);
            upsert.Parameters.Add("$created", SqliteType.Text);
            upsert.Parameters.Add("$updated", SqliteType.Text);

            int processed = 0;
            foreach (var r in rows)
            {
                if (string.IsNullOrWhiteSpace(r.Barcode)) continue;

                upsert.Parameters["$barcode"].Value = r.Barcode.Trim();
                upsert.Parameters["$initial"].Value = r.Quantity;
                var now = DateTime.UtcNow.ToString("o");
                upsert.Parameters["$created"].Value = now;
                upsert.Parameters["$updated"].Value = now;
                upsert.ExecuteNonQuery();

                if (++processed % 1000 == 0)
                    Console.WriteLine($"[DOTNET] Imported {processed}/{rows.Count}...");
            }

            tx.Commit();
            Console.WriteLine($"[DOTNET] ✅ Import finished. Total rows = {processed}");
        }
    }
}