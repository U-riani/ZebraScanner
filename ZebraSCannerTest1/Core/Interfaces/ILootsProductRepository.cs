using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.Core.Interfaces
{
    public interface ILootsProductRepository
    {
        Task AddAsync(LootProduct product);
        Task<IEnumerable<LootProduct>> GetAllAsync();
        Task ClearAsync();
        (int TotalInitial, int TotalScanned, int TotalBarcodes, int ScannedBarcodes) GetInventoryStats(InventoryMode mode = InventoryMode.Loots);
        Task<Product?> FindAsync(string barcode,  string boxId);
        Task UpdateAsync(Product product);
        Task<IEnumerable<Product>> GetByBoxAsync(string boxId);
    }
}
