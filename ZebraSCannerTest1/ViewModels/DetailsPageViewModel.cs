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
    private TestModel product;

    public DetailsViewModel(AppDbContext db)
    {
        _db = db;
    }

    public void Load()
    {
        Product = _db.Products.FirstOrDefault(p => p.Id == ProductId);
    }

    private void SaveCommand(object sender, EventArgs e)
    {
        // Implement save logic if needed
        // For example, update the database with changes made to the Product
    }

    public void SaveUpdatedDetails()
    {
        if (Product != null)
        {
            var existing = _db.Products.FirstOrDefault(p => p.Id == Product.Id);
            if (existing != null)
            {
                existing.Name = Product.Name;
                existing.Quantity = Product.Quantity;
                _db.SaveChanges();

                // Notify MainViewModel
                WeakReferenceMessenger.Default.Send(new ProductUpdatedMessage(Product));
            }
        }
    }
}
