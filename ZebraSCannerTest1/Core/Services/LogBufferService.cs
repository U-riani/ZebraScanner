using Microsoft.Data.Sqlite;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.Core.Services
{
    public class LogBufferService
    {
        private readonly SqliteConnection _conn;
        private readonly List<ScanLog> _buffer = new();
        private readonly object _lock = new();
        private readonly Timer _timer;

        private InventoryMode _mode = InventoryMode.Standard;

        public LogBufferService(SqliteConnection conn)
        {
            _conn = conn;
            _timer = new Timer(_ => Flush(_mode), null, 2000, 2000);
        }

        public void SetMode(InventoryMode mode)
        {
            _mode = mode;
        }

        public void AddLog(ScanLog log)
        {
            lock (_lock)
            {
                _buffer.Add(log);
            }
        }

        public void Flush(InventoryMode mode = InventoryMode.Standard)
        {
            List<ScanLog> toWrite;
            lock (_lock)
            {
                if (_buffer.Count == 0) return;
                toWrite = new List<ScanLog>(_buffer);
                _buffer.Clear();
            }

            var table = mode == InventoryMode.Loots ? "LootsScanLogs" : "ScanLogs";
            using var conn = new SqliteConnection($"Data Source={Path.Combine(FileSystem.AppDataDirectory,
                mode == InventoryMode.Loots ? "zebraScanner_loots.db" : "zebraScanner_standard.db")}");
            conn.Open();

            using var tx = conn.BeginTransaction();
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;

            if (mode == InventoryMode.Loots)
                cmd.CommandText = @"INSERT INTO LootsScanLogs
            (Barcode, Was, IncrementBy, IsValue, UpdatedAt, Section, Box_Id)
            VALUES ($b,$w,$i,$s,$t,$sec,$box);";
            else
                cmd.CommandText = @"INSERT INTO ScanLogs
            (Barcode, Was, IncrementBy, IsValue, UpdatedAt, Section)
            VALUES ($b,$w,$i,$s,$t,$sec);";

            cmd.Parameters.Add("$b", SqliteType.Text);
            cmd.Parameters.Add("$w", SqliteType.Integer);
            cmd.Parameters.Add("$i", SqliteType.Integer);
            cmd.Parameters.Add("$s", SqliteType.Integer);
            cmd.Parameters.Add("$t", SqliteType.Text);
            cmd.Parameters.Add("$sec", SqliteType.Text);
            cmd.Parameters.Add("$box", SqliteType.Text);

            foreach (var log in toWrite)
            {
                cmd.Parameters["$b"].Value = log.Barcode;
                cmd.Parameters["$w"].Value = log.Was;
                cmd.Parameters["$i"].Value = log.IncrementBy;
                cmd.Parameters["$s"].Value = log.IsValue;
                cmd.Parameters["$t"].Value = log.UpdatedAt.ToString("o");
                cmd.Parameters["$sec"].Value = string.IsNullOrEmpty(log.Section) ? DBNull.Value : log.Section;
                cmd.Parameters["$box"].Value = string.IsNullOrEmpty(log.Box_Id) ? DBNull.Value : log.Box_Id;

                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }


        public void Clear()
        {
            lock (_lock) { _buffer.Clear(); }
        }
    }
}
