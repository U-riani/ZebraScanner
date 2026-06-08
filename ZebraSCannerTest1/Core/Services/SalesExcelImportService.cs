using MiniExcelLibs;
using ZebraSCannerTest1.Core.Dtos;
using ZebraSCannerTest1.Infrastructure.Repositories;

namespace ZebraSCannerTest1.Core.Services
{
    public class SalesExcelImportService
    {
        private readonly SalesRepository _repo;
        public SalesExcelImportService(SalesRepository repo)
        {
            _repo = repo;
        }

        public async Task ImportSalesExcelAsync(Stream stream, string filePath)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream), "Excel stream cannot be null.");

            await _repo.ClearSalesAsync();


            var rows = MiniExcel.Query<ExcelSalesDto>(stream).ToList();

            foreach (var item in rows)
            {
                if (string.IsNullOrWhiteSpace(item.Barcode))
                {
                    continue;
                }
                
                await _repo.UpsertSaleAsync(item);
            }
        }

    }
}
