using Microsoft.Data.Sqlite;
using MiniExcelLibs;
using ZebraSCannerTest1.Models;

namespace ZebraSCannerTest1.Services;

public class ExcelImportService
{
    private readonly SqliteConnection _conn;

    public ExcelImportService(SqliteConnection conn)
    {
        _conn = conn;
    }

    public async Task ImportExcelAsync(string filePath)
    {
        Console.WriteLine($"[DOTNET] Importing from Excel: {filePath}");
        var rows = MiniExcel.Query<ExcelProductDto>(filePath).ToList();
        Console.WriteLine($"[DOTNET] Read {rows.Count} rows from Excel");

        await _conn.OpenAsync();
        using var tx = _conn.BeginTransaction();

        // Prepared insert/update
        var insertCmd = _conn.CreateCommand();
        insertCmd.CommandText = @"
            INSERT INTO InitialProducts (Id, Barcode, Quantity)
            VALUES ($id, $barcode, $qty)
            ON CONFLICT(Barcode) DO UPDATE SET Quantity = $qty;";
        insertCmd.Parameters.Add("$id", SqliteType.Integer);
        insertCmd.Parameters.Add("$barcode", SqliteType.Text);
        insertCmd.Parameters.Add("$qty", SqliteType.Integer);

        int processed = 0;
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Barcode)) continue;

            insertCmd.Parameters["$id"].Value = row.Id;
            insertCmd.Parameters["$barcode"].Value = row.Barcode.Trim();
            insertCmd.Parameters["$qty"].Value = row.Quantity;
            await insertCmd.ExecuteNonQueryAsync();

            processed++;
            if (processed % 1000 == 0)
                Console.WriteLine($"[DOTNET] Imported {processed}/{rows.Count}...");
        }

        await tx.CommitAsync();
        Console.WriteLine($"[DOTNET] ✅ Import finished. Total rows = {processed}");
    }
}
