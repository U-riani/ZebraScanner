using Microsoft.Data.Sqlite;
using System.Text.Json;
using ZebraSCannerTest1.Core.Dtos;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Data;

namespace ZebraSCannerTest1.Core.Services
{
    /// <summary>
    /// Handles importing data from multiple sources (Excel, JSON, or SQLite DB).
    /// </summary>
    public class DataImportService : IDataImportService
    {
        private readonly SqliteConnection _conn;
        private readonly ExcelImportService _excelImport;

        public DataImportService(SqliteConnection conn)
        {
            _conn = conn;
            _excelImport = new ExcelImportService(conn);
        }

        // ✅ Import Excel (already uses MiniExcel)
        public async Task ImportExcelAsync(Stream stream, InventoryMode mode = InventoryMode.Standard, string? fileName = null)
        {
            await _excelImport.ImportExcelAsync(stream, mode, fileName);
        }


        // ✅ Import SQLite DB (from FilePicker Stream)
        public async Task ImportDbAsync(Stream dbStream, InventoryMode mode = InventoryMode.Standard)
        {
            try
            {
                // ✅ pick correct file for each mode
                var fileName = mode == InventoryMode.Loots
                    ? "zebraScanner_loots.db"
                    : "zebraScanner_standard.db";

                var targetPath = Path.Combine(FileSystem.AppDataDirectory, fileName);

                // ✅ optional: backup before overwrite
                if (File.Exists(targetPath))
                {
                    var backup = targetPath + ".bak";
                    File.Copy(targetPath, backup, true);
                    Console.WriteLine($"[DB] Backup created: {backup}");
                }

                // ✅ overwrite only that mode’s DB file
                using (var dst = File.Create(targetPath))
                    await dbStream.CopyToAsync(dst);

                // ✅ reconnect only to that mode’s DB
                _conn.Close();
                _conn.ConnectionString = $"Data Source={targetPath}";
                _conn.Open();

                DatabaseInitializer.Initialize(_conn, mode);

                Console.WriteLine($"[DB] Successfully imported {mode} DB → {targetPath}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to import DB ({mode}): {ex.Message}", ex);
            }
        }


        // ✅ Import JSON via Stream
        public async Task<int> ImportJsonAsync(Stream jsonStream, InventoryMode mode = InventoryMode.Standard)
        {
            string table = mode == InventoryMode.Loots ? "LootsProducts" : "Products";
            bool isLoots = mode == InventoryMode.Loots;

            if (isLoots)
            {
                using var check = _conn.CreateCommand();
                check.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='LootsProducts';";
                var exists = check.ExecuteScalar() != null;
                if (!exists)
                    throw new InvalidOperationException("LootsProducts table not found. Please initialize database first.");
            }

            using var reader = new StreamReader(jsonStream);
            var json = await reader.ReadToEndAsync();

            // Deserialize JSON to list of DTOs
            var items = JsonSerializer.Deserialize<List<JsonDto>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (items == null || items.Count == 0)
                throw new Exception("No valid items found in JSON file.");
            // 🔥 Delete old data for this mode first
            using (var clear = _conn.CreateCommand())
            {
                string clearTable = isLoots ? "LootsProducts" : "Products";
                clear.CommandText = $"DELETE FROM {clearTable};";
                clear.ExecuteNonQuery();
                Console.WriteLine($"[IMPORT] Cleared old data from {clearTable}");
            }

            using var tx = _conn.BeginTransaction();
            using var insert = _conn.CreateCommand();

            insert.Transaction = tx;
            insert.CommandText = isLoots
                ? $@"
                    INSERT OR REPLACE INTO {table}
                    (Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt, Name, Color, Size, Price, ArticCode)
                    VALUES ($barcode, $box, $initial, $scanned, $created, $updated, $name, $color, $size, $price, $artic);"
                : $@"
                    INSERT OR REPLACE INTO {table}
                    (Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt, Name, Color, Size, Price, ArticCode)
                    VALUES ($barcode, $initial, $scanned, $created, $updated, $name, $color, $size, $price, $artic);";

            // Add parameters
            insert.Parameters.Add("$barcode", SqliteType.Text);
            insert.Parameters.Add("$box", SqliteType.Text);
            insert.Parameters.Add("$initial", SqliteType.Integer);
            insert.Parameters.Add("$scanned", SqliteType.Integer);
            insert.Parameters.Add("$created", SqliteType.Text);
            insert.Parameters.Add("$updated", SqliteType.Text);
            insert.Parameters.Add("$name", SqliteType.Text);
            insert.Parameters.Add("$color", SqliteType.Text);
            insert.Parameters.Add("$size", SqliteType.Text);
            insert.Parameters.Add("$price", SqliteType.Text);
            insert.Parameters.Add("$artic", SqliteType.Text);

            int processed = 0;
            var now = DateTime.UtcNow.ToString("o");

            try
            {
                foreach (var p in items)
                {
                    if (string.IsNullOrWhiteSpace(p.Barcode))
                        continue;

                    insert.Parameters["$barcode"].Value = p.Barcode.Trim();
                    insert.Parameters["$initial"].Value = p.InitialQuantity;
                    insert.Parameters["$scanned"].Value = p.ScannedQuantity;
                    insert.Parameters["$created"].Value = string.IsNullOrWhiteSpace(p.CreatedAt) ? now : p.CreatedAt;
                    insert.Parameters["$updated"].Value = string.IsNullOrWhiteSpace(p.UpdatedAt) ? now : p.UpdatedAt;
                    insert.Parameters["$name"].Value = p.Name ?? "";
                    insert.Parameters["$color"].Value = p.Color ?? "";
                    insert.Parameters["$size"].Value = p.Size ?? "";
                    insert.Parameters["$price"].Value = p.Price ?? "";
                    insert.Parameters["$artic"].Value = p.ArticCode ?? "";

                    if (isLoots)
                    {
                        insert.Parameters["$box"].Value =
                            p.GetType().GetProperty("Box_Id")?.GetValue(p)?.ToString()?.Trim()
                            ?? "UnknownBox";
                    }

                    insert.ExecuteNonQuery();
                    processed++;
                }

                tx.Commit();
            } catch (Exception ex)
            {
                tx.Rollback();
                Console.WriteLine($"❌ JSON import failed after {processed} rows → {ex.Message}");
                throw;
            }
            Console.WriteLine($"[IMPORT] ✅ JSON import complete → {processed} rows ({mode})");

            return processed;
        }
    }

}
