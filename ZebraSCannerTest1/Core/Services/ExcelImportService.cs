using Microsoft.Data.Sqlite;
using MiniExcelLibs;
using System.Diagnostics;
using ZebraSCannerTest1.Core.Dtos;
using ZebraSCannerTest1.Core.Enums;
using Microsoft.Maui.Storage;
using ZebraSCannerTest1.Data;

namespace ZebraSCannerTest1.Core.Services
{
    public class ExcelImportService
    {
        private readonly SqliteConnection _conn;
        public ExcelImportService(SqliteConnection conn) => _conn = conn;

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

            Console.WriteLine($"[DOTNET] ⏳ Importing into table: {table}");

            try
            {
                // 🔧 Ensure correct DB and write mode
                string dbFile = mode == InventoryMode.Loots
                    ? "zebraScanner_loots.db"
                    : "zebraScanner_standard.db";

                string dbPath = Path.Combine(FileSystem.AppDataDirectory, dbFile);

                // 🩹 Make sure file is writable
                if (File.Exists(dbPath))
                {
                    var attr = File.GetAttributes(dbPath);
                    if (attr.HasFlag(FileAttributes.ReadOnly))
                        File.SetAttributes(dbPath, attr & ~FileAttributes.ReadOnly);
                }

                // 🧹 Close and reopen connection cleanly
                try { _conn.Close(); } catch { }
                _conn.ConnectionString = $"Data Source={dbPath};Mode=ReadWriteCreate";
                _conn.Open();

                Console.WriteLine($"[DB SWITCH] → {_conn.DataSource}");
                DatabaseInitializer.Initialize(_conn, mode);

                using var tx = _conn.BeginTransaction();

                // 🔹 Step 1: Clear existing data (only target mode)
                using (var clear = _conn.CreateCommand())
                {
                    clear.Transaction = tx;

                    // 1️⃣ Disable FK checks
                    clear.CommandText = "PRAGMA foreign_keys = OFF;";
                    clear.ExecuteNonQuery();

                    // 2️⃣ Delete from logs
                    clear.CommandText = $"DELETE FROM {logsTable};";
                    clear.ExecuteNonQuery();

                    // 3️⃣ Delete from main table
                    clear.CommandText = $"DELETE FROM {table};";
                    clear.ExecuteNonQuery();

                    // 4️⃣ Reset auto-increment sequences
                    clear.CommandText = $"DELETE FROM sqlite_sequence WHERE name IN ('{table}', '{logsTable}');";
                    clear.ExecuteNonQuery();

                    // 5️⃣ Re-enable FK checks
                    clear.CommandText = "PRAGMA foreign_keys = ON;";
                    clear.ExecuteNonQuery();
                }


                // 🔹 Step 2: Prepare upsert
                using var upsert = _conn.CreateCommand();
                upsert.Transaction = tx;
                upsert.CommandText = isLoots
                    ? $@"
                        INSERT INTO {table} 
                            (Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt, Name, Color, Size, Price, ArticCode)
                        VALUES 
                            ($barcode, $box, $initial, 0, $created, $updated, $name, $color, $size, $price, $artic);"
                    : $@"
                        INSERT INTO {table} 
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

                // 🔹 Step 3: Bind parameters
                upsert.Parameters.Add("$barcode", SqliteType.Text);
                upsert.Parameters.Add("$box", SqliteType.Text);
                upsert.Parameters.Add("$initial", SqliteType.Integer);
                upsert.Parameters.Add("$created", SqliteType.Text);
                upsert.Parameters.Add("$updated", SqliteType.Text);
                upsert.Parameters.Add("$name", SqliteType.Text);
                upsert.Parameters.Add("$color", SqliteType.Text);
                upsert.Parameters.Add("$size", SqliteType.Text);
                upsert.Parameters.Add("$price", SqliteType.Text);
                upsert.Parameters.Add("$artic", SqliteType.Text);

                // 🔹 Step 4: Read Excel rows
                foreach (var r in stream.Query<ExcelProductDto>())
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
                    upsert.Parameters["$price"].Value = r.Price?.Trim() ?? "";
                    upsert.Parameters["$artic"].Value = r.ArticCode?.Trim() ?? "";

                    if (isLoots)
                        upsert.Parameters["$box"].Value = r.Box_Id?.Trim() ?? "Unknown_Box";

                    upsert.ExecuteNonQuery();
                    processed++;
                }

                tx.Commit();
                Debug.WriteLine($"[DOTNET] ✅ Excel import complete ({mode}). Rows = {processed}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Excel import failed: {ex.Message}");
                throw new InvalidOperationException($"Failed during Excel import ({mode}).", ex);
            }
        }
    }
}
