using Microsoft.Data.Sqlite;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.Infrastructure.Repositories;

public class ScanLogRepository : IScanLogRepository
{
    private readonly SqliteConnection _connection;

    public ScanLogRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public async Task InsertAsync(ScanLog log)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO ScanLogs (Barcode, Was, IncrementBy, IsValue, UpdatedAt, Section)
            VALUES ($b, $w, $i, $v, $u, $s)";
        cmd.Parameters.AddWithValue("$b", log.Barcode);
        cmd.Parameters.AddWithValue("$w", log.Was);
        cmd.Parameters.AddWithValue("$i", log.IncrementBy);
        cmd.Parameters.AddWithValue("$v", log.IsValue);
        cmd.Parameters.AddWithValue("$u", log.UpdatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$s", log.Section ?? (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<ScanLog>> GetByBarcodeAsync(string barcode)
    {
        var logs = new List<ScanLog>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Barcode, Was, IncrementBy, IsValue, UpdatedAt, Section FROM ScanLogs WHERE Barcode=$b ORDER BY UpdatedAt DESC";
        cmd.Parameters.AddWithValue("$b", barcode);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            logs.Add(new ScanLog
            {
                Barcode = reader.GetString(0),
                Was = reader.GetInt32(1),
                IncrementBy = reader.GetInt32(2),
                IsValue = reader.GetInt32(3),
                UpdatedAt = DateTime.Parse(reader.GetString(4)),
                Section = reader.IsDBNull(5) ? null : reader.GetString(5)
            });
        }
        return logs;
    }
}
