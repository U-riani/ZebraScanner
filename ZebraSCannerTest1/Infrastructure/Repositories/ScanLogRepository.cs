using Microsoft.Data.Sqlite;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.Infrastructure.Repositories
{
    public class ScanLogRepository : IScanLogRepository
    {
        private readonly IDbFactory _db;

        public ScanLogRepository(IDbFactory db)
        {
            _db = db;
        }

        private static string GetTable(InventoryMode mode)
            => mode == InventoryMode.Loots ? "LootsScanLogs" : "ScanLogs";

        private SqliteConnection Conn(InventoryMode mode) => _db.Inventorization(mode);

        public async Task InsertAsync(ScanLog log, InventoryMode mode = InventoryMode.Standard)
        {
            var table = GetTable(mode);

            using var conn = Conn(mode);
            using var cmd = conn.CreateCommand();

            if (mode == InventoryMode.Loots)
            {
                cmd.CommandText = $@"
                    INSERT INTO {table}
                    (Barcode, Was, IncrementBy, IsValue, UpdatedAt, Section, IsManual, Box_Id)
                    VALUES ($b, $w, $i, $v, $u, $s, $m, $box)";
                cmd.Parameters.AddWithValue("$box", log.Box_Id ?? (object)DBNull.Value);
            }
            else
            {
                cmd.CommandText = $@"
                    INSERT INTO {table}
                    (Barcode, Was, IncrementBy, IsValue, UpdatedAt, Section, IsManual)
                    VALUES ($b, $w, $i, $v, $u, $s, $m)";
            }

            cmd.Parameters.AddWithValue("$b", log.Barcode);
            cmd.Parameters.AddWithValue("$w", log.Was);
            cmd.Parameters.AddWithValue("$i", log.IncrementBy);
            cmd.Parameters.AddWithValue("$v", log.IsValue);
            cmd.Parameters.AddWithValue("$u", log.UpdatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("$s", log.Section ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$m", log.IsManual ?? (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<ScanLog>> GetByBarcodeAsync(string barcode, InventoryMode mode = InventoryMode.Standard)
        {
            var logs = new List<ScanLog>();
            var table = GetTable(mode);

            using var conn = Conn(mode);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = $@"
                SELECT Barcode, Was, IncrementBy, IsValue, UpdatedAt, IsManual, Section
                       {(mode == InventoryMode.Loots ? ", Box_Id" : "")}
                FROM {table}
                WHERE Barcode = $b
                ORDER BY UpdatedAt DESC";

            cmd.Parameters.AddWithValue("$b", barcode);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var log = new ScanLog
                {
                    Barcode = reader.GetString(0),
                    Was = reader.GetInt32(1),
                    IncrementBy = reader.GetInt32(2),
                    IsValue = reader.GetInt32(3),
                    UpdatedAt = DateTime.Parse(reader.GetString(4)),
                    IsManual = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    Section = reader.IsDBNull(6) ? null : reader.GetString(6)
                };

                if (mode == InventoryMode.Loots)
                    log.Box_Id = reader.IsDBNull(7) ? null : reader.GetString(7);

                logs.Add(log);
            }

            return logs;
        }

        public async Task ClearAsync(InventoryMode mode = InventoryMode.Standard)
        {
            var table = GetTable(mode);

            using var conn = Conn(mode);
            using var cmd = conn.CreateCommand();

            cmd.CommandText = $"DELETE FROM {table}";
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
