using Microsoft.Data.Sqlite;
using MiniExcelLibs;
using System.Diagnostics;
using ZebraSCannerTest1.Dtos;


namespace ZebraSCannerTest1.Services
{
    public class ExcelImportService
    {
        private readonly SqliteConnection _conn;

        public ExcelImportService(SqliteConnection conn) => _conn = conn;

        // ✅ MAIN ENTRY for mobile (stream from FilePicker)
        public async Task ImportExcelAsync(Stream stream, string? originalFileName = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream), "Excel stream cannot be null.");

            Console.WriteLine("[DOTNET] Importing Excel...");

            // 🔹 Copy into app sandbox to avoid Android permission issues
            var tempName = originalFileName ?? $"import_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
            var tempPath = Path.Combine(FileSystem.AppDataDirectory, tempName);

            try
            {
                // ✅ Detach from SAF and write to local sandbox
                byte[] buffer;
                using (var ms = new MemoryStream())
                {
                    await stream.CopyToAsync(ms);
                    buffer = ms.ToArray();
                }

                await stream.DisposeAsync();

                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                await File.WriteAllBytesAsync(tempPath, buffer);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to copy Excel file into local sandbox: {ex.Message}", ex);
            }

            // 🔹 Re-open as normal FileStream (seekable)
            using var localStream = File.OpenRead(tempPath);
            await ImportExcelInternalAsync(localStream);
        }

        // ✅ For debug / desktop use
        public async Task ImportExcelAsync(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            await ImportExcelInternalAsync(stream);
        }

        // ✅ Core shared logic
        private async Task ImportExcelInternalAsync(Stream stream)
        {
            int processed = 0;
            var now = DateTime.UtcNow.ToString("o");

            using var tx = _conn.BeginTransaction();

            // 🔹 Clear old data safely
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

            // 🔹 Prepare upsert command
            using var upsert = _conn.CreateCommand();
            upsert.Transaction = tx;
            upsert.CommandText = @"
                INSERT INTO Products 
                    (Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt, Name, Color, Size, Price, ArticCode)
                VALUES 
                    ($barcode, $initial, 0, $created, $updated, $name, $color, $size, $price, $artic)
                ON CONFLICT(Barcode) DO UPDATE SET
                    InitialQuantity = $initial,
                    ScannedQuantity = 0,
                    UpdatedAt = $updated,
                    Name = $name,
                    Color = $color,
                    Size = $size,
                    Price = $price,
                    ArticCode = $artic;";

            upsert.Parameters.Add("$barcode", SqliteType.Text);
            upsert.Parameters.Add("$initial", SqliteType.Integer);
            upsert.Parameters.Add("$created", SqliteType.Text);
            upsert.Parameters.Add("$updated", SqliteType.Text);
            upsert.Parameters.Add("$name", SqliteType.Text);
            upsert.Parameters.Add("$color", SqliteType.Text);
            upsert.Parameters.Add("$size", SqliteType.Text);
            upsert.Parameters.Add("$price", SqliteType.Text);
            upsert.Parameters.Add("$artic", SqliteType.Text);

            // 🔹 Read and insert Excel rows
            foreach (var r in MiniExcel.Query<ExcelProductDto>(stream))
            {
                if (r == null || string.IsNullOrWhiteSpace(r.Barcode))
                    continue;

#if DEBUG
                Console.WriteLine($"[ROW] {r.Id} | {r.Barcode} | {r.Quantity} | {r.Name}");
#endif

                upsert.Parameters["$barcode"].Value = r.Barcode.Trim();
                upsert.Parameters["$initial"].Value = r.Quantity;
                upsert.Parameters["$created"].Value = now;
                upsert.Parameters["$updated"].Value = now;
                upsert.Parameters["$name"].Value = r.Name?.Trim() ?? "";
                upsert.Parameters["$color"].Value = r.Color?.Trim() ?? "";
                upsert.Parameters["$size"].Value = r.Size?.Trim() ?? "";
                upsert.Parameters["$price"].Value = r.Price?.ToString() ?? "";
                upsert.Parameters["$artic"].Value = r.ArticCode?.Trim() ?? "";

                upsert.ExecuteNonQuery();
                processed++;
            }

            tx.Commit();
            Debug.WriteLine($"[DOTNET] ✅ Excel import complete. Rows = {processed}");

        }
    }
}
