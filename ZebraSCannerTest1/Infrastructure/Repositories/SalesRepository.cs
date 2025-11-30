using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Dtos;
using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.Infrastructure.Repositories
{
    public class SalesRepository
    {
        private SqliteConnection _connection;

        private const string tableName = "Sales";

        public SalesRepository(SqliteConnection connection)
        {
            _connection = connection;
        }

        public async Task<SalesModel?> GetSaleAsync(string barcode)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = $@"
SELECT 
    Barcode,
    Name,
    Color,
    Size,
    SaleType,
    OldPrice,
    NewPrice,
    ArticCode,
    CreatedAt
FROM Sales 
WHERE Barcode == $b
";
            cmd.Parameters.AddWithValue("$b", barcode);

            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync() )
                return null;

            return new SalesModel
            {
                Barcode = reader.IsDBNull(0) ? null : reader.GetString(0),
                Name = reader.IsDBNull(1) ? null : reader.GetString(1),
                Color = reader.IsDBNull(2) ? null : reader.GetString(2),
                Size = reader.IsDBNull(3) ? null : reader.GetString(3),
                SaleType = reader.IsDBNull(5) ? null : reader.GetString(4),
                OldPrice = reader.IsDBNull(6) ? null : reader.GetString(5),
                NewPrice = reader.IsDBNull(7) ? null : reader.GetString(6),
                ArticCode = reader.IsDBNull(8) ? null : reader.GetString(7),
            };
        }

        public async Task UpsertSaleAsync(ExcelSalesDto dto)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO Sales
(Barcode, Name, Color, Size, SaleType, OldPrice, NewPrice, ArticCode, CreatedAt)
VALUES
($b, $n, $c, $s, $st, $op, $np, $ac, $created)
ON CONFLICT(Barcode)
DO UPDATE SET
    Name = $n,
    Color = $c,
    Size = $s,
    SaleType = $st,
    OldPrice = $op,
    NewPrice = $np,
    ArticCode = $ac,
    CreatedAt = $created;
";

            cmd.Parameters.AddWithValue("$b", dto.Barcode);
            cmd.Parameters.AddWithValue("$n", dto.Name ?? "");
            cmd.Parameters.AddWithValue("$c", dto.Color ?? "");
            cmd.Parameters.AddWithValue("$s", dto.Size ?? "");
            cmd.Parameters.AddWithValue("$st", dto.SaleType ?? "");
            cmd.Parameters.AddWithValue("$op", dto.OldPrice ?? "");
            cmd.Parameters.AddWithValue("$np", dto.NewPrice ?? "");
            cmd.Parameters.AddWithValue("$ac", dto.ArticCode ?? "");
            cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("s"));

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task ClearSalesAsync()
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Sales;";
            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine("old sales table deleted");
        }

    }
}
