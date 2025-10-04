using Microsoft.Data.Sqlite;

namespace ZebraSCannerTest1.Data
{
    public static class DatabaseInitializer
    {
        private const string DbFileName = "zebraScannerData.db";

        public static SqliteConnection GetConnection()
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, DbFileName);
            var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();

            Initialize(conn);

            return conn;
        }

        public static void Initialize(SqliteConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
PRAGMA foreign_keys = ON;

-- ✅ Products table
CREATE TABLE IF NOT EXISTS Products (
    Barcode TEXT PRIMARY KEY,
    InitialQuantity INTEGER NOT NULL DEFAULT 0,
    ScannedQuantity INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    Name TEXT,
    Color TEXT,
    Size TEXT,
    Price TEXT,        -- stored as TEXT (Excel string values)
    ArticCode TEXT
);

-- ✅ Logs of scans
CREATE TABLE IF NOT EXISTS ScanLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Barcode TEXT NOT NULL,
    Was INTEGER NOT NULL DEFAULT 0,
    IncrementBy INTEGER NOT NULL DEFAULT 1,
    IsValue INTEGER NOT NULL DEFAULT 0,
    UpdatedAt TEXT NOT NULL
);



-- ✅ Scanned products (history of sessions)
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
    }
}
