using Microsoft.Data.Sqlite;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.Data;

namespace ZebraSCannerTest1.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly SqliteConnection _connection;
    public SqliteConnection Connection => _connection;
    private string GetTableName(InventoryMode mode) =>
        mode == InventoryMode.Loots ? "LootsProducts" : "Products";


    public ProductRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    //public SqliteConnection GetConnection(InventoryMode mode)
    //{
    //    var dbName = mode == InventoryMode.Loots
    //        ? "zebraScanner_loots.db"
    //        : "zebraScanner_standard.db";
    //    var path = Path.Combine(FileSystem.AppDataDirectory, dbName);

    //    var isNew = !File.Exists(path);
    //    var conn = new SqliteConnection($"Data Source={path}");
    //    conn.Open();

    //    //_logger?.Info($"[DB] Using {Path.GetFileName(path)} for {mode}");

    //    if (isNew)
    //        DatabaseInitializer.Initialize(conn, mode); // ensure schema exists

    //    return conn;
    //}


    public async Task<IEnumerable<Product>> GetRecentAsync(int limit = 8, InventoryMode mode = InventoryMode.Standard)
    {
        var products = new List<Product>();
        string table = GetTableName(mode);

        bool isLoots = mode == InventoryMode.Loots;

        using var conn = DatabaseInitializer.GetConnection(mode);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT Barcode, 
                   {(isLoots ? "Box_Id," : "")}
                   InitialQuantity, 
                   ScannedQuantity, 
                   CreatedAt, 
                   UpdatedAt 
            FROM {table} 
            ORDER BY UpdatedAt DESC 
            LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", limit);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var product = new Product
            {
                Barcode = reader.GetString(0),
                InitialQuantity = reader.GetInt32(isLoots ? 2 : 1),
                ScannedQuantity = reader.GetInt32(isLoots ? 3 : 2),
                CreatedAt = DateTime.Parse(reader.GetString(isLoots ? 4 : 3)),
                UpdatedAt = DateTime.Parse(reader.GetString(isLoots ? 5 : 4))
            };

            if (isLoots)
                product.Box_Id = reader.IsDBNull(1) ? null : reader.GetString(1);

            products.Add(product);
        }
        return products;
    }

    public async Task<IEnumerable<Product>> GetByBoxAsync(string boxId, InventoryMode mode = InventoryMode.Standard)
    {
        var products = new List<Product>();
        string table = GetTableName(mode);
        bool isLoots = mode == InventoryMode.Loots;

        using var conn = DatabaseInitializer.GetConnection(mode);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = isLoots
            ? $@"SELECT Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt
              FROM {table}
              WHERE Box_Id = $box
              ORDER BY UpdatedAt DESC"
            : $@"SELECT Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt
              FROM {table}
              ORDER BY UpdatedAt DESC"; // For non-loots mode, just ignore the box

        cmd.Parameters.AddWithValue("$box", boxId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var product = new Product
            {
                Barcode = reader.GetString(0),
                InitialQuantity = reader.GetInt32(isLoots ? 2 : 1),
                ScannedQuantity = reader.GetInt32(isLoots ? 3 : 2),
                CreatedAt = DateTime.Parse(reader.GetString(isLoots ? 4 : 3)),
                UpdatedAt = DateTime.Parse(reader.GetString(isLoots ? 5 : 4))
            };

            if (isLoots)
                product.Box_Id = reader.IsDBNull(1) ? null : reader.GetString(1);

            products.Add(product);
        }

        return products;
    }


    public async Task<Product?> FindAsync(string barcode, InventoryMode mode = InventoryMode.Standard, string? boxId = null)
    {
        string table = GetTableName(mode);
        bool isLoots = mode == InventoryMode.Loots;

        using var conn = DatabaseInitializer.GetConnection(mode);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = isLoots
             ? $"SELECT Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt FROM {table} WHERE Barcode=$b AND (Box_Id=$box OR $box IS NULL)"
             : $"SELECT Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt FROM {table} WHERE Barcode=$b";

        cmd.Parameters.AddWithValue("$b", barcode);

        if (isLoots)
            cmd.Parameters.AddWithValue("$box", boxId ?? (object)DBNull.Value);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var product = new Product
            {
                Barcode = reader.GetString(0),
                InitialQuantity = Convert.ToInt32(reader.GetValue(isLoots ? 2 : 1)),
                ScannedQuantity = Convert.ToInt32(reader.GetValue(isLoots ? 3 : 2)),
                CreatedAt = DateTime.Parse(reader.GetString(isLoots ? 4 : 3)),
                UpdatedAt = DateTime.Parse(reader.GetString(isLoots ? 5 : 4))
            };


            if (isLoots)
                product.Box_Id = reader.IsDBNull(1) ? null : reader.GetString(1);

            return product;
        }
        return null;
    }

    public async Task AddAsync(Product product, InventoryMode mode = InventoryMode.Standard)
    {
        string table = GetTableName(mode);
        bool isLoots = mode == InventoryMode.Loots;
        using var conn = DatabaseInitializer.GetConnection(mode);

        using var cmd = conn.CreateCommand();

        cmd.CommandText = isLoots
            ? $@"
                INSERT INTO {table} 
                (Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt)
                VALUES ($b, $box, $i, $s, $c, $u)"
            : $@"
                INSERT INTO {table} 
                (Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt)
                VALUES ($b, $i, $s, $c, $u)";

        cmd.Parameters.AddWithValue("$b", product.Barcode);
        if (isLoots)
            cmd.Parameters.AddWithValue("$box", product.Box_Id is null ? DBNull.Value : product.Box_Id);
        cmd.Parameters.AddWithValue("$i", product.InitialQuantity);
        cmd.Parameters.AddWithValue("$s", product.ScannedQuantity);
        cmd.Parameters.AddWithValue("$c", product.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$u", product.UpdatedAt.ToString("o"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateAsync(Product product, InventoryMode mode = InventoryMode.Standard)
    {
        string table = GetTableName(mode);
        bool isLoots = mode == InventoryMode.Loots;
        using var conn = DatabaseInitializer.GetConnection(mode);

        using var cmd = conn.CreateCommand();

        cmd.CommandText = isLoots
            ? $@"
                UPDATE {table}
                SET ScannedQuantity=$s, UpdatedAt=$u
                WHERE Barcode=$b AND (Box_Id=$box OR $box IS NULL)"
            : $@"
                UPDATE {table}
                SET ScannedQuantity=$s, UpdatedAt=$u
                WHERE Barcode=$b";

        cmd.Parameters.AddWithValue("$s", product.ScannedQuantity);
        cmd.Parameters.AddWithValue("$u", product.UpdatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$b", product.Barcode);

        if (isLoots)
            cmd.Parameters.AddWithValue("$box", product.Box_Id is null ? DBNull.Value : product.Box_Id);

        await cmd.ExecuteNonQueryAsync();
    }

    public (int TotalInitial, int TotalScanned, int TotalBarcodes, int ScannedBarcodes) GetInventoryStats(InventoryMode mode = InventoryMode.Standard)
    {
        string table = GetTableName(mode);
        using var conn = DatabaseInitializer.GetConnection(mode);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT 
                SUM(InitialQuantity), 
                SUM(ScannedQuantity),
                COUNT(*) AS TotalBarcodes,
                SUM(CASE WHEN ScannedQuantity > 0 THEN 1 ELSE 0 END) AS ScannedBarcodes
            FROM {table};";

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
    

}