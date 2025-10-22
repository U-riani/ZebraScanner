using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;

namespace ZebraSCannerTest1.Core.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _repository;
        private readonly ILoggerService<ProductService> _logger;

        public ProductService(IProductRepository repository, ILoggerService<ProductService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<IEnumerable<Product>> GetRecentAsync(int limit)
        {
            try
            {
                return await _repository.GetRecentAsync(limit);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to load recent products", ex);
                return Array.Empty<Product>();
            }
        }

        public (int TotalInitial, int TotalScanned, int TotalBarcodes, int ScannedBarcodes) GetInventoryStats()
        {
            try
            {
                return _repository.GetInventoryStats();
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to get inventory stats", ex);
                return (0, 0, 0, 0);
            }
        }

        public async Task<Product?> GetByBarcodeAsync(string barcode)
        {
            try
            {
                return await _repository.FindAsync(barcode);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to get product by barcode", ex);
                return null;
            }
        }

    }
}
