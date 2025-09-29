using Microsoft.Data.Sqlite;

namespace ZebraSCannerTest1.Data
{
    public static class DatabaseInitializer
    {
        private const string DbFileName = "appdata.db";

        /// <summary>
        /// Opens a SQLite connection, ensures tables exist, and returns the connection.
        /// </summary>
        public static SqliteConnection GetConnection()
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, DbFileName);
            var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();

            Initialize(conn);

            return conn;
        }

        /// <summary>
        /// Creates tables if they don’t already exist.
        /// </summary>
        public static void Initialize(SqliteConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS Products (
    Barcode TEXT PRIMARY KEY,
    InitialQuantity INTEGER NOT NULL DEFAULT 0,
    ScannedQuantity INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS ScanLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Barcode TEXT NOT NULL,
    ScannedQuantity INTEGER NOT NULL,
    Timestamp TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS ScannedProducts (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Barcode TEXT NOT NULL,
    Quantity INTEGER NOT NULL,
    InitialQuantity INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);
";
            cmd.ExecuteNonQuery();
        }
    }
}
