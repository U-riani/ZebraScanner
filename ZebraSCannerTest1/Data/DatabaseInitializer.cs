using Microsoft.Data.Sqlite;
using ZebraSCannerTest1.Core.Enums;

namespace ZebraSCannerTest1.Data
{
    public static class DatabaseInitializer
    {
        private const string StandardDb = "zebraScanner_standard.db";
        private const string LootsDb = "zebraScanner_loots.db";

        public static SqliteConnection GetConnection(InventoryMode mode)
        {
            var dbName = mode == InventoryMode.Loots
                ? "zebraScanner_loots.db"
                : "zebraScanner_standard.db";

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, dbName);
            //// 💣 optional reset — deletes old DB
            //if (File.Exists(dbPath))
            //{
            //    try
            //    {
            //        File.Delete(dbPath);
            //        Console.WriteLine($"🧹 Deleted old database at {dbPath}");
            //    }
            //    catch (Exception ex)
            //    {
            //        Console.WriteLine($"⚠️ Failed to delete DB: {ex.Message}");
            //    }
            //}
            var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            Initialize(conn, mode);
            return conn;
        }


        public static void Initialize(SqliteConnection conn, InventoryMode mode)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
PRAGMA foreign_keys = ON;

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
    ArticCode TEXT,
    Hall INTEGER NULL,
    BaseDspa TEXT NULL
);

CREATE TABLE IF NOT EXISTS LootsProducts (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Barcode TEXT NOT NULL,
    Box_Id TEXT,
    InitialQuantity INTEGER NOT NULL DEFAULT 0,
    ScannedQuantity INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    Name TEXT,
    Color TEXT,
    Size TEXT,
    Price TEXT,
    ArticCode TEXT,
    Hall INTEGER NULL,
    BaseDspa TEXT NULL
);

CREATE TABLE IF NOT EXISTS ScanLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Barcode TEXT NOT NULL,
    Was INTEGER NOT NULL DEFAULT 0,
    IncrementBy INTEGER NOT NULL DEFAULT 1,
    IsValue INTEGER NOT NULL DEFAULT 0,
    UpdatedAt TEXT NOT NULL,
    IsManual INTEGER DEFAULT NULL,
    Section TEXT DEFAULT NULL
);

CREATE TABLE IF NOT EXISTS LootsScanLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Barcode TEXT NOT NULL,
    Box_Id TEXT,
    Was INTEGER NOT NULL DEFAULT 0,
    IncrementBy INTEGER NOT NULL DEFAULT 1,
    IsValue INTEGER NOT NULL DEFAULT 0,
    UpdatedAt TEXT NOT NULL,
    IsManual INTEGER DEFAULT NULL,
    Section TEXT DEFAULT NULL
);
";
            cmd.ExecuteNonQuery();

            // Existing V18 databases already have the tables, so CREATE TABLE IF NOT EXISTS
            // alone would not add the new columns. Migrate in place without deleting data.
            EnsureColumn(conn, "Products", "Hall", "INTEGER NULL");
            EnsureColumn(conn, "Products", "BaseDspa", "TEXT NULL");
            EnsureColumn(conn, "LootsProducts", "Hall", "INTEGER NULL");
            EnsureColumn(conn, "LootsProducts", "BaseDspa", "TEXT NULL");
        }

        private static void EnsureColumn(
            SqliteConnection conn,
            string tableName,
            string columnName,
            string columnDefinition)
        {
            bool exists = false;

            using (var check = conn.CreateCommand())
            {
                check.CommandText = $"PRAGMA table_info({tableName});";
                using var reader = check.ExecuteReader();
                while (reader.Read())
                {
                    if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
            }

            if (exists) return;

            using var alter = conn.CreateCommand();
            alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
            alter.ExecuteNonQuery();
            Console.WriteLine($"[DB MIGRATION] Added {tableName}.{columnName}");
        }
    }
}
