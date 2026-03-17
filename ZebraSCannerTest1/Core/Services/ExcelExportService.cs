using Microsoft.Data.Sqlite;
using MiniExcelLibs;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;

namespace ZebraSCannerTest1.Core.Services
{
    public class ExcelExportService : IExcelExportService
    {
        private readonly IDbFactory _db;

        public ExcelExportService(IDbFactory db)
        {
            _db = db;
        }

        public async Task ExportProductsAsync(
            string filePath,
            IProgress<double>? progress = null,
            InventoryMode mode = InventoryMode.Standard)
        {
            string table = mode == InventoryMode.Loots ? "LootsProducts" : "Products";

            using var conn = _db.Inventorization(mode);

            // Count rows for progress
            int totalCount = 0;
            using (var countCmd = conn.CreateCommand())
            {
                countCmd.CommandText = $"SELECT COUNT(*) FROM {table}";
                totalCount = Convert.ToInt32(countCmd.ExecuteScalar() ?? 0);
            }

            // Read data
            using var cmd = conn.CreateCommand();
            cmd.CommandText = mode == InventoryMode.Loots
                ? $@"
                    SELECT Barcode, Box_Id, InitialQuantity, ScannedQuantity,
                           Name, Color, Size, Price, ArticCode, UpdatedAt
                    FROM {table}
                    ORDER BY UpdatedAt DESC;"
                : $@"
                    SELECT Barcode, InitialQuantity, ScannedQuantity,
                           Name, Color, Size, Price, ArticCode, UpdatedAt
                    FROM {table}
                    ORDER BY UpdatedAt DESC;";

            var rows = new List<object>();
            int processed = 0;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (mode == InventoryMode.Loots)
                {
                    rows.Add(new
                    {
                        Barcode = reader.GetString(0),
                        Box_Id = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        InitialQuantity = SafeToInt(reader.GetValue(2)),
                        ScannedQuantity = SafeToInt(reader.GetValue(3)),
                        Name = SafeStr(reader, 4),
                        Color = SafeStr(reader, 5),
                        Size = SafeStr(reader, 6),
                        Price = SafeStr(reader, 7),
                        ArticCode = SafeStr(reader, 8),
                        UpdatedAt = SafeToDate(reader.GetValue(9))
                    });
                }
                else
                {
                    rows.Add(new
                    {
                        Barcode = reader.GetString(0),
                        InitialQuantity = SafeToInt(reader.GetValue(1)),
                        ScannedQuantity = SafeToInt(reader.GetValue(2)),
                        Name = SafeStr(reader, 3),
                        Color = SafeStr(reader, 4),
                        Size = SafeStr(reader, 5),
                        Price = SafeStr(reader, 6),
                        ArticCode = SafeStr(reader, 7),
                        UpdatedAt = SafeToDate(reader.GetValue(8))
                    });
                }

                processed++;
                if (processed % 200 == 0 && totalCount > 0)
                    progress?.Report((double)processed / totalCount);
            }

            // Write Excel
            await Task.Run(() => MiniExcel.SaveAs(filePath, rows));
            progress?.Report(1.0);
        }

        private static string SafeStr(SqliteDataReader r, int index) =>
            r.IsDBNull(index) ? "" : r.GetString(index);

        private static int SafeToInt(object value)
        {
            try
            {
                if (value == null || value == DBNull.Value)
                    return 0;
                return Convert.ToInt32(value);
            }
            catch { return 0; }
        }

        private static DateTime SafeToDate(object value)
        {
            try
            {
                if (value == null || value == DBNull.Value)
                    return DateTime.MinValue;

                if (DateTime.TryParse(value.ToString(), out var dt))
                    return dt;

                return DateTime.MinValue;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }
    }
}
