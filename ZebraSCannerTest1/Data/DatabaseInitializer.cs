using Microsoft.Data.Sqlite;
using System.IO;

namespace ZebraSCannerTest1.Data
{
    public static class DatabaseInitializer
    {
        public static SqliteConnection GetConnection()
        {
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "scanner.db3");

            var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();

            using (var pragma = conn.CreateCommand())
            {
                pragma.CommandText = @"
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA temp_store = MEMORY;
PRAGMA page_size = 4096;
PRAGMA cache_size = -20000;";
                pragma.ExecuteNonQuery();
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Products (
    Barcode         TEXT PRIMARY KEY,
    InitialQuantity INTEGER NOT NULL,
    ScannedQuantity INTEGER NOT NULL,
    CreatedAt       TEXT NOT NULL,
    UpdatedAt       TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS ScanLogs (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    Barcode         TEXT NOT NULL,
    ScannedQuantity INTEGER NOT NULL,
    Timestamp       TEXT NOT NULL,
    FOREIGN KEY (Barcode) REFERENCES Products (Barcode)
);

CREATE INDEX IF NOT EXISTS idx_logs_barcode_ts ON ScanLogs(Barcode, Timestamp DESC);
";
            cmd.ExecuteNonQuery();

            return conn;
        }
    }
}
