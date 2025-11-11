using Microsoft.Data.Sqlite;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.Core.Enums;

namespace ZebraSCannerTest1.Infrastructure.Repositories
{
    public class LootsProductRepository : ILootsProductRepository
    {
        private SqliteConnection GetConnection()
        {
            var conn = DatabaseInitializer.GetConnection(InventoryMode.Loots);
            return conn;
        }

        public async Task AddAsync(LootProduct p)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO LootsProducts
                (Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt, Name, Color, Size, Price, ArticCode, Box_Id)
                VALUES ($b, $i, $s, $c, $u, $n, $col, $sz, $p, $a, $box);";

            cmd.Parameters.AddWithValue("$b", p.Barcode);
            cmd.Parameters.AddWithValue("$i", p.InitialQuantity);
            cmd.Parameters.AddWithValue("$s", p.ScannedQuantity);
            cmd.Parameters.AddWithValue("$c", p.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("$u", p.UpdatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("$n", p.Name ?? "");
            cmd.Parameters.AddWithValue("$col", p.Color ?? "");
            cmd.Parameters.AddWithValue("$sz", p.Size ?? "");
            cmd.Parameters.AddWithValue("$p", p.Price ?? "");
            cmd.Parameters.AddWithValue("$a", p.ArticCode ?? "");
            cmd.Parameters.AddWithValue("$box", string.IsNullOrWhiteSpace(p.Box_Id) ? DBNull.Value : p.Box_Id);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateAsync(Product product)
        {

            using var conn = DatabaseInitializer.GetConnection(InventoryMode.Loots);

            using var cmd = conn.CreateCommand();

            cmd.CommandText = $@"
                UPDATE LootsProducts
                SET ScannedQuantity=$s, UpdatedAt=$u
                WHERE Barcode=$b AND (Box_Id=$box OR $box IS NULL)";
           

            cmd.Parameters.AddWithValue("$s", product.ScannedQuantity);
            cmd.Parameters.AddWithValue("$u", product.UpdatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("$b", product.Barcode);

            
                cmd.Parameters.AddWithValue("$box", product.Box_Id is null ? DBNull.Value : product.Box_Id);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<LootProduct>> GetAllAsync()
        {
            var list = new List<LootProduct>();
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM LootsProducts";

            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                try
                {
                    // Defensive index resolution (in case table order changes)
                    int id = SafeGetOrdinal(r, "Id");
                    int barcode = SafeGetOrdinal(r, "Barcode");
                    int boxId = SafeGetOrdinal(r, "Box_Id");
                    int initial = SafeGetOrdinal(r, "InitialQuantity");
                    int scanned = SafeGetOrdinal(r, "ScannedQuantity");
                    int created = SafeGetOrdinal(r, "CreatedAt");
                    int updated = SafeGetOrdinal(r, "UpdatedAt");
                    int name = SafeGetOrdinal(r, "Name");
                    int color = SafeGetOrdinal(r, "Color");
                    int size = SafeGetOrdinal(r, "Size");
                    int price = SafeGetOrdinal(r, "Price");
                    int artic = SafeGetOrdinal(r, "ArticCode");

                    list.Add(new LootProduct
                    {
                        Id = SafeReadInt(r, id),
                        Barcode = SafeReadString(r, barcode),
                        Box_Id = SafeReadString(r, boxId),
                        InitialQuantity = SafeReadInt(r, initial),
                        ScannedQuantity = SafeReadInt(r, scanned),
                        CreatedAt = SafeReadDate(r, created),
                        UpdatedAt = SafeReadDate(r, updated),
                        Name = SafeReadString(r, name),
                        Color = SafeReadString(r, color),
                        Size = SafeReadString(r, size),
                        Price = SafeReadString(r, price),
                        ArticCode = SafeReadString(r, artic)
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ LootsProduct row skipped due to mismatch: {ex.Message}");
                    continue;
                }
            }

            return list;
        }

        public async Task<Product?> FindAsync(string barcode, string boxId)
        {

            using var conn = DatabaseInitializer.GetConnection(InventoryMode.Loots);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt FROM LootsProducts WHERE Barcode=$b AND (Box_Id=$box OR $box IS NULL)";

            cmd.Parameters.AddWithValue("$b", barcode);

            cmd.Parameters.AddWithValue("$box", boxId ?? (object)DBNull.Value);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var product = new Product
                {
                    Barcode = reader.GetString(0),
                    InitialQuantity = Convert.ToInt32(reader.GetValue(2)),
                    ScannedQuantity = Convert.ToInt32(reader.GetValue(3)),
                    CreatedAt = DateTime.Parse(reader.GetString(4)),
                    UpdatedAt = DateTime.Parse(reader.GetString(5))
                };
                product.Box_Id = reader.IsDBNull(1) ? null : reader.GetString(1);

                return product;
            }
            return null;
        }

        public async Task<IEnumerable<Product>> GetByBoxAsync(string boxId)
        {
            var products = new List<Product>();

            using var conn = DatabaseInitializer.GetConnection(InventoryMode.Loots);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"SELECT Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt
              FROM LootsProducts
              WHERE Box_Id = $box
              ORDER BY UpdatedAt DESC"; 

            cmd.Parameters.AddWithValue("$box", boxId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var product = new Product
                {
                    Barcode = reader.GetString(0),
                    InitialQuantity = reader.GetInt32( 2 ),
                    ScannedQuantity = reader.GetInt32( 3),
                    CreatedAt = DateTime.Parse(reader.GetString( 4)),
                    UpdatedAt = DateTime.Parse(reader.GetString( 5 ))
                };


                product.Box_Id = reader.IsDBNull(1) ? null : reader.GetString(1);

                products.Add(product);
            }

            return products;
        }

        public (int TotalInitial, int TotalScanned, int TotalBarcodes, int ScannedBarcodes) GetInventoryStats(InventoryMode mode = InventoryMode.Loots)
        {
            using var conn = DatabaseInitializer.GetConnection(mode);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
        SELECT 
            COALESCE(SUM(InitialQuantity), 0),
            COALESCE(SUM(ScannedQuantity), 0),
            COUNT(*) AS TotalBarcodes,
            SUM(CASE WHEN ScannedQuantity > 0 THEN 1 ELSE 0 END) AS ScannedBarcodes
        FROM LootsProducts;";

            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                return (
                    reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
                );
            }

            return (0, 0, 0, 0);
        }

        public async Task ClearAsync()
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM LootsProducts;";
            await cmd.ExecuteNonQueryAsync();
        }

        // --- Safety helpers ---
        private static int SafeGetOrdinal(SqliteDataReader r, string name)
        {
            try { return r.GetOrdinal(name); }
            catch { return -1; }
        }

        private static string SafeReadString(SqliteDataReader r, int index)
        {
            return index >= 0 && !r.IsDBNull(index) ? r.GetString(index) : string.Empty;
        }

        private static int SafeReadInt(SqliteDataReader r, int index)
        {
            return index >= 0 && !r.IsDBNull(index) ? Convert.ToInt32(r.GetValue(index)) : 0;
        }

        private static DateTime SafeReadDate(SqliteDataReader r, int index)
        {
            if (index < 0 || r.IsDBNull(index))
                return DateTime.MinValue;
            if (DateTime.TryParse(r.GetString(index), out var dt))
                return dt;
            return DateTime.MinValue;
        }
    }
}
