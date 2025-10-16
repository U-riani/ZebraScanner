using Microsoft.Data.Sqlite;

namespace ZebraSCannerTest1.Data
{
    public static class DatabaseInitializer
    {
        private const string DbFileName = "zebraScannerData.db";

        public static SqliteConnection GetConnection()
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, DbFileName);


            ////// 💣 optional reset — deletes old DB
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

            Console.WriteLine("🧩 DatabaseInitializer.Initialize() called");

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
    Name TEXT NOCASE,
    Color TEXT NOCASE,
    Size TEXT NOCASE,
    Price TEXT NOCASE,        -- stored as TEXT (Excel string values)
    ArticCode TEXT NOCASE
);

-- ✅ Logs of scans
CREATE TABLE IF NOT EXISTS ScanLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Barcode TEXT NOT NULL,
    Was INTEGER NOT NULL DEFAULT 0,
    IncrementBy INTEGER NOT NULL DEFAULT 1,
    IsValue INTEGER NOT NULL DEFAULT 0,
    UpdatedAt TEXT NOT NULL,
    IsManual INTEGER DEFAULT NULL,
    Section Text DEFAULT NULL
);



-- ✅ Scanned products (history of sessions)
CREATE TABLE IF NOT EXISTS ScannedProducts (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Barcode TEXT NOT NULL,
    Quantity INTEGER NOT NULL,
    InitialQuantity INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    Name TEXT NOCASE,
    Color TEXT NOCASE,
    Size TEXT NOCASE,
    Price TEXT NOCASE,
    ArticCode TEXT NOCASE
);
";
            cmd.ExecuteNonQuery();
            Console.WriteLine("-------- Tables created");


            // 🔹 Upgrade check for IsManual column (for existing databases)
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "PRAGMA table_info(ScanLogs)";
            using var reader = checkCmd.ExecuteReader();

            var existingCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (reader.Read())
                existingCols.Add(reader.GetString(1));
            reader.Close();

            if (!existingCols.Contains("IsManual"))
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE ScanLogs ADD COLUMN IsManual INTEGER DEFAULT NULL;";
                alterCmd.ExecuteNonQuery();
                Console.WriteLine("🩹 Added missing column: IsManual");
            }

            // Add Section if missing
            if (!existingCols.Contains("Section"))
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE ScanLogs ADD COLUMN Section TEXT DEFAULT NULL;";
                alterCmd.ExecuteNonQuery();
                Console.WriteLine("🩹 Added missing column: Section");
            }
        }
    }
}
