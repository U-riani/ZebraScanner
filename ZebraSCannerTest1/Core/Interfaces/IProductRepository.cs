using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.Core.Interfaces
{
    public interface IProductRepository
    {
        Task<IEnumerable<Product>> GetRecentAsync(int limit = 8, InventoryMode mode = InventoryMode.Standard);
        Task<Product?> FindAsync(string barcode, InventoryMode mode = InventoryMode.Standard, string? boxId = null);
        Task AddAsync(Product product, InventoryMode mode = InventoryMode.Standard);
        Task UpdateAsync(Product product, InventoryMode mode = InventoryMode.Standard);
        (int TotalInitial, int TotalScanned, int TotalBarcodes, int ScannedBarcodes) GetInventoryStats(InventoryMode mode = InventoryMode.Standard);
        Task<IEnumerable<Product>> GetByBoxAsync(string boxId, InventoryMode mode = InventoryMode.Standard);

    }

}
