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


    private sealed class LootScanTemplate
    {
        public long? Id { get; init; }
        public string Barcode { get; init; } = string.Empty;
        public string? BoxId { get; init; }
        public int InitialQuantity { get; init; }
        public int ScannedQuantity { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
        public string? Name { get; init; }
        public string? Color { get; init; }
        public string? Size { get; init; }
        public string? Price { get; init; }
        public string? ArticCode { get; init; }
    }

    private static LootScanTemplate ReadLootScanTemplate(SqliteDataReader reader, bool hasId)
    {
        var offset = hasId ? 1 : 0;

        return new LootScanTemplate
        {
            Id = hasId ? reader.GetInt64(0) : null,
            Barcode = reader.GetString(offset),
            BoxId = reader.IsDBNull(offset + 1) ? null : reader.GetString(offset + 1),
            InitialQuantity = Convert.ToInt32(reader.GetValue(offset + 2)),
            ScannedQuantity = Convert.ToInt32(reader.GetValue(offset + 3)),
            CreatedAt = DateTime.Parse(reader.GetString(offset + 4)),
            UpdatedAt = DateTime.Parse(reader.GetString(offset + 5)),
            Name = reader.IsDBNull(offset + 6) ? null : reader.GetString(offset + 6),
            Color = reader.IsDBNull(offset + 7) ? null : reader.GetString(offset + 7),
            Size = reader.IsDBNull(offset + 8) ? null : reader.GetString(offset + 8),
            Price = reader.IsDBNull(offset + 9) ? null : reader.GetString(offset + 9),
            ArticCode = reader.IsDBNull(offset + 10) ? null : reader.GetString(offset + 10)
        };
    }

    private static Product ToProduct(LootScanTemplate row, string? overrideBoxId = null, int? overrideInitial = null, int? overrideScanned = null, DateTime? overrideUpdatedAt = null)
    {
        return new Product
        {
            Barcode = row.Barcode,
            Box_Id = overrideBoxId ?? row.BoxId,
            InitialQuantity = overrideInitial ?? row.InitialQuantity,
            ScannedQuantity = overrideScanned ?? row.ScannedQuantity,
            CreatedAt = row.CreatedAt,
            UpdatedAt = overrideUpdatedAt ?? row.UpdatedAt,
            Name = row.Name,
            Color = row.Color,
            Size = row.Size,
            Price = row.Price,
            ArticCode = row.ArticCode
        };
    }

    private static void AddLogParameters(SqliteCommand cmd, string barcode, int was, int newValue, DateTime updatedAt, InventoryMode mode, string? section, string? boxId)
    {
        cmd.Parameters.AddWithValue("$b", barcode);
        cmd.Parameters.AddWithValue("$was", was);
        cmd.Parameters.AddWithValue("$inc", 1);
        cmd.Parameters.AddWithValue("$val", newValue);
        cmd.Parameters.AddWithValue("$u", updatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$manual", DBNull.Value);

        if (mode == InventoryMode.Loots)
            cmd.Parameters.AddWithValue("$box", ToDbTextOrNull(boxId));
        else
            cmd.Parameters.AddWithValue("$section", ToDbTextOrNull(section));
    }

    private static async Task InsertScanLogAsync(SqliteConnection conn, SqliteTransaction tx, string barcode, int was, int newValue, DateTime updatedAt, InventoryMode mode, string? section, string? boxId)
    {
        using var log = conn.CreateCommand();
        log.Transaction = tx;
        log.CommandText = mode == InventoryMode.Loots
            ? @"
                INSERT INTO LootsScanLogs
                (Barcode, Box_Id, Was, IncrementBy, IsValue, UpdatedAt, IsManual, Section)
                VALUES ($b, $box, $was, $inc, $val, $u, $manual, NULL)"
            : @"
                INSERT INTO ScanLogs
                (Barcode, Was, IncrementBy, IsValue, UpdatedAt, IsManual, Section)
                VALUES ($b, $was, $inc, $val, $u, $manual, $section)";

        AddLogParameters(log, barcode, was, newValue, updatedAt, mode, section, boxId);
        await log.ExecuteNonQueryAsync();
    }

    private static void AddProductMetadataParameters(SqliteCommand cmd, Product product)
    {
        cmd.Parameters.AddWithValue("$n", product.Name ?? "");
        cmd.Parameters.AddWithValue("$col", product.Color ?? "");
        cmd.Parameters.AddWithValue("$sz", product.Size ?? "");
        cmd.Parameters.AddWithValue("$p", product.Price ?? "");
        cmd.Parameters.AddWithValue("$a", product.ArticCode ?? "");
    }

    public async Task<Product?> IncrementScanAsync(
        string barcode,
        InventoryMode mode = InventoryMode.Standard,
        string? boxId = null,
        string? section = null,
        bool createIfMissing = false)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return null;

        barcode = barcode.Trim();
        var normalizedBoxId = NormalizeBoxId(boxId);
        var now = DateTime.UtcNow;

        using var conn = await _db.Inventorization(mode);
        using var tx = conn.BeginTransaction();

        try
        {
            if (mode == InventoryMode.Loots)
            {
                var scannedProduct = await IncrementLootScanAsync(conn, tx, barcode, normalizedBoxId, now, section, createIfMissing);
                tx.Commit();
                return scannedProduct;
            }

            var standardProduct = await IncrementStandardScanAsync(conn, tx, barcode, now, section, createIfMissing);
            tx.Commit();
            return standardProduct;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private async Task<Product?> IncrementStandardScanAsync(
        SqliteConnection conn,
        SqliteTransaction tx,
        string barcode,
        DateTime now,
        string? section,
        bool createIfMissing)
    {
        using (var update = conn.CreateCommand())
        {
            update.Transaction = tx;
            update.CommandText = @"
                UPDATE Products
                SET ScannedQuantity = ScannedQuantity + 1,
                    UpdatedAt = $u
                WHERE Barcode = $b";
            update.Parameters.AddWithValue("$u", now.ToString("o"));
            update.Parameters.AddWithValue("$b", barcode);

            var updatedRows = await update.ExecuteNonQueryAsync();
            if (updatedRows > 0)
            {
                using var select = conn.CreateCommand();
                select.Transaction = tx;
                select.CommandText = @"
                    SELECT Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
                           Name, Color, Size, Price, ArticCode
                    FROM Products
                    WHERE Barcode = $b
                    LIMIT 1";
                select.Parameters.AddWithValue("$b", barcode);

                using var reader = await select.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return null;

                var product = ReadProduct(reader, isLoots: false);
                await InsertScanLogAsync(conn, tx, product.Barcode, product.ScannedQuantity - 1, product.ScannedQuantity, now, InventoryMode.Standard, section, null);
                return product;
            }
        }

        if (!createIfMissing)
            return null;

        var created = new Product
        {
            Barcode = barcode,
            InitialQuantity = 0,
            ScannedQuantity = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        using (var insert = conn.CreateCommand())
        {
            insert.Transaction = tx;
            insert.CommandText = @"
                INSERT INTO Products
                (Barcode, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt, Name, Color, Size, Price, ArticCode)
                VALUES ($b, 0, 1, $c, $u, '', '', '', '', '')";
            insert.Parameters.AddWithValue("$b", created.Barcode);
            insert.Parameters.AddWithValue("$c", now.ToString("o"));
            insert.Parameters.AddWithValue("$u", now.ToString("o"));
            await insert.ExecuteNonQueryAsync();
        }

        await InsertScanLogAsync(conn, tx, created.Barcode, 0, 1, now, InventoryMode.Standard, section, null);
        return created;
    }

    private async Task<Product?> IncrementLootScanAsync(
        SqliteConnection conn,
        SqliteTransaction tx,
        string barcode,
        string? normalizedBoxId,
        DateTime now,
        string? section,
        bool createIfMissing)
    {
        if (normalizedBoxId is not null)
        {
            var exact = await GetLootRowAsync(conn, tx, barcode, normalizedBoxId, exactBox: true);
            if (exact is not null && exact.Id.HasValue)
            {
                var newScanned = exact.ScannedQuantity + 1;
                using var update = conn.CreateCommand();
                update.Transaction = tx;
                update.CommandText = @"
                    UPDATE LootsProducts
                    SET ScannedQuantity = $s,
                        UpdatedAt = $u
                    WHERE Id = $id";
                update.Parameters.AddWithValue("$s", newScanned);
                update.Parameters.AddWithValue("$u", now.ToString("o"));
                update.Parameters.AddWithValue("$id", exact.Id.Value);
                await update.ExecuteNonQueryAsync();

                await InsertScanLogAsync(conn, tx, exact.Barcode, exact.ScannedQuantity, newScanned, now, InventoryMode.Loots, section, normalizedBoxId);
                return ToProduct(exact, overrideBoxId: normalizedBoxId, overrideScanned: newScanned, overrideUpdatedAt: now);
            }
        }
        else
        {
            var noBox = await GetLootRowAsync(conn, tx, barcode, null, exactBox: false);
            if (noBox is not null && noBox.Id.HasValue)
            {
                var newScanned = noBox.ScannedQuantity + 1;
                using var update = conn.CreateCommand();
                update.Transaction = tx;
                update.CommandText = @"
                    UPDATE LootsProducts
                    SET ScannedQuantity = $s,
                        UpdatedAt = $u
                    WHERE Id = $id";
                update.Parameters.AddWithValue("$s", newScanned);
                update.Parameters.AddWithValue("$u", now.ToString("o"));
                update.Parameters.AddWithValue("$id", noBox.Id.Value);
                await update.ExecuteNonQueryAsync();

                await InsertScanLogAsync(conn, tx, noBox.Barcode, noBox.ScannedQuantity, newScanned, now, InventoryMode.Loots, section, null);
                return ToProduct(noBox, overrideScanned: newScanned, overrideUpdatedAt: now);
            }
        }

        var template = await GetLootRowAsync(conn, tx, barcode, null, exactBox: false)
                       ?? await GetAnyLootRowAsync(conn, tx, barcode);

        if (template is null && !createIfMissing)
            return null;

        var productToCreate = template is null
            ? new Product
            {
                Barcode = barcode,
                Box_Id = normalizedBoxId,
                InitialQuantity = 0,
                ScannedQuantity = 1,
                CreatedAt = now,
                UpdatedAt = now
            }
            : ToProduct(template, overrideBoxId: normalizedBoxId, overrideInitial: normalizedBoxId is null ? template.InitialQuantity : 0, overrideScanned: 1, overrideUpdatedAt: now);

        productToCreate.CreatedAt = now;
        productToCreate.UpdatedAt = now;

        using (var insert = conn.CreateCommand())
        {
            insert.Transaction = tx;
            insert.CommandText = @"
                INSERT INTO LootsProducts
                (Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt, Name, Color, Size, Price, ArticCode)
                VALUES ($b, $box, $i, $s, $c, $u, $n, $col, $sz, $p, $a)";
            insert.Parameters.AddWithValue("$b", productToCreate.Barcode);
            insert.Parameters.AddWithValue("$box", ToDbTextOrNull(productToCreate.Box_Id));
            insert.Parameters.AddWithValue("$i", productToCreate.InitialQuantity);
            insert.Parameters.AddWithValue("$s", productToCreate.ScannedQuantity);
            insert.Parameters.AddWithValue("$c", now.ToString("o"));
            insert.Parameters.AddWithValue("$u", now.ToString("o"));
            AddProductMetadataParameters(insert, productToCreate);
            await insert.ExecuteNonQueryAsync();
        }

        await InsertScanLogAsync(conn, tx, productToCreate.Barcode, 0, 1, now, InventoryMode.Loots, section, normalizedBoxId);
        return productToCreate;
    }

    private static async Task<LootScanTemplate?> GetLootRowAsync(SqliteConnection conn, SqliteTransaction tx, string barcode, string? boxId, bool exactBox)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = exactBox
            ? @"
                SELECT Id, Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
                       Name, Color, Size, Price, ArticCode
                FROM LootsProducts
                WHERE Barcode = $b AND Box_Id = $box
                ORDER BY UpdatedAt DESC
                LIMIT 1"
            : @"
                SELECT Id, Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
                       Name, Color, Size, Price, ArticCode
                FROM LootsProducts
                WHERE Barcode = $b AND (Box_Id IS NULL OR TRIM(Box_Id) = '')
                ORDER BY UpdatedAt DESC
                LIMIT 1";
        cmd.Parameters.AddWithValue("$b", barcode);
        if (exactBox)
            cmd.Parameters.AddWithValue("$box", boxId ?? string.Empty);

        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadLootScanTemplate(reader, hasId: true) : null;
    }

    private static async Task<LootScanTemplate?> GetAnyLootRowAsync(SqliteConnection conn, SqliteTransaction tx, string barcode)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
            SELECT Id, Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
                   Name, Color, Size, Price, ArticCode
            FROM LootsProducts
            WHERE Barcode = $b
            ORDER BY UpdatedAt DESC
            LIMIT 1";
        cmd.Parameters.AddWithValue("$b", barcode);

        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadLootScanTemplate(reader, hasId: true) : null;
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
