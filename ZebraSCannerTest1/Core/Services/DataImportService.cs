using Microsoft.Data.Sqlite;
using System.Text.Json;
using ZebraSCannerTest1.Core.Dtos;
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
        public async Task ImportExcelAsync(Stream stream, string? fileName = null)
        {
            await _excelImport.ImportExcelAsync(stream, fileName);
        }


        // ✅ Import SQLite DB (from FilePicker Stream)
        public async Task ImportDbAsync(Stream dbStream)
        {
            try
            {
                var targetPath = Path.Combine(FileSystem.AppDataDirectory, "zebraScannerData.db");

                // Overwrite safely inside app sandbox
                using (var dst = File.Create(targetPath))
                    await dbStream.CopyToAsync(dst);

                // Reopen connection to new DB
                _conn.Close();
                _conn.ConnectionString = $"Data Source={targetPath}";

                int retries = 3;
                while (retries-- > 0)
                {
                    try
                    {
                        _conn.Open();
                        break;
                    }
                    catch (SqliteException)
                    {
                        if (retries == 0) throw;
                        await Task.Delay(200); // wait before retry
                    }
                }
                DatabaseInitializer.Initialize(_conn); // ← THIS FIXES THE 'NO COLUMN NAMED SECTION' ERROR

            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to import DB: {ex.Message}", ex);
            }

            // ✅ Ensure all required tables exist (including new ScanLogs schema)
            using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Products (
                        Barcode TEXT PRIMARY KEY,
                        InitialQuantity INTEGER NOT NULL DEFAULT 0,
                        ScannedQuantity INTEGER NOT NULL DEFAULT 0,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL,
                        Name TEXT,
                        Color TEXT,
                        Size TEXT,
                        Price TEXT,
                        ArticCode TEXT
                    );

                    CREATE TABLE IF NOT EXISTS ScanLogs (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Barcode TEXT NOT NULL,
                        Was INTEGER NOT NULL DEFAULT 0,
                        IncrementBy INTEGER NOT NULL DEFAULT 1,
                        IsValue INTEGER NOT NULL DEFAULT 0,
                        UpdatedAt TEXT NOT NULL,
                        IsManual INTEGER NULL,
                        Section TEXT NULL
                    );

                    CREATE TABLE IF NOT EXISTS ScannedProducts (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Barcode TEXT NOT NULL,
                        Quantity INTEGER NOT NULL,
                        InitialQuantity INTEGER NOT NULL DEFAULT 0,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL,
                        Name TEXT,
                        Color TEXT,
                        Size TEXT,
                        Price TEXT,
                        ArticCode TEXT
                    );
                    ";
                cmd.ExecuteNonQuery();
            }

            // 🧩 Optional migration if old ScanLogs schema exists
            try
            {
                using var alter = _conn.CreateCommand();
                alter.CommandText = @"
                ALTER TABLE ScanLogs ADD COLUMN Was INTEGER DEFAULT 0;
                ALTER TABLE ScanLogs ADD COLUMN IncrementBy INTEGER DEFAULT 0;
                ALTER TABLE ScanLogs ADD COLUMN IsValue INTEGER DEFAULT 0;
                ALTER TABLE ScanLogs ADD COLUMN UpdatedAt TEXT DEFAULT '';
                ALTER TABLE ScanLogs ADD COLUMN IsManual INTEGER DEFAULT null;
                ";
                alter.ExecuteNonQuery();
            }
            catch
            {
                // Ignore errors if columns already exist
            }
        }

        // ✅ Import JSON via Stream
        public async Task<int> ImportJsonAsync(Stream jsonStream)
        {
            using var reader = new StreamReader(jsonStream);
            var json = await reader.ReadToEndAsync();

            // Deserialize JSON to list of DTOs
            var items = JsonSerializer.Deserialize<List<JsonDto>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (items == null || items.Count == 0)
                throw new Exception("No valid items found in JSON file.");

            using var tx = _conn.BeginTransaction();
            using var insert = _conn.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = @"
                INSERT OR REPLACE INTO Products
                (Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt, Name, Color, Size, Price, ArticCode)
                VALUES ($barcode, $initial, $scanned, $created, $updated, $name, $color, $size, $price, $artic);";

            insert.Parameters.Add("$barcode", SqliteType.Text);
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

                insert.ExecuteNonQuery();
                processed++;
            }

            tx.Commit();
            Console.WriteLine($"[IMPORT] ✅ JSON import complete: {processed} rows");

            return processed;
        }
    }

}
