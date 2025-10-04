using Microsoft.Data.Sqlite;
using MiniExcelLibs;
using ZebraSCannerTest1.Dtos;

namespace ZebraSCannerTest1.Services
{
    public class ExcelImportService
    {
        private readonly SqliteConnection _conn;

        public ExcelImportService(SqliteConnection conn) => _conn = conn;

        // For FilePath (desktop/debugging use)
        public async Task ImportExcelAsync(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            await ImportExcelAsync(stream);
        }

        // For Stream (mobile use)
        public async Task ImportExcelAsync(Stream stream)
        {
            Console.WriteLine($"[DOTNET] Importing from Excel stream...");

            int processed = 0;
            int batchSize = 5000; // ✅ adjust based on device performance

            using var tx = _conn.BeginTransaction();

            // Clear old data
            using (var clear = _conn.CreateCommand())
            {
                clear.Transaction = tx;
                clear.CommandText = @"
                    PRAGMA foreign_keys = OFF;
                    DELETE FROM ScanLogs;
                    DELETE FROM ScannedProducts;
                    DELETE FROM Products;
                    DELETE FROM sqlite_sequence WHERE name IN ('Products','ScanLogs','ScannedProducts');
                    PRAGMA foreign_keys = ON;";
                clear.ExecuteNonQuery();
            }

            // Upsert prepared statement
            using var upsert = _conn.CreateCommand();
            upsert.Transaction = tx;
            upsert.CommandText = @"
                INSERT INTO Products (Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt, Name, Color, Size, Price, ArticCode)
                VALUES ($barcode, $initial, 0, $created, $updated, $name, $color, $size, $price, $articCode)
                ON CONFLICT(Barcode) DO UPDATE SET
                    InitialQuantity = $initial,
                    ScannedQuantity = 0,
                    UpdatedAt = $updated,
                    Name = $name,
                    Color = $color,
                    Size = $size,
                    Price = $price,
                    ArticCode = $articCode;";

            upsert.Parameters.Add("$barcode", SqliteType.Text);
            upsert.Parameters.Add("$initial", SqliteType.Integer);
            upsert.Parameters.Add("$created", SqliteType.Text);
            upsert.Parameters.Add("$updated", SqliteType.Text);
            upsert.Parameters.Add("$name", SqliteType.Text);
            upsert.Parameters.Add("$color", SqliteType.Text);
            upsert.Parameters.Add("$size", SqliteType.Text);
            upsert.Parameters.Add("$price", SqliteType.Text);
            upsert.Parameters.Add("$articCode", SqliteType.Text);

            var now = DateTime.UtcNow.ToString("o");

            foreach (var r in MiniExcel.Query<ExcelProductDto>(stream))
            {
                if (string.IsNullOrWhiteSpace(r.Barcode)) continue;

                upsert.Parameters["$barcode"].Value = r.Barcode.Trim();
                upsert.Parameters["$initial"].Value = r.Quantity;
                upsert.Parameters["$created"].Value = now;
                upsert.Parameters["$updated"].Value = now;
                upsert.Parameters["$name"].Value = r.Name?.Trim() ?? "";
                upsert.Parameters["$color"].Value = r.Color?.Trim() ?? "";
                upsert.Parameters["$size"].Value = r.Size?.Trim() ?? "";
                upsert.Parameters["$price"].Value = r.Price?.ToString() ?? "";
                upsert.Parameters["$articCode"].Value = r.ArticCode?.Trim() ?? "";

                upsert.ExecuteNonQuery();
                processed++;

                // Periodic batch commit
                if (processed % batchSize == 0)
                {
                    tx.Commit();
                    Console.WriteLine($"[DOTNET] Imported {processed} rows (batch commit).");

                    var newTx = _conn.BeginTransaction();
                    upsert.Transaction = newTx;
                }
            }

            // Final commit
            tx.Commit();
            Console.WriteLine($"[DOTNET] ✅ Import finished. Total rows = {processed}");
        }
    }
}
