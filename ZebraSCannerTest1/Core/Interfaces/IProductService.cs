using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.Core.Interfaces
{
    public interface IProductService
    {
        Task<IEnumerable<Product>> GetRecentAsync(int limit);
        (int TotalInitial, int TotalScanned, int TotalBarcodes, int ScannedBarcodes) GetInventoryStats();
        Task<Product?> GetByBarcodeAsync(string barcode);

    }
}
