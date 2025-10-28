using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.Infrastructure.Repositories
{
    public class LootsProductRepository : ILootsProductRepository
    {
        private readonly SqliteConnection _connection;

        public LootsProductRepository(SqliteConnection connection)
        {
            _connection = connection;
        }

        public async Task AddAsync(LootProduct p)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
            INSERT INTO LootsProducts
            (Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt, Name, Color, Size, Price, ArticCode)
            VALUES ($b, $box, $i, $s, $c, $u, $n, $col, $sz, $p, $a);";

            cmd.Parameters.AddWithValue("$b", p.Barcode);
            cmd.Parameters.AddWithValue("$box", p.Box_Id is null ? DBNull.Value : p.Box_Id);
            cmd.Parameters.AddWithValue("$i", p.InitialQuantity);
            cmd.Parameters.AddWithValue("$s", p.ScannedQuantity);
            cmd.Parameters.AddWithValue("$c", p.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("$u", p.UpdatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("$n", p.Name ?? "");
            cmd.Parameters.AddWithValue("$col", p.Color ?? "");
            cmd.Parameters.AddWithValue("$sz", p.Size ?? "");
            cmd.Parameters.AddWithValue("$p", p.Price ?? "");
            cmd.Parameters.AddWithValue("$a", p.ArticCode ?? "");

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<LootProduct>> GetAllAsync()
        {
            var list = new List<LootProduct>();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM LootsProducts";
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new LootProduct
                {
                    Id = r.GetInt32(0),
                    Barcode = r.GetString(1),
                    Box_Id = r.IsDBNull(2) ? null : r.GetString(2),
                    InitialQuantity = r.GetInt32(3),
                    ScannedQuantity = r.GetInt32(4),
                    CreatedAt = DateTime.Parse(r.GetString(5)),
                    UpdatedAt = DateTime.Parse(r.GetString(6)),
                    Name = r.IsDBNull(7) ? null : r.GetString(7),
                    Color = r.IsDBNull(8) ? null : r.GetString(8),
                    Size = r.IsDBNull(9) ? null : r.GetString(9),
                    Price = r.IsDBNull(10) ? null : r.GetString(10),
                    ArticCode = r.IsDBNull(11) ? null : r.GetString(11)
                });
            }
            return list;
        }

        public async Task ClearAsync()
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM LootsProducts;";
            await cmd.ExecuteNonQueryAsync();
        }
    }

}
