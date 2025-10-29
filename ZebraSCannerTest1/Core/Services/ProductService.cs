using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;


namespace ZebraSCannerTest1.Core.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _repository;
        private readonly ILoggerService<ProductService> _logger;
        private string GetTableName(bool isLoots) => isLoots ? "LootsProducts" : "Products";


        public ProductService(IProductRepository repository, ILoggerService<ProductService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<IEnumerable<Product>> GetRecentAsync(int limit, InventoryMode mode = InventoryMode.Standard)
        {
            try
            {
                return await _repository.GetRecentAsync(limit, mode);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to load recent products", ex);
                return Array.Empty<Product>();
            }
        }

        public (int TotalInitial, int TotalScanned, int TotalBarcodes, int ScannedBarcodes) GetInventoryStats(InventoryMode mode = InventoryMode.Standard)
        {
            try
            {
                return _repository.GetInventoryStats(mode);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to get inventory stats", ex);
                return (0, 0, 0, 0);
            }
        }

        public async Task<Product?> GetByBarcodeAsync(string barcode, InventoryMode mode = InventoryMode.Standard)
        {
            try
            {
                return await _repository.FindAsync(barcode, mode);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to get product by barcode", ex);
                return null;
            }
        }

        public async Task AddOrUpdateAsync(Product product, InventoryMode mode = InventoryMode.Standard)
        {
            try
            {
                var existing = await _repository.FindAsync(product.Barcode, mode, product.Box_Id);
                if (existing is null)
                    await _repository.AddAsync(product, mode);
                else
                    await _repository.UpdateAsync(product, mode);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to add or update product", ex);
            }
        }

        public async Task<IEnumerable<Product>> GetProductsByBoxAsync(string boxId, InventoryMode mode = InventoryMode.Standard)
        {
            try
            {
                return await _repository.GetByBoxAsync(boxId, mode);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to load products by box", ex);
                return Array.Empty<Product>();
            }
        }

    }
}
