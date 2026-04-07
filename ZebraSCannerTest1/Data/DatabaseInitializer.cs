using Microsoft.Data.Sqlite;
using ZebraSCannerTest1.Core.Enums;

namespace ZebraSCannerTest1.Data
{
    public static class DatabaseInitializer
    {

        public static string GetDatabasePath(
        int userId,
        InventoryMode mode,
        string serverKey = "prod")
        {
            var dbName = BuildDatabaseName(userId, mode, serverKey);
            return Path.Combine(FileSystem.AppDataDirectory, dbName);
        }

        public static SqliteConnection GetConnection(
            int userId,
            InventoryMode mode,
            string serverKey = "prod")
        {
            var dbName = BuildDatabaseName(userId, mode, serverKey);
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, dbName);

            var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();

            Initialize(conn, userId, mode, serverKey);

            return conn;
        }

        private static string BuildDatabaseName(
            int userId,
            InventoryMode mode,
            string serverKey)
        {
            var modePart = mode == InventoryMode.Loots ? "loots" : "standard";

            return $"zebraScanner_{serverKey}_user_{userId}_{modePart}.db";
        }

        public static void Initialize(
            SqliteConnection conn,
            int userId,
            InventoryMode mode,
            string serverKey)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS AppMetadata (
    Id INTEGER PRIMARY KEY CHECK (Id = 1),
    UserId INTEGER NOT NULL,
    ServerKey TEXT NOT NULL,
    InventoryMode TEXT NOT NULL,
    SchemaVersion INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);

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
    ArticCode TEXT
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

            EnsureMetadata(conn, userId, mode, serverKey);
        }

        private static void EnsureMetadata(
            SqliteConnection conn,
            int userId,
            InventoryMode mode,
            string serverKey)
        {
            var now = DateTime.UtcNow.ToString("o");
            var modeValue = mode.ToString();
            const int schemaVersion = 1;

            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM AppMetadata WHERE Id = 1;";
            var exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;

            if (!exists)
            {
                using var insertCmd = conn.CreateCommand();
                insertCmd.CommandText = @"
INSERT INTO AppMetadata
    (Id, UserId, ServerKey, InventoryMode, SchemaVersion, CreatedAt, UpdatedAt)
VALUES
    (1, $userId, $serverKey, $inventoryMode, $schemaVersion, $createdAt, $updatedAt);";

                insertCmd.Parameters.AddWithValue("$userId", userId);
                insertCmd.Parameters.AddWithValue("$serverKey", serverKey);
                insertCmd.Parameters.AddWithValue("$inventoryMode", modeValue);
                insertCmd.Parameters.AddWithValue("$schemaVersion", schemaVersion);
                insertCmd.Parameters.AddWithValue("$createdAt", now);
                insertCmd.Parameters.AddWithValue("$updatedAt", now);

                insertCmd.ExecuteNonQuery();
            }
            else
            {
                using var validateCmd = conn.CreateCommand();
                validateCmd.CommandText = @"
SELECT UserId, ServerKey, InventoryMode
FROM AppMetadata
WHERE Id = 1;";
                using var reader = validateCmd.ExecuteReader();

                if (reader.Read())
                {
                    var existingUserId = reader.GetInt32(0);
                    var existingServerKey = reader.GetString(1);
                    var existingMode = reader.GetString(2);

                    if (existingUserId != userId ||
                        existingServerKey != serverKey ||
                        existingMode != modeValue)
                    {
                        throw new InvalidOperationException(
                            "Database metadata does not match the current user/session.");
                    }
                }
            }
        }
    }
}