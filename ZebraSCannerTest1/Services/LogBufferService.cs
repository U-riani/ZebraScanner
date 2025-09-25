using Microsoft.Data.Sqlite;
using ZebraSCannerTest1.Models;

namespace ZebraSCannerTest1.Services
{
    public class LogBufferService : IDisposable
    {
        private readonly SqliteConnection _conn;
        private readonly List<ScanLog> _buffer = new();
        private readonly object _lock = new();
        private bool _running = true;
        private readonly Task _worker;
        private readonly SqliteCommand _insertCmd;

        public LogBufferService(SqliteConnection conn)
        {
            _conn = conn;

            _insertCmd = _conn.CreateCommand();
            _insertCmd.CommandText = @"
INSERT INTO ScanLogs (Barcode, ScannedQuantity, Timestamp)
VALUES ($barcode,$qty,$ts)";
            _insertCmd.Parameters.Add("$barcode", SqliteType.Text);
            _insertCmd.Parameters.Add("$qty", SqliteType.Integer);
            _insertCmd.Parameters.Add("$ts", SqliteType.Text);

            _worker = Task.Run(FlushLoopAsync);
        }

        public void AddLog(ScanLog log)
        {
            lock (_lock) _buffer.Add(log);
        }

        public void FlushNow()
        {
            List<ScanLog> snap;
            lock (_lock)
            {
                if (_buffer.Count == 0) return;
                snap = new List<ScanLog>(_buffer);
                _buffer.Clear();
            }

            using var tx = _conn.BeginTransaction();
            _insertCmd.Transaction = tx;
            foreach (var l in snap)
            {
                _insertCmd.Parameters["$barcode"].Value = l.Barcode;
                _insertCmd.Parameters["$qty"].Value = l.ScannedQuantity;
                _insertCmd.Parameters["$ts"].Value = l.Timestamp.ToString("o");
                _insertCmd.ExecuteNonQuery();
            }
            tx.Commit();
            _insertCmd.Transaction = null;
        }

        private async Task FlushLoopAsync()
        {
            while (_running)
            {
                await Task.Delay(1500);
                FlushNow();
            }
        }

        public void Dispose()
        {
            _running = false;
            try { _worker.Wait(2000); } catch { }
            _insertCmd.Dispose();
        }
    }
}
