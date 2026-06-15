using Microsoft.Data.Sqlite;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.Infrastructure.Repositories
{
    public class LootsProductRepository : ILootsProductRepository
    {
        private readonly IDbFactory _db;

        public LootsProductRepository(IDbFactory db)
        {
            _db = db;
        }

        private async Task<SqliteConnection> Conn()
        {
            return await _db.Inventorization(InventoryMode.Loots);
        }

        private static string? NormalizeBoxId(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim();
        }

        private static object ToDbTextOrNull(string? value) =>
            string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

        private static Product ReadProduct(SqliteDataReader reader, string? overrideBoxId = null, bool zeroInitialForDynamicBox = false)
        {
            var product = new Product
            {
                Barcode = reader.GetString(0),
                Box_Id = overrideBoxId ?? (reader.IsDBNull(1) ? null : reader.GetString(1)),
                InitialQuantity = zeroInitialForDynamicBox ? 0 : Convert.ToInt32(reader.GetValue(2)),
                ScannedQuantity = Convert.ToInt32(reader.GetValue(3)),
                CreatedAt = DateTime.Parse(reader.GetString(4)),
                UpdatedAt = DateTime.Parse(reader.GetString(5))
            };

            if (reader.FieldCount > 6)
            {
                product.Name = reader.IsDBNull(6) ? null : reader.GetString(6);
                product.Color = reader.IsDBNull(7) ? null : reader.GetString(7);
                product.Size = reader.IsDBNull(8) ? null : reader.GetString(8);
                product.Price = reader.IsDBNull(9) ? null : reader.GetString(9);
                product.ArticCode = reader.IsDBNull(10) ? null : reader.GetString(10);
            }

            return product;
        }

        public async Task AddAsync(LootProduct p)
        {
            using var conn = await Conn();
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
            cmd.Parameters.AddWithValue("$box", ToDbTextOrNull(p.Box_Id));

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateAsync(Product product)
        {
            using var conn = await Conn();
            var normalizedBoxId = NormalizeBoxId(product.Box_Id);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = normalizedBoxId is null
                ? @"
                    UPDATE LootsProducts
                    SET ScannedQuantity = $s, UpdatedAt = $u
                    WHERE Barcode = $b AND (Box_Id IS NULL OR TRIM(Box_Id) = '')"
                : @"
                    UPDATE LootsProducts
                    SET ScannedQuantity = $s, UpdatedAt = $u
                    WHERE Barcode = $b AND Box_Id = $box";

            cmd.Parameters.AddWithValue("$s", product.ScannedQuantity);
            cmd.Parameters.AddWithValue("$u", product.UpdatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("$b", product.Barcode);
            if (normalizedBoxId is not null)
                cmd.Parameters.AddWithValue("$box", normalizedBoxId);

            var updated = await cmd.ExecuteNonQueryAsync();
            if (updated > 0)
                return;

            await AddAsync(new LootProduct
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
                Price = product.Price?.ToString(),
                ArticCode = product.ArticCode
            });
        }

        public async Task<IEnumerable<LootProduct>> GetAllAsync()
        {
            var list = new List<LootProduct>();
            using var conn = await Conn();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM LootsProducts";

            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                try
                {
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
                }
            }

            return list;
        }

        public async Task<IEnumerable<LootBoxSummary>> GetBoxSummariesAsync()
        {
            var list = new List<LootBoxSummary>();

            using var conn = await Conn();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    Box_Id,
                    COALESCE(SUM(InitialQuantity), 0) AS InitialQuantity,
                    COALESCE(SUM(ScannedQuantity), 0) AS ScannedQuantity,
                    MAX(UpdatedAt) AS LastUpdated
                FROM LootsProducts
                WHERE Box_Id IS NOT NULL
                  AND Box_Id <> ''
                GROUP BY Box_Id
                ORDER BY LastUpdated DESC;";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var boxId = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                if (string.IsNullOrWhiteSpace(boxId))
                    continue;

                list.Add(new LootBoxSummary
                {
                    Box_Id = boxId.Trim(),
                    InitialQuantity = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1)),
                    ScannedQuantity = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2))
                });
            }

            return list;
        }

        public async Task<Product?> FindAsync(string barcode, string boxId)
        {
            using var conn = await Conn();
            var normalizedBoxId = NormalizeBoxId(boxId);

            if (normalizedBoxId is not null)
            {
                using var exact = conn.CreateCommand();
                exact.CommandText = @"
                    SELECT Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
                           Name, Color, Size, Price, ArticCode
                    FROM LootsProducts
                    WHERE Barcode = $b AND Box_Id = $box
                    ORDER BY UpdatedAt DESC
                    LIMIT 1";
                exact.Parameters.AddWithValue("$b", barcode);
                exact.Parameters.AddWithValue("$box", normalizedBoxId);

                using var exactReader = await exact.ExecuteReaderAsync();
                if (await exactReader.ReadAsync())
                    return ReadProduct(exactReader);
            }

            using var template = conn.CreateCommand();
            template.CommandText = @"
                SELECT Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
                       Name, Color, Size, Price, ArticCode
                FROM LootsProducts
                WHERE Barcode = $b AND (Box_Id IS NULL OR TRIM(Box_Id) = '')
                ORDER BY UpdatedAt DESC
                LIMIT 1";
            template.Parameters.AddWithValue("$b", barcode);

            using var templateReader = await template.ExecuteReaderAsync();
            if (await templateReader.ReadAsync())
            {
                return ReadProduct(
                    templateReader,
                    overrideBoxId: normalizedBoxId,
                    zeroInitialForDynamicBox: normalizedBoxId is not null);
            }

            // Same dynamic-box fallback as ProductRepository: if the only local row is
            // barcode + another Box_Id, use it as metadata for the current new box.
            if (normalizedBoxId is not null)
            {
                using var anySameBarcode = conn.CreateCommand();
                anySameBarcode.CommandText = @"
                    SELECT Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
                           Name, Color, Size, Price, ArticCode
                    FROM LootsProducts
                    WHERE Barcode = $b
                    ORDER BY UpdatedAt DESC
                    LIMIT 1";
                anySameBarcode.Parameters.AddWithValue("$b", barcode);

                using var anyReader = await anySameBarcode.ExecuteReaderAsync();
                if (await anyReader.ReadAsync())
                {
                    return ReadProduct(
                        anyReader,
                        overrideBoxId: normalizedBoxId,
                        zeroInitialForDynamicBox: true);
                }
            }

            return null;
        }

        public async Task<IEnumerable<Product>> GetByBoxAsync(string boxId)
        {
            var products = new List<Product>();
            var normalizedBoxId = NormalizeBoxId(boxId);

            if (normalizedBoxId is null)
                return products;

            using var conn = await Conn();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
                       Name, Color, Size, Price, ArticCode
                FROM LootsProducts
                WHERE Box_Id = $box
                ORDER BY UpdatedAt DESC";

            cmd.Parameters.AddWithValue("$box", normalizedBoxId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                products.Add(ReadProduct(reader));

            return products;
        }

        public async Task<LootBarcodeProgress?> GetBarcodeProgressAsync(string barcode, string boxId)
        {
            var normalizedBoxId = NormalizeBoxId(boxId);
            var normalizedBarcode = string.IsNullOrWhiteSpace(barcode) ? string.Empty : barcode.Trim();

            if (normalizedBoxId is null || string.IsNullOrWhiteSpace(normalizedBarcode))
                return null;

            using var conn = await Conn();

            LootBarcodeProgress? progress = null;

            // Single-barcode fast path used after each scan. This avoids refreshing
            // the whole visible box list while the scanner is already sending the next barcode.
            using (var currentBoxCmd = conn.CreateCommand())
            {
                currentBoxCmd.CommandText = @"
                    SELECT
                        Barcode,
                        COALESCE(SUM(ScannedQuantity), 0) AS CurrentBoxScanned,
                        COALESCE(SUM(InitialQuantity), 0) AS CurrentBoxExpected,
                        MAX(Name) AS Name,
                        MAX(Color) AS Color,
                        MAX(Size) AS Size,
                        MAX(Price) AS Price,
                        MAX(ArticCode) AS ArticCode
                    FROM LootsProducts
                    WHERE Barcode = $b
                      AND Box_Id = $box
                    GROUP BY Barcode
                    LIMIT 1;";
                currentBoxCmd.Parameters.AddWithValue("$b", normalizedBarcode);
                currentBoxCmd.Parameters.AddWithValue("$box", normalizedBoxId);

                using var reader = await currentBoxCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var currentBoxScanned = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1));
                    var currentBoxExpected = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2));

                    progress = new LootBarcodeProgress
                    {
                        Barcode = reader.IsDBNull(0) ? normalizedBarcode : reader.GetString(0),
                        BoxId = normalizedBoxId,
                        CurrentBoxScannedQuantity = currentBoxScanned,
                        CurrentBoxExpectedQuantity = currentBoxExpected,
                        BarcodeTotalExpectedQuantity = currentBoxExpected,
                        BarcodeTotalScannedQuantity = currentBoxScanned,
                        HasBarcodeTotalExpectedQuantity = currentBoxExpected > 0,
                        Name = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Color = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Size = reader.IsDBNull(5) ? null : reader.GetString(5),
                        Price = reader.IsDBNull(6) ? null : reader.GetString(6),
                        ArticCode = reader.IsDBNull(7) ? null : reader.GetString(7)
                    };
                }
            }

            if (progress is null)
                return null;

            using (var templateCmd = conn.CreateCommand())
            {
                templateCmd.CommandText = @"
                    SELECT COALESCE(SUM(InitialQuantity), 0) AS BarcodeExpected
                    FROM LootsProducts
                    WHERE Barcode = $b
                      AND (Box_Id IS NULL OR TRIM(Box_Id) = '');";
                templateCmd.Parameters.AddWithValue("$b", normalizedBarcode);

                var templateExpected = await templateCmd.ExecuteScalarAsync();
                var barcodeExpected = templateExpected is null || templateExpected == DBNull.Value
                    ? 0
                    : Convert.ToInt32(templateExpected);

                if (barcodeExpected > 0 && progress.CurrentBoxExpectedQuantity <= 0)
                {
                    progress.BarcodeTotalExpectedQuantity = barcodeExpected;
                    progress.HasBarcodeTotalExpectedQuantity = true;
                }
            }

            using (var scannedCmd = conn.CreateCommand())
            {
                scannedCmd.CommandText = @"
                    SELECT COALESCE(SUM(ScannedQuantity), 0) AS BarcodeScanned
                    FROM LootsProducts
                    WHERE Barcode = $b
                      AND Box_Id IS NOT NULL
                      AND TRIM(Box_Id) <> '';";
                scannedCmd.Parameters.AddWithValue("$b", normalizedBarcode);

                var scannedTotal = await scannedCmd.ExecuteScalarAsync();
                progress.BarcodeTotalScannedQuantity = scannedTotal is null || scannedTotal == DBNull.Value
                    ? progress.CurrentBoxScannedQuantity
                    : Convert.ToInt32(scannedTotal);
            }

            return progress;
        }

        public async Task<IEnumerable<LootBarcodeProgress>> GetBarcodeProgressByBoxAsync(string boxId, int limit = 8)
        {
            var progressByBarcode = new Dictionary<string, LootBarcodeProgress>(StringComparer.OrdinalIgnoreCase);
            var normalizedBoxId = NormalizeBoxId(boxId);
            var safeLimit = Math.Clamp(limit, 1, 50);

            if (normalizedBoxId is null)
                return progressByBarcode.Values;

            using var conn = await Conn();

            // Fast path for LootsScanningPage:
            // only the visible/recent rows are needed on page open. The previous version grouped
            // the whole LootsProducts table three times, which made navigation freeze on Android.
            using (var currentBoxCmd = conn.CreateCommand())
            {
                currentBoxCmd.CommandText = @"
                    SELECT
                        Barcode,
                        CurrentBoxScanned,
                        CurrentBoxExpected,
                        Name,
                        Color,
                        Size,
                        Price,
                        ArticCode,
                        LastUpdated
                    FROM (
                        SELECT
                            Barcode,
                            COALESCE(SUM(ScannedQuantity), 0) AS CurrentBoxScanned,
                            COALESCE(SUM(InitialQuantity), 0) AS CurrentBoxExpected,
                            MAX(Name) AS Name,
                            MAX(Color) AS Color,
                            MAX(Size) AS Size,
                            MAX(Price) AS Price,
                            MAX(ArticCode) AS ArticCode,
                            MAX(UpdatedAt) AS LastUpdated
                        FROM LootsProducts
                        WHERE Box_Id = $box
                        GROUP BY Barcode
                    ) recent
                    ORDER BY LastUpdated DESC
                    LIMIT $limit;";
                currentBoxCmd.Parameters.AddWithValue("$box", normalizedBoxId);
                currentBoxCmd.Parameters.AddWithValue("$limit", safeLimit);

                using var reader = await currentBoxCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var barcode = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                    if (string.IsNullOrWhiteSpace(barcode))
                        continue;

                    var currentBoxScanned = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1));
                    var currentBoxExpected = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2));

                    progressByBarcode[barcode] = new LootBarcodeProgress
                    {
                        Barcode = barcode,
                        BoxId = normalizedBoxId,
                        CurrentBoxScannedQuantity = currentBoxScanned,
                        CurrentBoxExpectedQuantity = currentBoxExpected,
                        BarcodeTotalExpectedQuantity = currentBoxExpected,
                        BarcodeTotalScannedQuantity = currentBoxScanned,
                        HasBarcodeTotalExpectedQuantity = currentBoxExpected > 0,
                        Name = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Color = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Size = reader.IsDBNull(5) ? null : reader.GetString(5),
                        Price = reader.IsDBNull(6) ? null : reader.GetString(6),
                        ArticCode = reader.IsDBNull(7) ? null : reader.GetString(7)
                    };
                }
            }

            if (progressByBarcode.Count == 0)
                return progressByBarcode.Values;

            static void AddBarcodeParameters(SqliteCommand command, IReadOnlyList<string> barcodes)
            {
                for (var i = 0; i < barcodes.Count; i++)
                    command.Parameters.AddWithValue($"$barcode{i}", barcodes[i]);
            }

            var visibleBarcodes = progressByBarcode.Keys.ToList();
            var visibleBarcodeParameters = string.Join(", ", visibleBarcodes.Select((_, i) => $"$barcode{i}"));

            using (var templateCmd = conn.CreateCommand())
            {
                templateCmd.CommandText = $@"
                    SELECT Barcode, COALESCE(SUM(InitialQuantity), 0) AS BarcodeExpected
                    FROM LootsProducts
                    WHERE Barcode IN ({visibleBarcodeParameters})
                      AND (Box_Id IS NULL OR TRIM(Box_Id) = '')
                    GROUP BY Barcode;";
                AddBarcodeParameters(templateCmd, visibleBarcodes);

                using var reader = await templateCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var barcode = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                    if (string.IsNullOrWhiteSpace(barcode) || !progressByBarcode.TryGetValue(barcode, out var progress))
                        continue;

                    var barcodeExpected = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1));
                    if (barcodeExpected <= 0 || progress.CurrentBoxExpectedQuantity > 0)
                        continue;

                    progress.BarcodeTotalExpectedQuantity = barcodeExpected;
                    progress.HasBarcodeTotalExpectedQuantity = true;
                }
            }

            using (var scannedCmd = conn.CreateCommand())
            {
                scannedCmd.CommandText = $@"
                    SELECT Barcode, COALESCE(SUM(ScannedQuantity), 0) AS BarcodeScanned
                    FROM LootsProducts
                    WHERE Barcode IN ({visibleBarcodeParameters})
                      AND Box_Id IS NOT NULL
                      AND TRIM(Box_Id) <> ''
                    GROUP BY Barcode;";
                AddBarcodeParameters(scannedCmd, visibleBarcodes);

                using var reader = await scannedCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var barcode = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                    if (string.IsNullOrWhiteSpace(barcode) || !progressByBarcode.TryGetValue(barcode, out var progress))
                        continue;

                    progress.BarcodeTotalScannedQuantity = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1));
                }
            }

            return progressByBarcode.Values;
        }

        public async Task<(int TotalInitial, int TotalScanned, int TotalBarcodes, int ScannedBarcodes)> GetInventoryStats(InventoryMode mode = InventoryMode.Loots)
        {
            using var conn = await Conn();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    COALESCE(SUM(CASE WHEN Box_Id IS NULL OR TRIM(Box_Id) = '' THEN InitialQuantity ELSE 0 END), 0),
                    COALESCE(SUM(ScannedQuantity), 0),
                    COUNT(*) AS TotalBarcodes,
                    COALESCE(SUM(CASE WHEN ScannedQuantity > 0 THEN 1 ELSE 0 END), 0) AS ScannedBarcodes
                FROM LootsProducts;";

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

        public async Task ClearAsync()
        {
            using var conn = await Conn();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM LootsProducts;";
            await cmd.ExecuteNonQueryAsync();
        }

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

        public async Task<IEnumerable<Product>> GetProductsForUploadAsync()
        {
            var products = new List<Product>();

            using var conn = await Conn();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Barcode, Box_Id, InitialQuantity, ScannedQuantity, CreatedAt, UpdatedAt,
                       Name, Color, Size, Price, ArticCode
                FROM LootsProducts
                WHERE ScannedQuantity > 0
                  AND Box_Id IS NOT NULL
                  AND TRIM(Box_Id) <> ''
                ORDER BY UpdatedAt DESC;";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                products.Add(ReadProduct(reader));

            return products;
        }
    }
}
