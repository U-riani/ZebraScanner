using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows.Input;
using ZebraSCannerTest1.Data;
using ZebraSCannerTest1.Messages;
using ZebraSCannerTest1.Models;

namespace ZebraSCannerTest1.ViewModels;

[QueryProperty(nameof(ProductId), "ProductId")]
public partial class DetailsViewModel : BaseViewModel
{
    private readonly AppDbContext _db;

    public ICommand SaveUpdatedDetailsCommand { get; set; }


    [ObservableProperty]
    private int productId;

    [ObservableProperty]
    private ScannedProduct product;

    private string _saveStatus;
    public string SaveStatus
    {
        get => _saveStatus;
        set => SetProperty(ref _saveStatus, value);
    }

    //public IAsyncRelayCommand ScanAgainCommand { get; }


    public DetailsViewModel(AppDbContext db)
    {
        _db = db;
        SaveUpdatedDetailsCommand = new RelayCommand(SaveUpdatedDetails);

    }

    //public void Load()
    //{
    //    Product = _db.Products.FirstOrDefault(p => p.Id == ProductId);
    //}

    private void SaveCommand(object sender, EventArgs e)
    {
        // Implement save logic if needed
        // For example, update the database with changes made to the Product
    }

    //public void SaveUpdatedDetails()
    //{
    //    if (Product != null)
    //    {
    //        var existing = _db.Products.FirstOrDefault(p => p.Id == Product.Id);
    //        if (existing != null)
    //        {
    //            existing.Name = Product.Name;
    //            existing.Quantity = Product.Quantity;
    //            _db.SaveChanges();

    //            // Notify MainViewModel
    //            WeakReferenceMessenger.Default.Send(new ProductUpdatedMessage(Product));
    //        }
    //    }
    //}

    public void Load()
    {
        Product = _db.ScannedProducts
            .Where(p => p.Id == ProductId)
            .Select(p => new ScannedProduct
            {
                Id = p.Id,
                Barcode = p.Barcode,
                Quantity = p.Quantity,
                InitialQuantity = p.InitialQuantity,
            })
            .FirstOrDefault();
    }


    public async void SaveUpdatedDetails()
    {
        if (Product == null)
            return;

        var existing = _db.ScannedProducts.FirstOrDefault(p => p.Id == Product.Id);
        if (existing != null)
        {
            bool quantityChanged = existing.Quantity != Product.Quantity;

            existing.Barcode = Product.Barcode;
            existing.Quantity = Product.Quantity;
            existing.UpdatedAt = DateTime.Now;
            _db.SaveChanges();

            if (quantityChanged)
            {
                // Create a new ScanLog object and save it
                var newLog = new ScanLog
                {
                    Barcode = existing.Barcode,
                    Quantity = existing.Quantity,
                    InitialQuantity = existing.InitialQuantity,
                    ScannedProductId = existing.Id,
                    Timestamp = DateTime.Now
                };
                _db.ScanLogs.Add(newLog);
                _db.SaveChanges();

                // Notify LogsViewModel immediately
                WeakReferenceMessenger.Default.Send(new NewScanLogMessage(newLog));
            }

            // Notify MainViewModel about the update
            WeakReferenceMessenger.Default.Send(new ProductUpdatedMessage(new ScannedProduct
            {
                Id = existing.Id,
                Barcode = existing.Barcode,
                Quantity = existing.Quantity
            }));

            SaveStatus = "✔ Saved!";
            await Task.Delay(500);

            // Navigate back to MainPage
            await Shell.Current.GoToAsync("..", true);

            SaveStatus = string.Empty;
        }
    }
}
