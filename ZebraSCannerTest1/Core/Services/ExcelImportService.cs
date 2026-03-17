using Microsoft.Data.Sqlite;
using MiniExcelLibs;
using System.Diagnostics;
using ZebraSCannerTest1.Core.Dtos;
using ZebraSCannerTest1.Core.Enums;
using Microsoft.Maui.Storage;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.Core.Interfaces;

namespace ZebraSCannerTest1.Core.Services
{
    public class ExcelImportService
    {
        private readonly IDbFactory _db;

        public ExcelImportService(IDbFactory db)
        {
            _db = db;
        }
        // ✅ MAIN ENTRY (mobile)
        public async Task ImportExcelAsync(Stream stream, InventoryMode mode = InventoryMode.Standard, string? originalFileName = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream), "Excel stream cannot be null.");

            Console.WriteLine($"[DOTNET] Importing Excel... (mode={mode})");

            var tempName = originalFileName ?? $"import_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
            var tempPath = Path.Combine(FileSystem.AppDataDirectory, tempName);

            try
            {
                // ✅ Copy Excel to sandbox (Android-safe)
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

                Console.WriteLine($"[DOTNET] Copied Excel to sandbox: {tempPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Excel copy failed: {ex.Message}");
                throw new IOException($"Failed to copy Excel file into local sandbox: {ex.Message}", ex);
            }

            // 🔹 Process import
            try
            {
                using var localStream = File.OpenRead(tempPath);
                await ImportExcelInternalAsync(localStream, mode);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DOTNET] ❌ Import failed: {ex.Message}");
                throw;
            }
        }

        // ✅ For desktop/debug use
        public async Task ImportExcelAsync(string filePath, InventoryMode mode = InventoryMode.Standard)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                await ImportExcelInternalAsync(stream, mode);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DOTNET] ❌ Import from file failed: {ex.Message}");
                throw;
            }
        }

        // ✅ Core shared logic
        private async Task ImportExcelInternalAsync(Stream stream, InventoryMode mode)
        {
            string table = mode == InventoryMode.Loots ? "LootsProducts" : "Products";
            string logsTable = mode == InventoryMode.Loots ? "LootsScanLogs" : "ScanLogs";
            bool isLoots = mode == InventoryMode.Loots;

            int processed = 0;
            var now = DateTime.UtcNow.ToString("o");

            using var conn = _db.Inventorization(mode);
            DatabaseInitializer.Initialize(conn, mode);

            using var tx = conn.BeginTransaction();

            // Clear data
            using (var clear = conn.CreateCommand())
            {
                clear.Transaction = tx;
                clear.CommandText = $"DELETE FROM {logsTable};";
                clear.ExecuteNonQuery();

                clear.CommandText = $"DELETE FROM {table};";
                clear.ExecuteNonQuery();

                clear.CommandText = $"DELETE FROM sqlite_sequence WHERE name IN ('{table}', '{logsTable}');";
                clear.ExecuteNonQuery();
            }

            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;

            cmd.CommandText = isLoots
                ? $@"INSERT INTO {table} 
                    (Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt, Name, Color, Size, Price, ArticCode)
                    VALUES ($barcode, $box, $initial, 0, $created, $updated, $name, $color, $size, $price, $artic);"
                : $@"INSERT INTO {table} 
                    (Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt, Name, Color, Size, Price, ArticCode)
                    VALUES ($barcode, $initial, 0, $created, $updated, $name, $color, $size, $price, $artic)
                    ON CONFLICT(Barcode) DO UPDATE SET
                        InitialQuantity = $initial,
                        ScannedQuantity = 0,
                        UpdatedAt = $updated,
                        Name = $name,
                        Color = $color,
                        Size = $size,
                        Price = $price,
                        ArticCode = $artic;";

            cmd.Parameters.Add("$barcode", SqliteType.Text);
            cmd.Parameters.Add("$initial", SqliteType.Integer);
            cmd.Parameters.Add("$created", SqliteType.Text);
            cmd.Parameters.Add("$updated", SqliteType.Text);
            cmd.Parameters.Add("$name", SqliteType.Text);
            cmd.Parameters.Add("$color", SqliteType.Text);
            cmd.Parameters.Add("$size", SqliteType.Text);
            cmd.Parameters.Add("$price", SqliteType.Text);
            cmd.Parameters.Add("$artic", SqliteType.Text);
            if (isLoots)
                cmd.Parameters.Add("$box", SqliteType.Text);

            foreach (var row in stream.Query<ExcelProductDto>())
            {
                if (row == null || string.IsNullOrWhiteSpace(row.Barcode))
                    continue;

                cmd.Parameters["$barcode"].Value = row.Barcode.Trim();
                cmd.Parameters["$initial"].Value = row.Quantity;
                cmd.Parameters["$created"].Value = now;
                cmd.Parameters["$updated"].Value = now;
                cmd.Parameters["$name"].Value = row.Name ?? "";
                cmd.Parameters["$color"].Value = row.Color ?? "";
                cmd.Parameters["$size"].Value = row.Size ?? "";
                cmd.Parameters["$price"].Value = row.Price ?? "";
                cmd.Parameters["$artic"].Value = row.ArticCode ?? "";

                if (isLoots)
                    cmd.Parameters["$box"].Value = row.Box_Id ?? "UnknownBox";

                cmd.ExecuteNonQuery();
                processed++;
            }

            tx.Commit();
            Debug.WriteLine($"[IMPORT] Excel import done. Rows: {processed}");
        }

    
    }
}
