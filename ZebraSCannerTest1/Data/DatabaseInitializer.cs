using Microsoft.Data.Sqlite;
using ZebraSCannerTest1.Core.Enums;

namespace ZebraSCannerTest1.Data
{
    public static class DatabaseInitializer
    {
        public static string GetDatabasePath(
            int userId,
            InventoryMode mode,
            string serverKey = "prod",
            int? documentId = null,
            string? documentModule = null,
            string? assignmentRole = null)
        {
            var dbName = BuildDatabaseName(
                userId,
                mode,
                serverKey,
                documentId,
                documentModule,
                assignmentRole);

            return Path.Combine(FileSystem.AppDataDirectory, dbName);
        }

        public static SqliteConnection GetConnection(
            int userId,
            InventoryMode mode,
            string serverKey = "prod",
            int? documentId = null,
            string? documentModule = null,
            string? assignmentRole = null)
        {
            var dbPath = GetDatabasePath(
                userId,
                mode,
                serverKey,
                documentId,
                documentModule,
                assignmentRole);

            var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();

            Initialize(
                conn,
                userId,
                mode,
                serverKey,
                documentId,
                documentModule,
                assignmentRole);

            return conn;
        }

        private static string BuildDatabaseName(
            int userId,
            InventoryMode mode,
            string serverKey,
            int? documentId = null,
            string? documentModule = null,
            string? assignmentRole = null)
        {
            var safeServerKey = ToFilePart(serverKey, "prod");
            var modePart = mode == InventoryMode.Loots ? "loots" : "standard";

            if (documentId.HasValue && documentId.Value > 0 && !string.IsNullOrWhiteSpace(documentModule))
            {
                var modulePart = ToFilePart(documentModule, "document");
                var rolePart = ToFilePart(assignmentRole, "default");

                return $"zebraScanner_{safeServerKey}_user_{userId}_{modulePart}_{documentId.Value}_{rolePart}_{modePart}.db";
            }

            // Legacy/manual fallback. Real document scanning should always use document-aware DB names.
            return $"zebraScanner_{safeServerKey}_user_{userId}_{modePart}.db";
        }

        public static void Initialize(
            SqliteConnection conn,
            int userId,
            InventoryMode mode,
            string serverKey,
            int? documentId = null,
            string? documentModule = null,
            string? assignmentRole = null)
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

            EnsureAppMetadataColumns(conn);
            EnsureMetadata(conn, userId, mode, serverKey, documentId, documentModule, assignmentRole);
        }

        private static void EnsureAppMetadataColumns(SqliteConnection conn)
        {
            AddColumnIfMissing(conn, "AppMetadata", "DocumentId", "INTEGER");
            AddColumnIfMissing(conn, "AppMetadata", "DocumentModule", "TEXT");
            AddColumnIfMissing(conn, "AppMetadata", "AssignmentRole", "TEXT");
        }

        private static void AddColumnIfMissing(
            SqliteConnection conn,
            string tableName,
            string columnName,
            string definition)
        {
            if (ColumnExists(conn, tableName, columnName))
                return;

            using var alter = conn.CreateCommand();
            alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition};";
            alter.ExecuteNonQuery();
        }

        private static bool ColumnExists(SqliteConnection conn, string tableName, string columnName)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({tableName});";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var existingColumn = reader.GetString(1);
                if (string.Equals(existingColumn, columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void EnsureMetadata(
            SqliteConnection conn,
            int userId,
            InventoryMode mode,
            string serverKey,
            int? documentId = null,
            string? documentModule = null,
            string? assignmentRole = null)
        {
            var now = DateTime.UtcNow.ToString("o");
            var modeValue = mode.ToString();
            var normalizedServerKey = NormalizeOrDefault(serverKey, "prod");
            var normalizedDocumentModule = NormalizeNullable(documentModule);
            var normalizedAssignmentRole = NormalizeNullable(assignmentRole);
            const int schemaVersion = 2;

            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM AppMetadata WHERE Id = 1;";
            var exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;

            if (!exists)
            {
                using var insertCmd = conn.CreateCommand();
                insertCmd.CommandText = @"
INSERT INTO AppMetadata
    (Id, UserId, ServerKey, InventoryMode, SchemaVersion, CreatedAt, UpdatedAt, DocumentId, DocumentModule, AssignmentRole)
VALUES
    (1, $userId, $serverKey, $inventoryMode, $schemaVersion, $createdAt, $updatedAt, $documentId, $documentModule, $assignmentRole);";

                insertCmd.Parameters.AddWithValue("$userId", userId);
                insertCmd.Parameters.AddWithValue("$serverKey", normalizedServerKey);
                insertCmd.Parameters.AddWithValue("$inventoryMode", modeValue);
                insertCmd.Parameters.AddWithValue("$schemaVersion", schemaVersion);
                insertCmd.Parameters.AddWithValue("$createdAt", now);
                insertCmd.Parameters.AddWithValue("$updatedAt", now);
                insertCmd.Parameters.AddWithValue("$documentId", documentId.HasValue ? (object)documentId.Value : DBNull.Value);
                insertCmd.Parameters.AddWithValue("$documentModule", normalizedDocumentModule ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("$assignmentRole", normalizedAssignmentRole ?? (object)DBNull.Value);

                insertCmd.ExecuteNonQuery();
            }
            else
            {
                using var validateCmd = conn.CreateCommand();
                validateCmd.CommandText = @"
SELECT UserId, ServerKey, InventoryMode, DocumentId, DocumentModule, AssignmentRole
FROM AppMetadata
WHERE Id = 1;";

                using var reader = validateCmd.ExecuteReader();

                if (reader.Read())
                {
                    var existingUserId = reader.GetInt32(0);
                    var existingServerKey = reader.GetString(1);
                    var existingMode = reader.GetString(2);
                    int? existingDocumentId = reader.IsDBNull(3) ? null : reader.GetInt32(3);
                    var existingDocumentModule = reader.IsDBNull(4) ? null : reader.GetString(4);
                    var existingAssignmentRole = reader.IsDBNull(5) ? null : reader.GetString(5);

                    if (existingUserId != userId
                        || existingServerKey != normalizedServerKey
                        || existingMode != modeValue
                        || existingDocumentId != documentId
                        || !StringEquals(existingDocumentModule, normalizedDocumentModule)
                        || !StringEquals(existingAssignmentRole, normalizedAssignmentRole))
                    {
                        throw new InvalidOperationException(
                            "Database metadata does not match the current user/document/session.");
                    }
                }
            }
        }

        private static bool StringEquals(string? left, string? right)
        {
            return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeOrDefault(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
        }

        private static string ToFilePart(string? value, string fallback)
        {
            var normalized = NormalizeOrDefault(value, fallback);
            var chars = normalized
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
                .ToArray();

            var result = new string(chars).Trim('_');
            return string.IsNullOrWhiteSpace(result) ? fallback : result;
        }
    }
}
