
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

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS InitialProducts (
                Id INTEGER PRIMARY KEY,
                Barcode TEXT NOT NULL UNIQUE,
                Quantity INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ScannedProducts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Barcode TEXT NOT NULL UNIQUE,
                Quantity INTEGER NOT NULL,
                InitialQuantity INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ScanLogs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Barcode TEXT NOT NULL,
                Quantity INTEGER NOT NULL,
                InitialQuantity INTEGER NOT NULL,
                Timestamp TEXT NOT NULL,
                ScannedProductId INTEGER
            );

            CREATE INDEX IF NOT EXISTS idx_initial_barcode ON InitialProducts(Barcode);
            CREATE INDEX IF NOT EXISTS idx_scanned_barcode ON ScannedProducts(Barcode);
            ";
            cmd.ExecuteNonQuery();

            return conn;
        }
    }
}
