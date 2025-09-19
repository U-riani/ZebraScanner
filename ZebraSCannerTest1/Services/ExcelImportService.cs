using MiniExcelLibs;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.Models;

namespace ZebraSCannerTest1.Services
{
    public class ExcelImportService
    {
        private readonly AppDbContext _db;

        public ExcelImportService(AppDbContext db)
        {
            _db = db;
        }

        public async Task ImportExcelAsync(string filePath)
        {
            Console.WriteLine($"[DOTNET] Importing from Excel: {filePath}");

            var rows = MiniExcel.Query<ExcelProductDto>(filePath).ToList();

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Barcode))
                {
                    Console.WriteLine("[DOTNET] ❌ Skipping row - empty Barcode");
                    continue;
                }

                string barcode = row.Barcode.Trim();

                Console.WriteLine($"[DOTNET] Import Row => Id={row.Id}, Barcode='{barcode}', Qty={row.Quantity}");

                var existing = _db.InitialProducts.FirstOrDefault(p => p.Barcode == barcode);
                if (existing == null)
                {
                    _db.InitialProducts.Add(new InitialProduct
                    {
                        Id = row.Id,
                        Barcode = barcode,
                        Quantity = row.Quantity
                    });
                }
                else
                {
                    existing.Quantity = row.Quantity;
                }
            }

            await _db.SaveChangesAsync();
            Console.WriteLine($"[DOTNET] ✅ Import finished. InitialProducts count = {_db.InitialProducts.Count()}");
        }
    }
}
