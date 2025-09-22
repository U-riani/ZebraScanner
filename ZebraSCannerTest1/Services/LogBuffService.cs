using Microsoft.Data.Sqlite;
using ZebraSCannerTest1.Models;

namespace ZebraSCannerTest1.Services;

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

        // Prepare insert statement ONCE
        _insertCmd = _conn.CreateCommand();
        _insertCmd.CommandText = @"
            INSERT INTO ScanLogs (Barcode, Quantity, InitialQuantity, Timestamp)
            VALUES ($barcode,$qty,$init,$ts)";
        _insertCmd.Parameters.Add("$barcode", SqliteType.Text);
        _insertCmd.Parameters.Add("$qty", SqliteType.Integer);
        _insertCmd.Parameters.Add("$init", SqliteType.Integer);
        _insertCmd.Parameters.Add("$ts", SqliteType.Text);

        _worker = Task.Run(FlushLoopAsync);
    }

    public void AddLog(ScanLog log)
    {
        lock (_lock) _buffer.Add(log);
    }

    private async Task FlushLoopAsync()
    {
        while (_running)
        {
            await Task.Delay(1500); // flush every 1.5s

            List<ScanLog> snapshot;
            lock (_lock)
            {
                if (_buffer.Count == 0) continue;
                snapshot = new List<ScanLog>(_buffer);
                _buffer.Clear();
            }

            using var tx = _conn.BeginTransaction();
            foreach (var log in snapshot)
            {
                _insertCmd.Parameters["$barcode"].Value = log.Barcode;
                _insertCmd.Parameters["$qty"].Value = log.Quantity;
                _insertCmd.Parameters["$init"].Value = log.InitialQuantity;
                _insertCmd.Parameters["$ts"].Value = log.Timestamp.ToString("o");
                _insertCmd.ExecuteNonQuery();
            }
            tx.Commit();

            Console.WriteLine($"[DOTNET] Flushed {snapshot.Count} logs");
        }
    }

    public void Dispose()
    {
        _running = false;
        _worker.Wait();
        _insertCmd.Dispose();
    }
}
