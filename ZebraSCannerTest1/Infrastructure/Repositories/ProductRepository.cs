using Microsoft.Data.Sqlite;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly SqliteConnection _connection;
    public SqliteConnection Connection => _connection;


    public ProductRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public async Task<IEnumerable<Product>> GetRecentAsync(int limit = 8)
    {
        var products = new List<Product>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt FROM Products ORDER BY UpdatedAt DESC LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", limit);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            products.Add(new Product
            {
                Barcode = reader.GetString(0),
                InitialQuantity = reader.GetInt32(1),
                ScannedQuantity = reader.GetInt32(2),
                CreatedAt = DateTime.Parse(reader.GetString(3)),
                UpdatedAt = DateTime.Parse(reader.GetString(4))
            });
        }
        return products;
    }

    public async Task<Product?> FindAsync(string barcode)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt FROM Products WHERE Barcode=$b";
        cmd.Parameters.AddWithValue("$b", barcode);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Product
            {
                Barcode = reader.GetString(0),
                InitialQuantity = reader.GetInt32(1),
                ScannedQuantity = reader.GetInt32(2),
                CreatedAt = DateTime.Parse(reader.GetString(3)),
                UpdatedAt = DateTime.Parse(reader.GetString(4))
            };
        }
        return null;
    }

    public async Task AddAsync(Product product)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Products (Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt)
            VALUES ($b, $i, $s, $c, $u)";
        cmd.Parameters.AddWithValue("$b", product.Barcode);
        cmd.Parameters.AddWithValue("$i", product.InitialQuantity);
        cmd.Parameters.AddWithValue("$s", product.ScannedQuantity);
        cmd.Parameters.AddWithValue("$c", product.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$u", product.UpdatedAt.ToString("o"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateAsync(Product product)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE Products 
            SET ScannedQuantity=$s, UpdatedAt=$u
            WHERE Barcode=$b";
        cmd.Parameters.AddWithValue("$s", product.ScannedQuantity);
        cmd.Parameters.AddWithValue("$u", product.UpdatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$b", product.Barcode);
        await cmd.ExecuteNonQueryAsync();
    }

    public (int TotalInitial, int TotalScanned, int TotalBarcodes, int ScannedBarcodes) GetInventoryStats()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
        SELECT 
            SUM(InitialQuantity), 
            SUM(ScannedQuantity),
            COUNT(*) AS TotalBarcodes,
            SUM(CASE WHEN ScannedQuantity > 0 THEN 1 ELSE 0 END) AS ScannedBarcodes
        FROM Products;";

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
