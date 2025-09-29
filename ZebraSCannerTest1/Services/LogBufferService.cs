using Microsoft.Data.Sqlite;
using ZebraSCannerTest1.Models;

namespace ZebraSCannerTest1.Services
{
    public class LogBufferService
    {
        private readonly SqliteConnection _conn;
        private readonly List<ScanLog> _buffer = new();
        private readonly object _lock = new();
        private readonly Timer _timer;

        public LogBufferService(SqliteConnection conn)
        {
            _conn = conn;
            // Flush buffer every 2 seconds
            _timer = new Timer(_ => Flush(), null, 2000, 2000);
        }

        public void AddLog(ScanLog log)
        {
            lock (_lock)
            {
                _buffer.Add(log);
            }
        }

        public void Flush()
        {
            List<ScanLog> toWrite;
            lock (_lock)
            {
                if (_buffer.Count == 0) return;
                toWrite = new List<ScanLog>(_buffer);
                _buffer.Clear();
            }

            using var tx = _conn.BeginTransaction();
            using var cmd = _conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO ScanLogs (Barcode, ScannedQuantity, Timestamp)
VALUES ($b, $q, $t);";
            cmd.Parameters.Add("$b", SqliteType.Text);
            cmd.Parameters.Add("$q", SqliteType.Integer);
            cmd.Parameters.Add("$t", SqliteType.Text);

            foreach (var log in toWrite)
            {
                cmd.Parameters["$b"].Value = log.Barcode;
                cmd.Parameters["$q"].Value = log.ScannedQuantity;
                cmd.Parameters["$t"].Value = log.Timestamp.ToString("o");
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        /// <summary>
        /// Clears any buffered logs (used on fresh import).
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _buffer.Clear();
            }
        }
    }
}
