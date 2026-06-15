using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.Core.Interfaces
{
    public interface IProductService
    {
        Task<IEnumerable<Product>> GetRecentAsync(int limit, InventoryMode mode = InventoryMode.Standard);
        Task<(int TotalInitial, int TotalScanned, int TotalBarcodes, int ScannedBarcodes)> GetInventoryStats(InventoryMode mode = InventoryMode.Standard);
        Task<Product?> GetByBarcodeAsync(string barcode, InventoryMode mode = InventoryMode.Standard, string box_id = null);
        Task AddOrUpdateAsync(Product product, InventoryMode mode = InventoryMode.Standard);
        Task<IEnumerable<Product>> GetProductsByBoxAsync(string boxId, InventoryMode mode = InventoryMode.Standard);
        Task<IEnumerable<LootBarcodeProgress>> GetLootBarcodeProgressByBoxAsync(string boxId, int limit = 8);
        Task<LootBarcodeProgress?> GetLootBarcodeProgressAsync(string barcode, string boxId);
        Task<IEnumerable<Product>> GetProductsForUploadAsync(InventoryMode mode = InventoryMode.Standard);
    }
}
