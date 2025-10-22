using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.Core.Interfaces
{
    public interface IProductRepository
    {
        Task<IEnumerable<Product>> GetRecentAsync(int limit = 8);
        Task<Product?> FindAsync(string barcode);
        Task AddAsync(Product product);
        Task UpdateAsync(Product product);
        (int TotalInitial, int TotalScanned, int TotalBarcodes, int ScannedBarcodes) GetInventoryStats();

    }
}
