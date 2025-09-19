using ClosedXML.Excel;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.Models;

namespace ZebraSCannerTest1.Services;

public class ExcelImportService
{
    private readonly AppDbContext _db;

    public ExcelImportService(AppDbContext db)
    {
        _db = db;
    }

    public async Task ImportExcelAsync(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.First();

        // Assuming row 1 = headers
        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var id = row.Cell(1).GetValue<int>();
            var barcode = row.Cell(2).GetValue<string>();
            var quantity = row.Cell(3).GetValue<int>();

            // Insert into InitialProducts
            var existing = _db.InitialProducts.FirstOrDefault(p => p.Barcode == barcode);
            if (existing == null)
            {
                _db.InitialProducts.Add(new InitialProduct
                {
                    Id = id,
                    Barcode = barcode,
                    Quantity = quantity
                });
            }
            else
            {
                existing.Quantity = quantity; // update if exists
            }
        }

        await _db.SaveChangesAsync();
    }
}
