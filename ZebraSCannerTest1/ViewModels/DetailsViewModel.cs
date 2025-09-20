using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.Messages;
using ZebraSCannerTest1.Models;

namespace ZebraSCannerTest1.ViewModels;

[QueryProperty(nameof(ProductBarcode), "Barcode")]
[QueryProperty(nameof(ScannedQuantity), "Quantity")]
[QueryProperty(nameof(InitialQuantity), "InitialQuantity")]
public partial class DetailsViewModel : ObservableObject
{
    private readonly AppDbContext _db;

    public DetailsViewModel(AppDbContext db)
    {
        _db = db;
        SaveCommand = new AsyncRelayCommand(SaveUpdatedDetailsAsync);
    }

    [ObservableProperty] private string productBarcode;
    [ObservableProperty] private int scannedQuantity;
    [ObservableProperty] private int initialQuantity;

    public IAsyncRelayCommand SaveCommand { get; }

    public async Task SaveUpdatedDetailsAsync()
    {
        if (string.IsNullOrEmpty(ProductBarcode)) return;

        var existing = await _db.ScannedProducts.FirstOrDefaultAsync(p => p.Barcode == ProductBarcode);

        if (existing != null)
        {
            bool quantityChanged = existing.Quantity != ScannedQuantity;

            existing.Quantity = ScannedQuantity;
            existing.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            if (quantityChanged)
            {
                var newLog = new ScanLog
                {
                    Barcode = existing.Barcode,
                    Quantity = existing.Quantity,
                    InitialQuantity = existing.InitialQuantity,
                    ScannedProductId = existing.Id,
                    Timestamp = DateTime.Now
                };
                _db.ScanLogs.Add(newLog);
                await _db.SaveChangesAsync();
            }
        }
        else
        {
            // Insert new one if not in DB
            var newScanned = new ScannedProduct
            {
                Barcode = ProductBarcode,
                Quantity = ScannedQuantity,
                InitialQuantity = InitialQuantity,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _db.ScannedProducts.Add(newScanned);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine("[DOTNET][ERROR] DbUpdateException: " + ex.InnerException?.Message);
                throw; // rethrow so you still see it in debugger
            }
        }

        // ✅ Notify MainPage to refresh
        WeakReferenceMessenger.Default.Send(new ProductUpdatedMessage(new ScannedProduct
        {
            Barcode = ProductBarcode,
            Quantity = ScannedQuantity,
            InitialQuantity = InitialQuantity
        }));

        await Shell.Current.GoToAsync(".."); // navigate back
    }
}
