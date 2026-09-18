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
                            (Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt, Name, Color, Size, Price, ArticCode, Hall, BaseDspa)
                        VALUES 
                            ($barcode, $box, $initial, 0, $created, $updated, $name, $color, $size, $price, $artic, $hall, $baseDspa);"
                    : $@"
                        INSERT INTO {table} 
                            (Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt, Name, Color, Size, Price, ArticCode, Hall, BaseDspa)
                        VALUES 
                            ($barcode, $initial, 0, $created, $updated, $name, $color, $size, $price, $artic, $hall, $baseDspa)
                        ON CONFLICT(Barcode) DO UPDATE SET
                            InitialQuantity = $initial,
                            ScannedQuantity = 0,
                            UpdatedAt = $updated,
                            Name = $name,
                            Color = $color,
                            Size = $size,
                            Price = $price,
                            ArticCode = $artic,
                            Hall = $hall,
                            BaseDspa = $baseDspa;";

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
                upsert.Parameters.Add("$hall", SqliteType.Integer);
                upsert.Parameters.Add("$baseDspa", SqliteType.Text);

                // 🔹 Step 4: Read Excel rows by header name.
                // Dynamic mapping is deliberate here: Hall and BaseDspa are optional,
                // so legacy V18 Excel files without those headers continue to import.
                foreach (IDictionary<string, object> row in stream.Query(useHeaderRow: true))
                {
                    var barcode = GetText(row, "Barcode");
                    if (string.IsNullOrWhiteSpace(barcode))
                        continue;

                    var quantity = GetInt(row, "Quantity");
                    var hall = GetNullableBool(row, "Hall");
                    var baseDspa = GetText(row, "BaseDspa");

#if DEBUG
                    Console.WriteLine($"[ROW] {GetInt(row, "Id")} | {barcode} | {quantity} | {GetText(row, "Name")}");
#endif

                    upsert.Parameters["$barcode"].Value = barcode.Trim();
                    upsert.Parameters["$initial"].Value = quantity;
                    upsert.Parameters["$created"].Value = now;
                    upsert.Parameters["$updated"].Value = now;
                    upsert.Parameters["$name"].Value = GetText(row, "Name")?.Trim() ?? "";
                    upsert.Parameters["$color"].Value = GetText(row, "Color")?.Trim() ?? "";
                    upsert.Parameters["$size"].Value = GetText(row, "Size")?.Trim() ?? "";
                    upsert.Parameters["$price"].Value = GetText(row, "Price")?.Trim() ?? "";
                    upsert.Parameters["$artic"].Value = GetText(row, "ArticCode")?.Trim() ?? "";
                    upsert.Parameters["$hall"].Value = hall.HasValue
                        ? (object)(hall.Value ? 1 : 0)
                        : DBNull.Value;
                    upsert.Parameters["$baseDspa"].Value = string.IsNullOrWhiteSpace(baseDspa)
                        ? DBNull.Value
                        : baseDspa.Trim();

                    if (isLoots)
                    {
                        var boxId = GetText(row, "Box_Id");
                        upsert.Parameters["$box"].Value =
                            string.IsNullOrWhiteSpace(boxId) ? "Unknown_Box" : boxId.Trim();
                    }

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

        private static object? GetValue(IDictionary<string, object> row, string columnName)
        {
            foreach (var item in row)
            {
                if (string.Equals(item.Key?.Trim(), columnName, StringComparison.OrdinalIgnoreCase))
                    return item.Value;
            }

            return null;
        }

        private static string? GetText(IDictionary<string, object> row, string columnName)
        {
            var value = GetValue(row, columnName);
            if (value == null || value == DBNull.Value)
                return null;

            return Convert.ToString(value)?.Trim();
        }

        private static int GetInt(IDictionary<string, object> row, string columnName)
        {
            var value = GetValue(row, columnName);
            if (value == null || value == DBNull.Value)
                return 0;

            if (value is int i) return i;
            if (value is long l) return checked((int)l);
            if (value is double d) return Convert.ToInt32(d);
            if (value is decimal m) return Convert.ToInt32(m);

            return int.TryParse(Convert.ToString(value), out var parsed) ? parsed : 0;
        }

        private static bool? GetNullableBool(IDictionary<string, object> row, string columnName)
        {
            var value = GetValue(row, columnName);
            if (value == null || value == DBNull.Value)
                return null;

            if (value is bool b)
                return b;

            if (value is byte by)
                return by != 0;

            if (value is short sh)
                return sh != 0;

            if (value is int i)
                return i != 0;

            if (value is long l)
                return l != 0;

            if (value is double d)
                return Math.Abs(d) > double.Epsilon;

            var text = Convert.ToString(value)?.Trim();
            if (string.IsNullOrEmpty(text))
                return null;

            if (bool.TryParse(text, out var parsedBool))
                return parsedBool;

            if (double.TryParse(text, out var parsedNumber))
                return Math.Abs(parsedNumber) > double.Epsilon;

            return null;
        }
    }
}
