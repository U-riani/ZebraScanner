using Microsoft.Data.Sqlite;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly IDbFactory _db;

    public ProductRepository(IDbFactory db)
    {
        _db = db;
    }

    private string GetTableName(InventoryMode mode) =>
        mode == InventoryMode.Loots ? "LootsProducts" : "Products";

    private static string? NormalizeBoxId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    private static object ToDbTextOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static Product ReadProduct(SqliteDataReader reader, bool isLoots, string? overrideBoxId = null, bool zeroInitialForDynamicBox = false)
    {
        var product = new Product
        {
            Barcode = reader.GetString(0),
            InitialQuantity = zeroInitialForDynamicBox ? 0 : Convert.ToInt32(reader.GetValue(isLoots ? 2 : 1)),
            ScannedQuantity = Convert.ToInt32(reader.GetValue(isLoots ? 3 : 2)),
            CreatedAt = DateTime.Parse(reader.GetString(isLoots ? 4 : 3)),
            UpdatedAt = DateTime.Parse(reader.GetString(isLoots ? 5 : 4))
        };

        if (isLoots)
        {
            var storedBoxId = reader.IsDBNull(1) ? null : reader.GetString(1);
            product.Box_Id = overrideBoxId ?? storedBoxId;
        }

        var nameIndex = isLoots ? 6 : 5;
        if (reader.FieldCount > nameIndex)
        {
            product.Name = reader.IsDBNull(nameIndex) ? null : reader.GetString(nameIndex);
            product.Color = reader.IsDBNull(nameIndex + 1) ? null : reader.GetString(nameIndex + 1);
            product.Size = reader.IsDBNull(nameIndex + 2) ? null : reader.GetString(nameIndex + 2);
            product.Price = reader.IsDBNull(nameIndex + 3) ? null : reader.GetString(nameIndex + 3);
            product.ArticCode = reader.IsDBNull(nameIndex + 4) ? null : reader.GetString(nameIndex + 4);
        }

        return product;
    }

    public async Task<IEnumerable<Product>> GetRecentAsync(int limit = 8, InventoryMode mode = InventoryMode.Standard)
    {
        var products = new List<Product>();
        string table = GetTableName(mode);
        bool isLoots = mode == InventoryMode.Loots;

        using var conn = await _db.Inventorization(mode);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = isLoots
            ? $@"
                SELECT Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
                       Name, Color, Size, Price, ArticCode
                FROM {table}
                WHERE ScannedQuantity > 0 OR Box_Id IS NOT NULL AND TRIM(Box_Id) <> ''
                ORDER BY UpdatedAt DESC
                LIMIT $limit"
            : $@"
                SELECT Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
                       Name, Color, Size, Price, ArticCode
                FROM {table}
                ORDER BY UpdatedAt DESC
                LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", limit);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            products.Add(ReadProduct(reader, isLoots));

        return products;
    }

    public async Task<IEnumerable<Product>> GetByBoxAsync(string boxId, InventoryMode mode = InventoryMode.Standard)
    {
        var products = new List<Product>();
        string table = GetTableName(mode);
        bool isLoots = mode == InventoryMode.Loots;
        var normalizedBoxId = NormalizeBoxId(boxId);

        using var conn = await _db.Inventorization(mode);
        using var cmd = conn.CreateCommand();

        if (isLoots)
        {
            if (normalizedBoxId is null)
                return products;

            cmd.CommandText = $@"
                SELECT Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
                       Name, Color, Size, Price, ArticCode
                FROM {table}
                WHERE Box_Id = $box
                ORDER BY UpdatedAt DESC";
            cmd.Parameters.AddWithValue("$box", normalizedBoxId);
        }
        else
        {
            cmd.CommandText = $@"
                SELECT Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
                       Name, Color, Size, Price, ArticCode
                FROM {table}
                ORDER BY UpdatedAt DESC";
        }

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            products.Add(ReadProduct(reader, isLoots));

        return products;
    }

    public async Task<Product?> FindAsync(string barcode, InventoryMode mode = InventoryMode.Standard, string? boxId = null)
    {
        string table = GetTableName(mode);
        bool isLoots = mode == InventoryMode.Loots;
        var normalizedBoxId = NormalizeBoxId(boxId);

        using var conn = await _db.Inventorization(mode);

        if (isLoots)
        {
            if (normalizedBoxId is not null)
            {
                using var exact = conn.CreateCommand();
                exact.CommandText = $@"
                    SELECT Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
                           Name, Color, Size, Price, ArticCode
                    FROM {table}
                    WHERE Barcode = $b AND Box_Id = $box
                    ORDER BY UpdatedAt DESC
                    LIMIT 1";
                exact.Parameters.AddWithValue("$b", barcode);
                exact.Parameters.AddWithValue("$box", normalizedBoxId);

                using var exactReader = await exact.ExecuteReaderAsync();
                if (await exactReader.ReadAsync())
                    return ReadProduct(exactReader, isLoots);
            }

            // Dynamic transfer loots use product-level rows without Box_Id.
            // When a sender scans a box first, use the no-box row as a template and create/update
            // a box-specific local row during UpdateAsync().
            using var template = conn.CreateCommand();
            template.CommandText = $@"
                SELECT Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
                       Name, Color, Size, Price, ArticCode
                FROM {table}
                WHERE Barcode = $b AND (Box_Id IS NULL OR TRIM(Box_Id) = '')
                ORDER BY UpdatedAt DESC
                LIMIT 1";
            template.Parameters.AddWithValue("$b", barcode);

            using var templateReader = await template.ExecuteReaderAsync();
            if (await templateReader.ReadAsync())
                return ReadProduct(
                    templateReader,
                    isLoots,
                    overrideBoxId: normalizedBoxId,
                    zeroInitialForDynamicBox: normalizedBoxId is not null);

            // If another worker already scanned this barcode into a different dynamic box,
            // the backend may send only that boxed row back to Pocket. Use it as product
            // metadata and let UpdateAsync create the current box-specific row.
            if (normalizedBoxId is not null)
            {
                using var anySameBarcode = conn.CreateCommand();
                anySameBarcode.CommandText = $@"
                    SELECT Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
                           Name, Color, Size, Price, ArticCode
                    FROM {table}
                    WHERE Barcode = $b
                    ORDER BY UpdatedAt DESC
                    LIMIT 1";
                anySameBarcode.Parameters.AddWithValue("$b", barcode);

                using var anyReader = await anySameBarcode.ExecuteReaderAsync();
                if (await anyReader.ReadAsync())
                    return ReadProduct(
                        anyReader,
                        isLoots,
                        overrideBoxId: normalizedBoxId,
                        zeroInitialForDynamicBox: true);
            }

            return null;
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
                   Name, Color, Size, Price, ArticCode
            FROM {table}
            WHERE Barcode = $b
            LIMIT 1";
        cmd.Parameters.AddWithValue("$b", barcode);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return ReadProduct(reader, isLoots);

        return null;
    }

    public async Task AddAsync(Product product, InventoryMode mode = InventoryMode.Standard)
    {
        string table = GetTableName(mode);
        bool isLoots = mode == InventoryMode.Loots;
        using var conn = await _db.Inventorization(mode);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = isLoots
            ? $@"
                INSERT INTO {table}
                (Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt, Name, Color, Size, Price, ArticCode)
                VALUES ($b, $box, $i, $s, $c, $u, $n, $col, $sz, $p, $a)"
            : $@"
                INSERT INTO {table}
                (Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt, Name, Color, Size, Price, ArticCode)
                VALUES ($b, $i, $s, $c, $u, $n, $col, $sz, $p, $a)";

        cmd.Parameters.AddWithValue("$b", product.Barcode);
        if (isLoots)
            cmd.Parameters.AddWithValue("$box", ToDbTextOrNull(product.Box_Id));
        cmd.Parameters.AddWithValue("$i", product.InitialQuantity);
        cmd.Parameters.AddWithValue("$s", product.ScannedQuantity);
        cmd.Parameters.AddWithValue("$c", product.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$u", product.UpdatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$n", product.Name ?? "");
        cmd.Parameters.AddWithValue("$col", product.Color ?? "");
        cmd.Parameters.AddWithValue("$sz", product.Size ?? "");
        cmd.Parameters.AddWithValue("$p", product.Price ?? "");
        cmd.Parameters.AddWithValue("$a", product.ArticCode ?? "");

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateAsync(Product product, InventoryMode mode = InventoryMode.Standard)
    {
        string table = GetTableName(mode);
        bool isLoots = mode == InventoryMode.Loots;
        using var conn = await _db.Inventorization(mode);

        if (isLoots)
        {
            var normalizedBoxId = NormalizeBoxId(product.Box_Id);
            using var update = conn.CreateCommand();

            update.CommandText = normalizedBoxId is null
                ? $@"
                    UPDATE {table}
                    SET ScannedQuantity = $s, UpdatedAt = $u
                    WHERE Barcode = $b AND (Box_Id IS NULL OR TRIM(Box_Id) = '')"
                : $@"
                    UPDATE {table}
                    SET ScannedQuantity = $s, UpdatedAt = $u
                    WHERE Barcode = $b AND Box_Id = $box";

            update.Parameters.AddWithValue("$s", product.ScannedQuantity);
            update.Parameters.AddWithValue("$u", product.UpdatedAt.ToString("o"));
            update.Parameters.AddWithValue("$b", product.Barcode);
            if (normalizedBoxId is not null)
                update.Parameters.AddWithValue("$box", normalizedBoxId);

            var updatedRows = await update.ExecuteNonQueryAsync();
            if (updatedRows > 0)
                return;

            // First scan for this dynamic transfer box: create a box-specific row.
            await AddAsync(new Product
            {
                Barcode = product.Barcode,
                Box_Id = normalizedBoxId,
                InitialQuantity = product.InitialQuantity,
                ScannedQuantity = product.ScannedQuantity,
                CreatedAt = product.CreatedAt == default ? DateTime.UtcNow : product.CreatedAt,
                UpdatedAt = product.UpdatedAt == default ? DateTime.UtcNow : product.UpdatedAt,
                Name = product.Name,
                Color = product.Color,
                Size = product.Size,
                Price = product.Price,
                ArticCode = product.ArticCode
            }, mode);

            return;
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            UPDATE {table}
            SET ScannedQuantity = $s, UpdatedAt = $u
            WHERE Barcode = $b";

        cmd.Parameters.AddWithValue("$s", product.ScannedQuantity);
        cmd.Parameters.AddWithValue("$u", product.UpdatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$b", product.Barcode);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<(int TotalInitial, int TotalScanned, int TotalBarcodes, int ScannedBarcodes)> GetInventoryStats(InventoryMode mode = InventoryMode.Standard)
    {
        string table = GetTableName(mode);
        using var conn = await _db.Inventorization(mode);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = mode == InventoryMode.Loots
            ? $@"
                SELECT
                    COALESCE(SUM(CASE WHEN Box_Id IS NULL OR TRIM(Box_Id) = '' THEN InitialQuantity ELSE 0 END), 0),
                    COALESCE(SUM(ScannedQuantity), 0),
                    COUNT(*) AS TotalBarcodes,
                    COALESCE(SUM(CASE WHEN ScannedQuantity > 0 THEN 1 ELSE 0 END), 0) AS ScannedBarcodes
                FROM {table};"
            : $@"
                SELECT
                    COALESCE(SUM(InitialQuantity), 0),
                    COALESCE(SUM(ScannedQuantity), 0),
                    COUNT(*) AS TotalBarcodes,
                    COALESCE(SUM(CASE WHEN ScannedQuantity > 0 THEN 1 ELSE 0 END), 0) AS ScannedBarcodes
                FROM {table};";

        using var reader = cmd.ExecuteReader();

        if (reader.Read())
        {
            return (
                reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0)),
                reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1)),
                reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2)),
                reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3))
            );
        }

        return (0, 0, 0, 0);
    }

    public async Task<IEnumerable<Product>> GetProductsForUploadAsync(InventoryMode mode = InventoryMode.Standard)
    {
        var products = new List<Product>();
        string table = GetTableName(mode);
        bool isLoots = mode == InventoryMode.Loots;

        using var conn = await _db.Inventorization(mode);
        using var cmd = conn.CreateCommand();

        cmd.CommandText = isLoots
            ? $@"
                SELECT Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
                       Name, Color, Size, Price, ArticCode
                FROM {table}
                WHERE ScannedQuantity > 0
                  AND Box_Id IS NOT NULL
                  AND TRIM(Box_Id) <> ''
                ORDER BY UpdatedAt DESC"
            : $@"
                SELECT Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
                       Name, Color, Size, Price, ArticCode
                FROM {table}
                WHERE ScannedQuantity > 0
                ORDER BY UpdatedAt DESC";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            products.Add(ReadProduct(reader, isLoots));

        return products;
    }
}
