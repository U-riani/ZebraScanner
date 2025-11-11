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
        private readonly ILootsProductRepository _lootsRepo;


        public ProductService(IProductRepository repository, ILoggerService<ProductService> logger, ILootsProductRepository lootsRepo)
        {
            _repository = repository;
            _logger = logger;
            _lootsRepo = lootsRepo;
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
                if (mode == InventoryMode.Loots)
                    return _lootsRepo.GetInventoryStats(mode);

                return _repository.GetInventoryStats(mode);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to get inventory stats", ex);
                return (0, 0, 0, 0);
            }
        }


        public async Task<Product?> GetByBarcodeAsync(string barcode, InventoryMode mode = InventoryMode.Standard, string box_id = null)
        {
            try
            {
                if (mode == InventoryMode.Loots && box_id is not null)
                    return await _lootsRepo.FindAsync(barcode, box_id);

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
                if (mode == InventoryMode.Loots)
                {
                    var existing = await _lootsRepo.FindAsync(product.Barcode, product.Box_Id);
                    if (existing is null)
                        await _lootsRepo.AddAsync(new LootProduct
                        {
                            Barcode = product.Barcode,
                            Box_Id = product.Box_Id,
                            InitialQuantity = product.InitialQuantity,
                            ScannedQuantity = product.ScannedQuantity,
                            CreatedAt = product.CreatedAt,
                            UpdatedAt = product.UpdatedAt,
                            Name = product.Name,
                            Color = product.Color,
                            Size = product.Size,
                            Price = product.Price?.ToString(),
                            ArticCode = product.ArticCode
                        });
                    else
                        await _lootsRepo.UpdateAsync(product); // or implement UpdateAsync in LootsRepo later
                    return;
                }

                // Standard
                var exist = await _repository.FindAsync(product.Barcode, mode);
                if (exist is null)
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
                return await _lootsRepo.GetByBoxAsync(boxId);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to load products by box", ex);
                return Array.Empty<Product>();
            }
        }

    }
}
