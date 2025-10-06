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
    UpdatedAt TEXT NOT NULL,
    IsManual INTEGER DEFAULT NULL
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

            // 🔹 Upgrade check for IsManual column (for existing databases)
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "PRAGMA table_info(ScanLogs)";
            using var reader = checkCmd.ExecuteReader();

            bool hasIsManual = false;
            while (reader.Read())
            {
                var columnName = reader.GetString(1);
                if (columnName.Equals("IsManual", StringComparison.OrdinalIgnoreCase))
                {
                    hasIsManual = true;
                    break;
                }
            }
            reader.Close();

            if (!hasIsManual)
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE ScanLogs ADD COLUMN IsManual INTEGER DEFAULT NULL;";
                alterCmd.ExecuteNonQuery();
            }
        }
    }
}
